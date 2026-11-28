using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Orders;
using TelegramCrm.Domain.Enums;
using TelegramCrm.Infrastructure.Persistence;

namespace TelegramCrm.Infrastructure.Orders;

/// <summary>Configuration for the automatic unpaid-order expiry background worker.</summary>
public sealed class OrderLifecycleOptions
{
    public const string SectionName = "Orders";

    public bool EnableExpiryWorker { get; set; } = true;

    public int ExpiryPollingIntervalSeconds { get; set; } = 30;

    public int ExpiryBatchSize { get; set; } = 20;
}

/// <summary>
/// Sweeps unpaid card-to-card orders whose <c>PaymentExpiresAt</c> has elapsed, cancelling
/// each order inside an isolated per-tenant scope so <c>GlobalQueryFilters</c> and
/// <c>TenantGuardInterceptor</c> remain strictly enforced while restoring product stock.
/// </summary>
public sealed partial class OrderExpiryProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly OrderLifecycleOptions _options;
    private readonly ILogger<OrderExpiryProcessor> _logger;

    public OrderExpiryProcessor(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        IOptions<OrderLifecycleOptions> options,
        ILogger<OrderExpiryProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessExpiredOrdersAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        List<(Guid OrderId, Guid TenantId)> due;
        using (var scanScope = _scopeFactory.CreateScope())
        {
            var db = scanScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var rows = await db.Orders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(o =>
                    o.Status == OrderStatus.Pending &&
                    (o.PaymentStatus == PaymentStatus.AwaitingReceipt || o.PaymentStatus == PaymentStatus.Rejected) &&
                    o.PaymentExpiresAt != null &&
                    o.PaymentExpiresAt <= now)
                .OrderBy(o => o.PaymentExpiresAt)
                .Take(_options.ExpiryBatchSize)
                .Select(o => new { o.Id, o.TenantId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            due = rows.Select(r => (r.Id, r.TenantId)).ToList();
        }

        if (due.Count == 0)
        {
            return 0;
        }

        var cancelled = 0;

        foreach (var (orderId, tenantId) in due)
        {
            using var itemScope = _scopeFactory.CreateScope();
            var tenantSetter = itemScope.ServiceProvider.GetRequiredService<ISystemTenantSetter>();
            tenantSetter.SetSystemTenant(tenantId);

            var orderService = itemScope.ServiceProvider.GetRequiredService<OrderService>();

            try
            {
                var didCancel = await orderService
                    .ExpireUnpaidOrderAsync(orderId, cancellationToken)
                    .ConfigureAwait(false);

                if (didCancel)
                {
                    cancelled++;
                    LogOrderExpired(orderId, tenantId);
                }
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogOrderExpiryFailed(ex, orderId, tenantId);
            }
        }

        return cancelled;
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Auto-cancelled expired unpaid order {OrderId} in tenant {TenantId} and restored stock.")]
    private partial void LogOrderExpired(Guid orderId, Guid tenantId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Failed to auto-cancel expired order {OrderId} in tenant {TenantId}.")]
    private partial void LogOrderExpiryFailed(Exception exception, Guid orderId, Guid tenantId);
}

/// <summary>Hosted background worker polling <see cref="OrderExpiryProcessor"/>.</summary>
public sealed partial class OrderExpiryWorker : BackgroundService
{
    private readonly OrderExpiryProcessor _processor;
    private readonly OrderLifecycleOptions _options;
    private readonly ILogger<OrderExpiryWorker> _logger;

    public OrderExpiryWorker(
        OrderExpiryProcessor processor,
        IOptions<OrderLifecycleOptions> options,
        ILogger<OrderExpiryWorker> logger)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableExpiryWorker)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.ExpiryPollingIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _processor.ProcessExpiredOrdersAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogExpiryWorkerIterationFailed(ex);
            }
        }
    }

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Order expiry worker iteration failed.")]
    private partial void LogExpiryWorkerIterationFailed(Exception exception);
}
