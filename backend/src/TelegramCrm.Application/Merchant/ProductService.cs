using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Merchant;

/// <summary>
/// Product CRUD and stock management (contract v1.2 §4.3). Stock changes are applied with a
/// single conditional set-based statement so two concurrent adjustments can never drive the
/// stock below zero (§3.3): the guard lives in the <c>WHERE</c> clause, not in application code.
/// </summary>
public sealed class ProductService : MerchantServiceBase
{
    public ProductService(IAppDbContext db, ITenantContext tenantContext, IClock clock)
        : base(db, tenantContext, clock)
    {
    }

    public async Task<PagedResult<ProductDto>> ListAsync(
        int? page,
        int? pageSize,
        Guid? categoryId,
        string? q,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = Db.Products.AsNoTracking();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .Select(p => ToDto(p))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ProductDto>(items, total, normalizedPage, normalizedSize);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await Db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        return ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken).ConfigureAwait(false);

        var now = Clock.UtcNow;
        var product = new Product
        {
            TenantId = TenantId,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            NameEn = request.NameEn?.Trim(),
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            IsActive = request.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Db.Products.Add(product);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await Db.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        if (request.CategoryId.HasValue && request.CategoryId.Value != product.CategoryId)
        {
            await EnsureCategoryExistsAsync(request.CategoryId.Value, cancellationToken).ConfigureAwait(false);
            product.CategoryId = request.CategoryId.Value;
        }

        if (request.Name is not null)
        {
            product.Name = request.Name.Trim();
        }

        if (request.NameEn is not null)
        {
            product.NameEn = request.NameEn.Trim();
        }

        if (request.Description is not null)
        {
            product.Description = request.Description;
        }

        if (request.Price.HasValue)
        {
            product.Price = request.Price.Value;
        }

        if (request.IsActive.HasValue)
        {
            product.IsActive = request.IsActive.Value;
        }

        product.UpdatedAt = Clock.UtcNow;
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await Db.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        // OrderItem references the product with ON DELETE RESTRICT: a product that has ever
        // been ordered must not vanish, or order history would lose its anchor.
        var isReferenced = await Db.OrderItems
            .AnyAsync(i => i.ProductId == id, cancellationToken)
            .ConfigureAwait(false);

        if (isReferenced)
        {
            throw new DomainException(
                "product_in_use",
                "The product appears in existing orders; deactivate it instead of deleting it.");
        }

        Db.Products.Remove(product);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies <paramref name="request"/>.Delta to the stock atomically. A negative result is
    /// impossible: the conditional update only matches when <c>Stock + delta &gt;= 0</c>, and
    /// zero affected rows means either the product is not visible to this tenant (404) or the
    /// adjustment would go negative (409 <c>insufficient_stock</c>).
    /// </summary>
    public async Task<StockAdjustResult> AdjustStockAsync(
        Guid id,
        StockAdjustRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var delta = request.Delta;

        var affected = await Db.Products
            .Where(p => p.Id == id && p.TenantId == tenantId && p.Stock + delta >= 0)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.Stock, p => p.Stock + delta)
                    .SetProperty(p => p.UpdatedAt, Clock.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
        {
            // Distinguish "not yours / not there" (404) from "would go negative" (409).
            var exists = await Db.Products
                .AnyAsync(p => p.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (!exists)
            {
                throw NotFound(id);
            }

            throw new DomainException(
                "insufficient_stock",
                $"Adjusting the stock by {delta} would make it negative.");
        }

        var stock = await Db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => p.Stock)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        return new StockAdjustResult(id, stock, delta);
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var exists = await Db.Categories
            .AnyAsync(c => c.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new DomainException(
                "category_not_found",
                $"Category '{categoryId}' was not found in this shop.");
        }
    }

    private static NotFoundException NotFound(Guid id) =>
        new("product_not_found", $"Product '{id}' was not found.");

    private static ProductDto ToDto(Product p) =>
        new(p.Id, p.CategoryId, p.Name, p.NameEn, p.Description, p.Price, p.Stock, p.IsActive, p.CreatedAt, p.UpdatedAt);
}
