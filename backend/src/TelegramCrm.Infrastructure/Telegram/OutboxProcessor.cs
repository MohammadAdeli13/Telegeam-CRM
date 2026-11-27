using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Telegram;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Infrastructure.Persistence;

namespace TelegramCrm.Infrastructure.Telegram;

/// <summary>
/// Reads undelivered <see cref="OutboxMessage"/> rows and dispatches Telegram notifications
/// asynchronously with retry tracking (contract v1.2 §1.7, §3.5).
/// </summary>
public sealed partial class OutboxProcessor
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotSender _sender;
    private readonly IClock _clock;
    private readonly TelegramBotOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ITelegramBotSender sender,
        IClock clock,
        IOptions<TelegramBotOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _sender = sender;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Processes one batch of pending outbox messages and returns how many were delivered.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < _options.OutboxMaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(_options.OutboxBatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return 0;
        }

        var delivered = 0;

        foreach (var message in pending)
        {
            var now = _clock.UtcNow;
            message.Attempts++;
            message.UpdatedAt = now;

            try
            {
                await DispatchAsync(message, cancellationToken).ConfigureAwait(false);
                message.ProcessedAt = now;
                message.LastError = null;
                delivered++;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                LogOutboxDeliveryFailed(ex, message.Id, message.Type, message.Attempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return delivered;
    }

    private Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        message.Type switch
        {
            OutboxEventTypes.OrderPlaced => DispatchOrderPlacedAsync(message.Payload, cancellationToken),
            OutboxEventTypes.OrderStatusChanged => DispatchOrderStatusChangedAsync(message.Payload, cancellationToken),
            OutboxEventTypes.BotReply => DispatchBotReplyAsync(message.Payload, cancellationToken),
            _ => HandleUnknownType(message),
        };

    private async Task DispatchOrderPlacedAsync(string rawPayload, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrderPlacedOutboxPayload>(rawPayload, _json)
            ?? throw new InvalidOperationException("Invalid OrderPlacedOutboxPayload.");

        var isPersian = string.Equals(payload.ShopLanguage, "fa", StringComparison.OrdinalIgnoreCase);
        var amount = payload.TotalAmount.ToString("N2", CultureInfo.InvariantCulture);
        var customerLabel = string.IsNullOrWhiteSpace(payload.CustomerDisplayName)
            ? payload.CustomerTelegramUserId.ToString(CultureInfo.InvariantCulture)
            : payload.CustomerDisplayName;

        var text = isPersian
            ? $"سفارش جدید در {payload.ShopName}\nمشتری: {customerLabel}\n" +
              $"تعداد اقلام: {payload.ItemCount}\nمبلغ کل: {amount} {payload.Currency}"
            : $"New order in {payload.ShopName}\nCustomer: {customerLabel}\n" +
              $"Items: {payload.ItemCount}\nTotal: {amount} {payload.Currency}";

        foreach (var merchantChatId in payload.MerchantTelegramUserIds.Where(id => id > 0))
        {
            await _sender.SendMessageAsync(merchantChatId, text, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchOrderStatusChangedAsync(string rawPayload, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrderStatusChangedOutboxPayload>(rawPayload, _json)
            ?? throw new InvalidOperationException("Invalid OrderStatusChangedOutboxPayload.");

        if (payload.CustomerTelegramUserId <= 0)
        {
            return;
        }

        var isPersian = string.Equals(payload.ShopLanguage, "fa", StringComparison.OrdinalIgnoreCase);
        var shortId = payload.OrderId.ToString("N")[..8];
        var text = isPersian
            ? $"وضعیت سفارش #{shortId} در فروشگاه {payload.ShopName} به «{payload.ToStatus}» تغییر یافت."
            : $"Your order #{shortId} in {payload.ShopName} is now '{payload.ToStatus}'.";

        if (!string.IsNullOrWhiteSpace(payload.Reason))
        {
            text += isPersian ? $"\nتوضیح: {payload.Reason}" : $"\nNote: {payload.Reason}";
        }

        await _sender
            .SendMessageAsync(payload.CustomerTelegramUserId, text, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DispatchBotReplyAsync(string rawPayload, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BotReplyOutboxPayload>(rawPayload, _json)
            ?? throw new InvalidOperationException("Invalid BotReplyOutboxPayload.");

        if (!string.IsNullOrWhiteSpace(payload.ButtonText) && !string.IsNullOrWhiteSpace(payload.WebAppUrl))
        {
            await _sender
                .SendWebAppButtonAsync(
                    payload.ChatId,
                    payload.Text,
                    payload.ButtonText,
                    payload.WebAppUrl,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _sender
                .SendMessageAsync(payload.ChatId, payload.Text, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private Task HandleUnknownType(OutboxMessage message)
    {
        LogUnknownOutboxType(message.Id, message.Type);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Outbox delivery failed for message {MessageId} ({MessageType}), attempt {Attempts}.")]
    private partial void LogOutboxDeliveryFailed(Exception exception, Guid messageId, string messageType, int attempts);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Ignoring unknown outbox message type '{MessageType}' on message {MessageId}.")]
    private partial void LogUnknownOutboxType(Guid messageId, string messageType);
}

/// <summary>Hosted background worker polling <see cref="OutboxProcessor"/>.</summary>
public sealed partial class OutboxWorker : BackgroundService
{
    private readonly OutboxProcessor _processor;
    private readonly TelegramBotOptions _options;
    private readonly ILogger<OutboxWorker> _logger;

    public OutboxWorker(
        OutboxProcessor processor,
        IOptions<TelegramBotOptions> options,
        ILogger<OutboxWorker> logger)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableOutboxWorker)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.OutboxPollingIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _processor.ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogWorkerIterationFailed(ex);
            }
        }
    }

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Outbox worker polling iteration failed.")]
    private partial void LogWorkerIterationFailed(Exception exception);
}
