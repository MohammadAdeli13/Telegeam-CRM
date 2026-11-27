using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Orders;
using TelegramCrm.Application.Telegram;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Enums;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Storefront;

/// <summary>
/// Customer storefront catalog, cart validation and idempotent order placement
/// (contract v1.2 §4.4). Every call runs under the tenant resolved from <c>{shopSlug}</c>
/// by <c>StorefrontTenantFilter</c>, so EF Core global filters isolate all queries.
/// </summary>
public sealed class StorefrontService
{
    public const int MaxIdempotencyKeyLength = 128;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IFileStorageService _storage;
    private readonly OrderService _orderService;

    public StorefrontService(
        IAppDbContext db,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IClock clock,
        IFileStorageService storage,
        OrderService orderService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _clock = clock;
        _storage = storage;
        _orderService = orderService;
    }

    public async Task<StorefrontShopDto> GetShopAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("shop_not_found", "Shop was not found.");

        return new StorefrontShopDto(
            tenant.Id,
            tenant.Slug,
            tenant.Name,
            tenant.DefaultLanguage,
            tenant.Currency);
    }

    public async Task<IReadOnlyList<StorefrontCategoryDto>> ListCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();

        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new StorefrontCategoryDto(c.Id, c.Name, c.NameEn, c.Slug, c.SortOrder))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedResult<StorefrontProductSummaryDto>> ListProductsAsync(
        int? page,
        int? pageSize,
        Guid? categoryId,
        string? q,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = SearchPattern.Contains(q);
            query = query.Where(p =>
                EF.Functions.Like(p.Name.ToLowerInvariant(), pattern, SearchPattern.EscapeChar) ||
                (p.NameEn != null && EF.Functions.Like(p.NameEn.ToLowerInvariant(), pattern, SearchPattern.EscapeChar)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var products = await query
            .OrderBy(p => p.Name)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .Include(p => p.Images)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = products
            .Select(p =>
            {
                var primary = p.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .FirstOrDefault();

                return new StorefrontProductSummaryDto(
                    p.Id,
                    p.CategoryId,
                    p.Name,
                    p.NameEn,
                    p.Price,
                    p.Stock,
                    primary is null ? null : _storage.GetPublicUrl(primary.StoragePath));
            })
            .ToList();

        return new PagedResult<StorefrontProductSummaryDto>(items, total, normalizedPage, normalizedSize);
    }

    public async Task<StorefrontProductDetailDto> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();

        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");

        var images = product.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .Select(i => new StorefrontProductImageDto(
                i.Id,
                _storage.GetPublicUrl(i.StoragePath),
                i.AltText,
                i.SortOrder,
                i.IsPrimary))
            .ToList();

        return new StorefrontProductDetailDto(
            product.Id,
            product.CategoryId,
            product.Name,
            product.NameEn,
            product.Description,
            product.Price,
            product.Stock,
            images);
    }

    public async Task<CartValidationResponse> ValidateCartAsync(
        ValidateCartRequest request,
        CancellationToken cancellationToken = default)
    {
        var shop = await GetShopAsync(cancellationToken).ConfigureAwait(false);
        var merged = MergeLines(request.Items);
        var productIds = merged.Select(m => m.ProductId).ToList();

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id, cancellationToken)
            .ConfigureAwait(false);

        var validatedLines = new List<ValidatedCartLineDto>(merged.Count);
        decimal totalAmount = 0m;
        var allValid = true;

        foreach (var line in merged)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                allValid = false;
                validatedLines.Add(new ValidatedCartLineDto(
                    line.ProductId,
                    null,
                    0m,
                    line.Quantity,
                    0,
                    0m,
                    false,
                    "product_not_available"));
                continue;
            }

            var lineTotal = product.Price * line.Quantity;
            if (product.Stock < line.Quantity)
            {
                allValid = false;
                validatedLines.Add(new ValidatedCartLineDto(
                    product.Id,
                    product.Name,
                    product.Price,
                    line.Quantity,
                    product.Stock,
                    lineTotal,
                    false,
                    "insufficient_stock"));
                continue;
            }

            totalAmount += lineTotal;
            validatedLines.Add(new ValidatedCartLineDto(
                product.Id,
                product.Name,
                product.Price,
                line.Quantity,
                product.Stock,
                lineTotal,
                true,
                null));
        }

        return new CartValidationResponse(allValid, totalAmount, shop.Currency, validatedLines);
    }

    /// <summary>
    /// Places an order atomically with a mandatory <c>Idempotency-Key</c>. Returns
    /// <c>Created = true</c> on first execution and <c>Created = false</c> on idempotent
    /// replay without decrementing stock a second time.
    /// </summary>
    public async Task<(OrderDetailDto Order, bool Created)> PlaceOrderAsync(
        PlaceOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var telegramUserId = _currentUser.TelegramUserId
            ?? throw new UnauthorizedAccessException("Authenticated Telegram user is required.");

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > MaxIdempotencyKeyLength)
        {
            throw new DomainException(
                "idempotency_key_required",
                $"Header 'Idempotency-Key' is required and must be at most {MaxIdempotencyKeyLength} characters.");
        }

        var normalizedKey = idempotencyKey.Trim();
        var merged = MergeLines(request.Items);
        var customer = await GetOrCreateCustomerAsync(tenantId, telegramUserId, request.Phone, cancellationToken)
            .ConfigureAwait(false);

        var orderId = DeterministicGuid.FromOrderIdempotencyKey(tenantId, customer.Id, normalizedKey);

        // Fast-path idempotent replay.
        var existingOrder = await _db.Orders
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId && o.CustomerId == customer.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existingOrder)
        {
            var existingDetail = await _orderService
                .LoadOrderDetailAsync(orderId, customer.Id, cancellationToken)
                .ConfigureAwait(false);

            return (existingDetail, false);
        }

        var isInMemory = IsInMemory();
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var productIds = merged.Select(m => m.ProductId).ToList();
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToDictionaryAsync(p => p.Id, cancellationToken)
                .ConfigureAwait(false);

            var now = _clock.UtcNow;
            var orderItems = new List<OrderItem>(merged.Count);
            decimal totalAmount = 0m;

            // Process in deterministic ProductId order to prevent row-lock deadlocks.
            foreach (var line in merged)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                {
                    throw new DomainException(
                        "product_not_available",
                        $"Product '{line.ProductId}' is not available in this shop.");
                }

                if (isInMemory)
                {
                    if (product.Stock < line.Quantity)
                    {
                        throw new DomainException(
                            "insufficient_stock",
                            $"Insufficient stock for product '{product.Name}'.");
                    }

                    product.Stock -= line.Quantity;
                    product.UpdatedAt = now;
                }
                else
                {
                    var affected = await _db.Products
                        .Where(p => p.Id == line.ProductId && p.TenantId == tenantId && p.IsActive && p.Stock >= line.Quantity)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(p => p.Stock, p => p.Stock - line.Quantity)
                                .SetProperty(p => p.UpdatedAt, now),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (affected == 0)
                    {
                        throw new DomainException(
                            "insufficient_stock",
                            $"Insufficient stock for product '{product.Name}'.");
                    }
                }

                totalAmount += product.Price * line.Quantity;

                orderItems.Add(new OrderItem
                {
                    TenantId = tenantId,
                    OrderId = orderId,
                    ProductId = product.Id,
                    ProductNameSnapshot = product.Name,
                    UnitPrice = product.Price,
                    Quantity = line.Quantity,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

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

            var order = new Order
            {
                Id = orderId,
                TenantId = tenantId,
                CustomerId = customer.Id,
                TotalAmount = totalAmount,
                Note = request.Note?.Trim(),
                PlacedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _db.Orders.Add(order);
            _db.OrderItems.AddRange(orderItems);

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                TenantId = tenantId,
                OrderId = orderId,
                FromStatus = null,
                ToStatus = OrderStatus.Pending,
                ActorUserId = _currentUser.UserId,
                ActorRole = "Customer",
                Reason = "Order placed.",
                OccurredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });

            var payload = new OrderPlacedOutboxPayload(
                orderId,
                tenantId,
                tenant.Name,
                tenant.Slug,
                tenant.DefaultLanguage,
                tenant.Currency,
                totalAmount,
                orderItems.Sum(i => i.Quantity),
                telegramUserId,
                customer.DisplayName,
                merchantTelegramIds,
                now);

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxEventTypes.OrderPlaced,
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
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                await transaction.DisposeAsync().ConfigureAwait(false);
                transaction = null;
            }

            // Concurrent race on the exact same Idempotency-Key: return the committed order.
            var committed = await _db.Orders
                .AsNoTracking()
                .AnyAsync(o => o.Id == orderId && o.CustomerId == customer.Id, cancellationToken)
                .ConfigureAwait(false);

            if (committed)
            {
                var replay = await _orderService
                    .LoadOrderDetailAsync(orderId, customer.Id, cancellationToken)
                    .ConfigureAwait(false);

                return (replay, false);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        var detail = await _orderService
            .LoadOrderDetailAsync(orderId, customer.Id, cancellationToken)
            .ConfigureAwait(false);

        return (detail, true);
    }

    public async Task<PagedResult<OrderSummaryDto>> ListMyOrdersAsync(
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var telegramUserId = _currentUser.TelegramUserId
            ?? throw new UnauthorizedAccessException("Authenticated Telegram user is required.");

        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TelegramUserId == telegramUserId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!customerId.HasValue)
        {
            return new PagedResult<OrderSummaryDto>([], 0, normalizedPage, normalizedSize);
        }

        var query = _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId.Value);

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

    public async Task<OrderDetailDto> GetMyOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var telegramUserId = _currentUser.TelegramUserId
            ?? throw new UnauthorizedAccessException("Authenticated Telegram user is required.");

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TelegramUserId == telegramUserId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        return await _orderService
            .LoadOrderDetailAsync(orderId, customerId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        Guid tenantId,
        long telegramUserId,
        string? phone,
        CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.TelegramUserId == telegramUserId, cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;

        if (customer is not null)
        {
            if (!string.IsNullOrWhiteSpace(phone) && !string.Equals(customer.Phone, phone.Trim(), StringComparison.Ordinal))
            {
                customer.Phone = phone.Trim();
                customer.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return customer;
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, cancellationToken)
            .ConfigureAwait(false);

        var displayName = user is null
            ? null
            : string.Join(" ", new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        customer = new Customer
        {
            TenantId = tenantId,
            TelegramUserId = telegramUserId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return customer;
    }

    private static List<CartLineRequest> MergeLines(IReadOnlyList<CartLineRequest> items) =>
        items
            .GroupBy(i => i.ProductId)
            .Select(g => new CartLineRequest(g.Key, g.Sum(x => x.Quantity)))
            .OrderBy(i => i.ProductId)
            .ToList();

    private Guid RequireTenantId() =>
        _tenantContext.TenantId is { } id && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("StorefrontService ran without a resolved storefront tenant.");

    private bool IsInMemory() =>
        string.Equals(_db.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal);
}
