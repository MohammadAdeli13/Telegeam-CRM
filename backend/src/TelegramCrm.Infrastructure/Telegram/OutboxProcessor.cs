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
            OutboxEventTypes.OrderReceiptSubmitted => DispatchReceiptSubmittedAsync(message.Payload, cancellationToken),
            OutboxEventTypes.OrderPaymentReviewed => DispatchPaymentReviewedAsync(message.Payload, cancellationToken),
            OutboxEventTypes.BotReply => DispatchBotReplyAsync(message.Payload, cancellationToken),
            _ => HandleUnknownType(message),
        };

    private async Task DispatchOrderPlacedAsync(string rawPayload, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrderPlacedOutboxPayload>(rawPayload, _json)
            ?? throw new InvalidOperationException("Invalid OrderPlacedOutboxPayload.");

        var isPersian = string.Equals(payload.ShopLanguage, "fa", StringComparison.OrdinalIgnoreCase);
        var orderLabel = FormatOrderLabel(payload.OrderId, payload.OrderNumber);
        var formattedAmount = FormatAmount(payload.TotalAmount, payload.Currency, isPersian);
        var customerLabel = string.IsNullOrWhiteSpace(payload.CustomerDisplayName)
            ? payload.CustomerTelegramUserId.ToString(CultureInfo.InvariantCulture)
            : payload.CustomerDisplayName;

        var text = isPersian
            ? $"سفارش جدید #{orderLabel} در {payload.ShopName}\nمشتری: {customerLabel}\n" +
              $"تعداد اقلام: {payload.ItemCount}\nمبلغ کل: {formattedAmount}"
            : $"New order #{orderLabel} in {payload.ShopName}\nCustomer: {customerLabel}\n" +
              $"Items: {payload.ItemCount}\nTotal: {formattedAmount}";

        if (!string.IsNullOrWhiteSpace(payload.ShippingMethodName))
        {
            text += isPersian
                ? $"\nروش ارسال: {payload.ShippingMethodName}"
                : $"\nShipping: {payload.ShippingMethodName}";
        }

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
        var orderLabel = FormatOrderLabel(payload.OrderId, payload.OrderNumber);
        var statusLabel = FormatOrderStatus(payload.ToStatus, isPersian);
        var text = isPersian
            ? $"وضعیت سفارش #{orderLabel} در فروشگاه {payload.ShopName} به «{statusLabel}» تغییر یافت."
            : $"Your order #{orderLabel} in {payload.ShopName} is now '{statusLabel}'.";

        if (!string.IsNullOrWhiteSpace(payload.CarrierName))
        {
            text += isPersian
                ? $"\nشرکت حمل‌ونقل: {payload.CarrierName}"
                : $"\nCarrier: {payload.CarrierName}";
        }

        if (!string.IsNullOrWhiteSpace(payload.TrackingCode))
        {
            text += isPersian
                ? $"\nکد رهگیری: {payload.TrackingCode}"
                : $"\nTracking code: {payload.TrackingCode}";
        }

        var localizedReason = FormatTransitionReason(payload.Reason, isPersian);
        if (!string.IsNullOrWhiteSpace(localizedReason))
        {
            text += isPersian ? $"\nتوضیح: {localizedReason}" : $"\nNote: {localizedReason}";
        }

        await _sender
            .SendMessageAsync(payload.CustomerTelegramUserId, text, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DispatchReceiptSubmittedAsync(string rawPayload, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrderReceiptSubmittedOutboxPayload>(rawPayload, _json)
            ?? throw new InvalidOperationException("Invalid OrderReceiptSubmittedOutboxPayload.");

        var isPersian = string.Equals(payload.ShopLanguage, "fa", StringComparison.OrdinalIgnoreCase);
        var orderLabel = FormatOrderLabel(payload.OrderId, payload.OrderNumber);
        var formattedAmount = FormatAmount(payload.TotalAmount, payload.Currency, isPersian);
        var customerLabel = string.IsNullOrWhiteSpace(payload.CustomerDisplayName)
            ? "-"
            : payload.CustomerDisplayName;

        var text = isPersian
            ? $"فیش واریزی جدید برای سفارش #{orderLabel} در {payload.ShopName} ارسال شد.\n" +
              $"مشتری: {customerLabel}\nمبلغ: {formattedAmount}\n" +
              "لطفاً پس از بررسی واریز در همراه بانک، فیش را در پنل تأیید یا رد کنید."
            : $"New payment receipt submitted for order #{orderLabel} in {payload.ShopName}.\n" +
              $"Customer: {customerLabel}\nAmount: {formattedAmount}\n" +
              "Please verify the transfer in your bank app before approving.";

        foreach (var merchantChatId in payload.MerchantTelegramUserIds.Where(id => id > 0))
        {
            await _sender.SendMessageAsync(merchantChatId, text, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchPaymentReviewedAsync(string rawPayload, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrderPaymentReviewedOutboxPayload>(rawPayload, _json)
            ?? throw new InvalidOperationException("Invalid OrderPaymentReviewedOutboxPayload.");

        if (payload.CustomerTelegramUserId <= 0)
        {
            return;
        }

        var isPersian = string.Equals(payload.ShopLanguage, "fa", StringComparison.OrdinalIgnoreCase);
        var orderLabel = FormatOrderLabel(payload.OrderId, payload.OrderNumber);

        string text;
        if (payload.Approved)
        {
            text = isPersian
                ? payload.AutoConfirmed
                    ? $"پرداخت سفارش #{orderLabel} در {payload.ShopName} تأیید شد و سفارش شما وارد مرحله تأیید شده گردید."
                    : $"پرداخت سفارش #{orderLabel} در {payload.ShopName} تأیید شد."
                : payload.AutoConfirmed
                    ? $"Payment for order #{orderLabel} in {payload.ShopName} was verified and your order is now Confirmed."
                    : $"Payment for order #{orderLabel} in {payload.ShopName} was verified.";
        }
        else
        {
            text = isPersian
                ? $"فیش واریزی سفارش #{orderLabel} در {payload.ShopName} تأیید نشد. لطفاً فیش صحیح را مجدداً ارسال کنید."
                : $"The payment receipt for order #{orderLabel} in {payload.ShopName} was rejected. Please upload a valid receipt.";

            if (!string.IsNullOrWhiteSpace(payload.Reason))
            {
                text += isPersian ? $"\nدلیل: {payload.Reason}" : $"\nReason: {payload.Reason}";
            }
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

    public static string FormatOrderLabel(Guid orderId, int orderNumber) =>
        orderNumber > 0
            ? orderNumber.ToString(CultureInfo.InvariantCulture)
            : orderId.ToString("N")[..8].ToUpperInvariant();

    public static string FormatOrderStatus(string status, bool isPersian)
    {
        if (!isPersian)
        {
            return status;
        }

        return status switch
        {
            "Pending" => "در انتظار تأیید",
            "Confirmed" => "تأیید شده",
            "Preparing" => "در حال آماده‌سازی",
            "Shipped" => "ارسال شده",
            "Delivered" => "تحویل شده",
            "Cancelled" => "لغو شده",
            _ => status,
        };
    }

    public static string? FormatTransitionReason(string? reason, bool isPersian)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        return reason switch
        {
            "payment_expired" => isPersian
                ? "مهلت ارسال فیش واریزی به پایان رسید."
                : "The payment window expired.",
            "auto_confirmed" or "payment_verified" => null,
            _ => reason,
        };
    }

    public static string FormatAmount(decimal amount, string currency, bool isPersian)
    {
        var isZeroDecimal = string.Equals(currency, "IRT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currency, "IRR", StringComparison.OrdinalIgnoreCase);

        var numeric = amount.ToString(isZeroDecimal ? "N0" : "N2", CultureInfo.InvariantCulture);

        if (isPersian)
        {
            var label = currency.ToUpperInvariant() switch
            {
                "IRT" => "تومان",
                "IRR" => "ریال",
                "USD" => "دلار",
                "EUR" => "یورو",
                _ => currency,
            };

            return $"{numeric} {label}";
        }

        var enLabel = string.Equals(currency, "IRT", StringComparison.OrdinalIgnoreCase)
            ? "Toman"
            : currency;

        return $"{numeric} {enLabel}";
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
