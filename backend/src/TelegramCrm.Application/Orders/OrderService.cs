using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Telegram;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Enums;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Orders;

/// <summary>
/// Shared order lifecycle service for merchant management, customer receipt uploads,
/// customer cancellation, and background unpaid-order expiration. Enforces state-machine
/// transitions, optimistic concurrency, idempotent stock restoration, append-only
/// <see cref="OrderStatusHistory"/>, and transactional <see cref="OutboxMessage"/> writes.
/// </summary>
public sealed class OrderService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IFileStorageService _storage;

    public OrderService(
        IAppDbContext db,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IClock clock,
        IFileStorageService storage)
    {
        _db = db;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _clock = clock;
        _storage = storage;
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
                o.OrderNumber,
                o.CustomerId,
                o.Customer != null ? o.Customer.DisplayName : null,
                o.Status.ToString(),
                o.SubtotalAmount,
                o.ShippingAmount,
                o.TotalAmount,
                o.PaymentMethod.ToString(),
                o.PaymentStatus.ToString(),
                o.ShippingCity,
                o.CarrierName,
                o.TrackingCode,
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
            carrierName: string.IsNullOrWhiteSpace(request.CarrierName) ? null : request.CarrierName.Trim(),
            trackingCode: string.IsNullOrWhiteSpace(request.TrackingCode) ? null : request.TrackingCode.Trim(),
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
            carrierName: null,
            trackingCode: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Allows the customer who owns a Pending card-to-card order to upload (or re-upload after
    /// rejection) a payment receipt image. Pauses automatic payment expiry while awaiting review.
    /// </summary>
    public async Task<OrderDetailDto> SubmitCustomerReceiptAsync(
        Guid orderId,
        string? contentType,
        long length,
        Stream content,
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

        var order = await _db.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customer.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(orderId);

        if (order.Status != OrderStatus.Pending ||
            order.PaymentMethod != PaymentMethod.CardToCard ||
            (order.PaymentStatus != PaymentStatus.AwaitingReceipt && order.PaymentStatus != PaymentStatus.Rejected))
        {
            throw new DomainException(
                "receipt_upload_not_allowed",
                "A payment receipt can only be uploaded for a pending card-to-card order awaiting payment.");
        }

        using var buffer = await ImageUploadValidator
            .ValidateAndBufferAsync(contentType, length, content, cancellationToken)
            .ConfigureAwait(false);

        var extension = ImageUploadValidator.GetExtensionForContentType(contentType!);
        var fileName = $"receipt-{Guid.NewGuid():N}{extension}";
        var newPath = await _storage
            .SaveAsync(tenantId, "receipts", orderId, fileName, buffer, cancellationToken)
            .ConfigureAwait(false);

        var previousPath = order.ReceiptStoragePath;
        var now = _clock.UtcNow;

        try
        {
            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);

            var merchantTelegramIds = await _db.TenantMembers
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId)
                .Join(
                    _db.Users.AsNoTracking(),
                    m => m.UserId,
                    u => u.Id,
                    (_, u) => u.TelegramUserId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            order.ReceiptStoragePath = newPath;
            order.ReceiptSubmittedAt = now;
            order.PaymentStatus = PaymentStatus.ReceiptSubmitted;
            order.PaymentRejectionReason = null;
            order.PaymentExpiresAt = null;
            order.UpdatedAt = now;

            var payload = new OrderReceiptSubmittedOutboxPayload(
                order.Id,
                order.OrderNumber,
                tenantId,
                tenant.Name,
                tenant.DefaultLanguage,
                tenant.Currency,
                order.TotalAmount,
                order.Customer?.DisplayName,
                merchantTelegramIds,
                now);

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxEventTypes.OrderReceiptSubmitted,
                Payload = JsonSerializer.Serialize(payload, _json),
                OccurredAt = now,
                TenantId = tenantId,
                CreatedAt = now,
                UpdatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _storage.DeleteAsync(tenantId, newPath, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousPath))
        {
            await _storage.DeleteAsync(tenantId, previousPath, cancellationToken).ConfigureAwait(false);
        }

        return await LoadOrderDetailAsync(orderId, customer.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Approves or rejects a submitted card-to-card receipt. On approval, auto-transitions
    /// <c>Pending -&gt; Confirmed</c> when the shop has <c>RequireOrderApproval == false</c>.
    /// On rejection, sets a fresh payment deadline so the customer can re-upload a valid receipt.
    /// </summary>
    public async Task<OrderDetailDto> ReviewOrderPaymentAsync(
        Guid orderId,
        PaymentDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var isInMemory = IsInMemory();
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var order = await _db.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw NotFound(orderId);

            if (order.Status != OrderStatus.Pending ||
                order.PaymentMethod != PaymentMethod.CardToCard ||
                order.PaymentStatus != PaymentStatus.ReceiptSubmitted)
            {
                throw new DomainException(
                    "payment_not_reviewable",
                    "Only a pending card-to-card order with a submitted receipt can be reviewed.");
            }

            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);

            var now = _clock.UtcNow;
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            var autoConfirmed = false;

            if (request.Approve)
            {
                order.PaymentStatus = PaymentStatus.Verified;
                order.PaymentVerifiedAt = now;
                order.PaymentVerifiedByUserId = _currentUser.UserId;
                order.PaymentRejectionReason = null;
                order.PaymentExpiresAt = null;
                order.UpdatedAt = now;

                if (!tenant.RequireOrderApproval)
                {
                    autoConfirmed = true;
                    order.TransitionTo(OrderStatus.Confirmed);

                    _db.OrderStatusHistories.Add(new OrderStatusHistory
                    {
                        TenantId = tenantId,
                        OrderId = order.Id,
                        FromStatus = OrderStatus.Pending,
                        ToStatus = OrderStatus.Confirmed,
                        ActorUserId = _currentUser.UserId,
                        ActorRole = _currentUser.Role ?? "MerchantOwner",
                        Reason = reason ?? "payment_verified",
                        OccurredAt = now,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                }
            }
            else
            {
                order.PaymentStatus = PaymentStatus.Rejected;
                order.PaymentRejectionReason = reason;
                order.PaymentExpiresAt = now.AddMinutes(Math.Max(5, tenant.PaymentExpiryMinutes));
                order.UpdatedAt = now;
            }

            var payload = new OrderPaymentReviewedOutboxPayload(
                order.Id,
                order.OrderNumber,
                tenantId,
                tenant.Name,
                tenant.DefaultLanguage,
                order.Customer?.TelegramUserId ?? 0,
                request.Approve,
                autoConfirmed,
                reason,
                now);

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxEventTypes.OrderPaymentReviewed,
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

        return await LoadOrderDetailAsync(orderId, requiredCustomerId: null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Auto-cancels a single unpaid card-to-card order whose payment deadline elapsed and
    /// restores all item stock atomically. Called inside a per-tenant system scope.
    /// </summary>
    public async Task<bool> ExpireUnpaidOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var now = _clock.UtcNow;
        var isInMemory = IsInMemory();
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var orderQuery = (isInMemory ? _db.Orders : _db.Orders.AsNoTracking())
                .Include(o => o.Customer)
                .Include(o => o.Items);

            var order = await orderQuery
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                .ConfigureAwait(false);

            if (order is null ||
                order.Status != OrderStatus.Pending ||
                (order.PaymentStatus != PaymentStatus.AwaitingReceipt && order.PaymentStatus != PaymentStatus.Rejected) ||
                !order.PaymentExpiresAt.HasValue ||
                order.PaymentExpiresAt.Value > now)
            {
                return false;
            }

            if (isInMemory)
            {
                order.TransitionTo(OrderStatus.Cancelled);
                order.PaymentExpiresAt = null;
                order.UpdatedAt = now;
            }
            else
            {
                var affected = await _db.Orders
                    .Where(o =>
                        o.Id == orderId &&
                        o.TenantId == tenantId &&
                        o.Status == OrderStatus.Pending &&
                        (o.PaymentStatus == PaymentStatus.AwaitingReceipt || o.PaymentStatus == PaymentStatus.Rejected) &&
                        o.PaymentExpiresAt != null &&
                        o.PaymentExpiresAt <= now)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(o => o.Status, OrderStatus.Cancelled)
                            .SetProperty(o => o.PaymentExpiresAt, (DateTimeOffset?)null)
                            .SetProperty(o => o.UpdatedAt, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (affected == 0)
                {
                    return false;
                }
            }

            await RestoreStockAsync(tenantId, order.Items, now, isInMemory, cancellationToken)
                .ConfigureAwait(false);

            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                TenantId = tenantId,
                OrderId = order.Id,
                FromStatus = OrderStatus.Pending,
                ToStatus = OrderStatus.Cancelled,
                ActorUserId = null,
                ActorRole = "System",
                Reason = "payment_expired",
                OccurredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });

            var payload = new OrderStatusChangedOutboxPayload(
                order.Id,
                order.OrderNumber,
                tenantId,
                tenant.Name,
                tenant.DefaultLanguage,
                tenant.Currency,
                order.TotalAmount,
                OrderStatus.Pending.ToString(),
                OrderStatus.Cancelled.ToString(),
                order.Customer?.TelegramUserId ?? 0,
                ActorUserId: null,
                ActorRole: "System",
                Reason: "payment_expired",
                CarrierName: null,
                TrackingCode: null,
                OccurredAt: now);

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

            return true;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
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
        string? carrierName,
        string? trackingCode,
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

            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);

            var fromStatus = order.Status;

            if (customerCancelOnly)
            {
                var allowedForCustomer = fromStatus == OrderStatus.Pending ||
                    (fromStatus == OrderStatus.Confirmed &&
                     !tenant.RequireOrderApproval &&
                     order.PaymentStatus != PaymentStatus.Verified);

                if (!allowedForCustomer)
                {
                    throw new InvalidOrderStatusTransitionException(fromStatus, OrderStatus.Cancelled);
                }
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
            var resolvedCarrier = target == OrderStatus.Shipped ? carrierName ?? order.CarrierName : order.CarrierName;
            var resolvedTracking = target == OrderStatus.Shipped ? trackingCode ?? order.TrackingCode : order.TrackingCode;
            var resolvedShippedAt = target == OrderStatus.Shipped ? now : order.ShippedAt;
            var resolvedExpiry = target is OrderStatus.Cancelled or OrderStatus.Confirmed
                ? null
                : order.PaymentExpiresAt;

            if (isInMemory)
            {
                order.TransitionTo(target);
                order.CarrierName = resolvedCarrier;
                order.TrackingCode = resolvedTracking;
                order.ShippedAt = resolvedShippedAt;
                order.PaymentExpiresAt = resolvedExpiry;
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
                            .SetProperty(o => o.CarrierName, resolvedCarrier)
                            .SetProperty(o => o.TrackingCode, resolvedTracking)
                            .SetProperty(o => o.ShippedAt, resolvedShippedAt)
                            .SetProperty(o => o.PaymentExpiresAt, resolvedExpiry)
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

            // Restore stock idempotently on EVERY transition into Cancelled (contract v1.2 §3.4).
            if (Order.RestoresStock(target))
            {
                await RestoreStockAsync(tenantId, order.Items, now, isInMemory, cancellationToken)
                    .ConfigureAwait(false);
            }

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
                order.OrderNumber,
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
                target == OrderStatus.Shipped ? resolvedCarrier : null,
                target == OrderStatus.Shipped ? resolvedTracking : null,
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

    private async Task RestoreStockAsync(
        Guid tenantId,
        ICollection<OrderItem> items,
        DateTimeOffset now,
        bool isInMemory,
        CancellationToken cancellationToken)
    {
        foreach (var item in items.OrderBy(i => i.ProductId))
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

    private OrderDetailDto MapDetail(Order order) =>
        new(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Customer?.DisplayName,
            order.Status.ToString(),
            order.SubtotalAmount,
            order.ShippingAmount,
            order.TotalAmount,
            order.ShippingMethodNameSnapshot,
            order.IsShippingPayAtDestination,
            order.RecipientName,
            order.RecipientPhone,
            order.ShippingProvince,
            order.ShippingCity,
            order.ShippingAddressSnapshot,
            order.ShippingPostalCode,
            order.PaymentMethod.ToString(),
            order.PaymentStatus.ToString(),
            string.IsNullOrWhiteSpace(order.ReceiptStoragePath)
                ? null
                : _storage.GetPublicUrl(order.ReceiptStoragePath),
            order.ReceiptSubmittedAt,
            order.PaymentVerifiedAt,
            order.PaymentRejectionReason,
            order.PaymentExpiresAt,
            order.CarrierName,
            order.TrackingCode,
            order.ShippedAt,
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
