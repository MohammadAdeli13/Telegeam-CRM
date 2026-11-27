using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Telegram;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Enums;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Orders;

/// <summary>
/// Shared order lifecycle service for both the merchant back-office and customer cancel
/// flows (contract v1.2 §3, §4.3, §4.4). Enforces state-machine transitions, optimistic
/// concurrency, idempotent stock restoration on every cancellation, append-only
/// <see cref="OrderStatusHistory"/>, and transactional <see cref="OutboxMessage"/> writes.
/// </summary>
public sealed class OrderService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public OrderService(
        IAppDbContext db,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IClock clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<OrderSummaryDto>> ListMerchantOrdersAsync(
        int? page,
        int? pageSize,
        string? status,
        Guid? customerId,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = _db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                throw new DomainException("invalid_status_filter", $"Unknown order status '{status}'.");
            }

            query = query.Where(o => o.Status == parsedStatus);
        }

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(o => o.PlacedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.CustomerId,
                o.Customer != null ? o.Customer.DisplayName : null,
                o.Status.ToString(),
                o.TotalAmount,
                o.Note,
                o.PlacedAt,
                o.RowVersion))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<OrderSummaryDto>(items, total, normalizedPage, normalizedSize);
    }

    public async Task<OrderDetailDto> GetMerchantOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        return await LoadOrderDetailAsync(orderId, requiredCustomerId: null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OrderStatusHistoryDto>> GetMerchantOrderHistoryAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();

        var exists = await _db.Orders
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw NotFound(orderId);
        }

        return await _db.OrderStatusHistories
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.OccurredAt)
            .Select(h => new OrderStatusHistoryDto(
                h.Id,
                h.FromStatus.HasValue ? h.FromStatus.Value.ToString() : null,
                h.ToStatus.ToString(),
                h.ActorUserId,
                h.ActorRole,
                h.Reason,
                h.OccurredAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OrderDetailDto> TransitionMerchantOrderStatusAsync(
        Guid orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        if (!Enum.TryParse<OrderStatus>(request.Status.Trim(), ignoreCase: true, out var target))
        {
            throw new DomainException("invalid_status", $"Unknown order status '{request.Status}'.");
        }

        return await ExecuteTransitionAsync(
            tenantId,
            orderId,
            target,
            request.Reason?.Trim(),
            request.RowVersion,
            requiredCustomerId: null,
            customerCancelOnly: false,
            actorRole: _currentUser.Role ?? "MerchantOwner",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OrderDetailDto> CancelCustomerOrderAsync(
        Guid orderId,
        CancelOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var telegramUserId = _currentUser.TelegramUserId
            ?? throw new UnauthorizedAccessException("Authenticated Telegram user is required.");

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TelegramUserId == telegramUserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(orderId);

        return await ExecuteTransitionAsync(
            tenantId,
            orderId,
            OrderStatus.Cancelled,
            request?.Reason?.Trim(),
            expectedRowVersion: null,
            requiredCustomerId: customer.Id,
            customerCancelOnly: true,
            actorRole: "Customer",
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<OrderDetailDto> LoadOrderDetailAsync(
        Guid orderId,
        Guid? requiredCustomerId,
        CancellationToken cancellationToken)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .Where(o => o.Id == orderId);

        if (requiredCustomerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == requiredCustomerId.Value);
        }

        var order = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(orderId);

        return MapDetail(order);
    }

    private async Task<OrderDetailDto> ExecuteTransitionAsync(
        Guid tenantId,
        Guid orderId,
        OrderStatus target,
        string? reason,
        uint? expectedRowVersion,
        Guid? requiredCustomerId,
        bool customerCancelOnly,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var isInMemory = IsInMemory();
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var query = (isInMemory ? _db.Orders : _db.Orders.AsNoTracking())
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .Where(o => o.Id == orderId);

            if (requiredCustomerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == requiredCustomerId.Value);
            }

            var order = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                ?? throw NotFound(orderId);

            var fromStatus = order.Status;

            if (customerCancelOnly && fromStatus != OrderStatus.Pending)
            {
                throw new InvalidOrderStatusTransitionException(fromStatus, OrderStatus.Cancelled);
            }

            if (!Order.CanTransition(fromStatus, target))
            {
                throw new InvalidOrderStatusTransitionException(fromStatus, target);
            }

            if (expectedRowVersion.HasValue && order.RowVersion != expectedRowVersion.Value)
            {
                throw new DomainException(
                    "concurrency_conflict",
                    "The order was modified concurrently; reload and retry.");
            }

            var now = _clock.UtcNow;

            if (isInMemory)
            {
                order.TransitionTo(target);
                order.UpdatedAt = now;
            }
            else
            {
                // Conditional atomic update guards against concurrent double-cancellation
                // so stock restoration can run at most once (contract v1.2 §3.4).
                var updateQuery = _db.Orders
                    .Where(o => o.Id == orderId && o.TenantId == tenantId && o.Status == fromStatus);

                if (expectedRowVersion.HasValue)
                {
                    var expected = expectedRowVersion.Value;
                    updateQuery = updateQuery.Where(o => o.RowVersion == expected);
                }

                var affected = await updateQuery
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(o => o.Status, target)
                            .SetProperty(o => o.UpdatedAt, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (affected == 0)
                {
                    throw new DomainException(
                        "concurrency_conflict",
                        "The order status changed concurrently; reload and retry.");
                }
            }

            // Restore stock idempotently on EVERY transition into Cancelled, including
            // Pending -> Cancelled (contract v1.2 §3.4).
            if (Order.RestoresStock(target))
            {
                foreach (var item in order.Items.OrderBy(i => i.ProductId))
                {
                    if (isInMemory)
                    {
                        var product = await _db.Products
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken)
                            .ConfigureAwait(false);
                        if (product is not null)
                        {
                            product.Stock += item.Quantity;
                            product.UpdatedAt = now;
                        }
                    }
                    else
                    {
                        await _db.Products
                            .Where(p => p.Id == item.ProductId && p.TenantId == tenantId)
                            .ExecuteUpdateAsync(
                                setters => setters
                                    .SetProperty(p => p.Stock, p => p.Stock + item.Quantity)
                                    .SetProperty(p => p.UpdatedAt, now),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }

            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                TenantId = tenantId,
                OrderId = order.Id,
                FromStatus = fromStatus,
                ToStatus = target,
                ActorUserId = _currentUser.UserId,
                ActorRole = actorRole,
                Reason = reason,
                OccurredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });

            var payload = new OrderStatusChangedOutboxPayload(
                order.Id,
                tenantId,
                tenant.Name,
                tenant.DefaultLanguage,
                tenant.Currency,
                order.TotalAmount,
                fromStatus.ToString(),
                target.ToString(),
                order.Customer?.TelegramUserId ?? 0,
                _currentUser.UserId,
                actorRole,
                reason,
                now);

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxEventTypes.OrderStatusChanged,
                Payload = JsonSerializer.Serialize(payload, _json),
                OccurredAt = now,
                TenantId = tenantId,
                CreatedAt = now,
                UpdatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        return await LoadOrderDetailAsync(orderId, requiredCustomerId, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static OrderDetailDto MapDetail(Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.Customer?.DisplayName,
            order.Status.ToString(),
            order.TotalAmount,
            order.Note,
            order.PlacedAt,
            order.RowVersion,
            order.Items
                .OrderBy(i => i.ProductNameSnapshot, StringComparer.Ordinal)
                .Select(i => new OrderItemDto(
                    i.Id,
                    i.ProductId,
                    i.ProductNameSnapshot,
                    i.UnitPrice,
                    i.Quantity,
                    i.UnitPrice * i.Quantity))
                .ToList(),
            order.StatusHistory
                .OrderBy(h => h.OccurredAt)
                .Select(h => new OrderStatusHistoryDto(
                    h.Id,
                    h.FromStatus.HasValue ? h.FromStatus.Value.ToString() : null,
                    h.ToStatus.ToString(),
                    h.ActorUserId,
                    h.ActorRole,
                    h.Reason,
                    h.OccurredAt))
                .ToList());

    private Guid RequireTenantId() =>
        _tenantContext.TenantId is { } id && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("OrderService ran without a resolved tenant.");

    private bool IsInMemory() =>
        string.Equals(_db.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal);

    private static NotFoundException NotFound(Guid orderId) =>
        new("order_not_found", $"Order '{orderId}' was not found.");
}
