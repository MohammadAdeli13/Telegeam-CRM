using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Merchant;

/// <summary>
/// Category CRUD for the merchant back-office (contract v1.2 §4.3). Every query runs
/// through the global tenant filter, so another tenant's category is simply not found
/// (404, never 403 — §6.4).
/// </summary>
public sealed class CategoryService : MerchantServiceBase
{
    public CategoryService(IAppDbContext db, ITenantContext tenantContext, IClock clock)
        : base(db, tenantContext, clock)
    {
    }

    public async Task<PagedResult<CategoryDto>> ListAsync(
        int? page,
        int? pageSize,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = Db.Categories.AsNoTracking();
        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<CategoryDto>(items, total, normalizedPage, normalizedSize);
    }

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await Db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        return ToDto(category);
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        await EnsureSlugIsFreeAsync(slug, excludingId: null, cancellationToken).ConfigureAwait(false);

        var now = Clock.UtcNow;
        var category = new Category
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            NameEn = request.NameEn?.Trim(),
            Slug = slug,
            SortOrder = request.SortOrder ?? 0,
            IsActive = request.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Db.Categories.Add(category);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await Db.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        if (request.Slug is not null)
        {
            var slug = request.Slug.Trim().ToLowerInvariant();
            if (!string.Equals(slug, category.Slug, StringComparison.Ordinal))
            {
                await EnsureSlugIsFreeAsync(slug, id, cancellationToken).ConfigureAwait(false);
                category.Slug = slug;
            }
        }

        if (request.Name is not null)
        {
            category.Name = request.Name.Trim();
        }

        if (request.NameEn is not null)
        {
            category.NameEn = request.NameEn.Trim();
        }

        if (request.SortOrder.HasValue)
        {
            category.SortOrder = request.SortOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            category.IsActive = request.IsActive.Value;
        }

        category.UpdatedAt = Clock.UtcNow;
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await Db.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        // Products reference the category with ON DELETE RESTRICT: deleting a category that
        // still has products would orphan them, so it is a business-rule conflict.
        var hasProducts = await Db.Products
            .AnyAsync(p => p.CategoryId == id, cancellationToken)
            .ConfigureAwait(false);

        if (hasProducts)
        {
            throw new DomainException(
                "category_in_use",
                "The category still has products; move or delete them first.");
        }

        Db.Categories.Remove(category);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureSlugIsFreeAsync(string slug, Guid? excludingId, CancellationToken cancellationToken)
    {
        var taken = await Db.Categories
            .AnyAsync(c => c.Slug == slug && (excludingId == null || c.Id != excludingId), cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            throw new DomainException("slug_taken", $"The category slug '{slug}' is already in use.");
        }
    }

    private static NotFoundException NotFound(Guid id) =>
        new("category_not_found", $"Category '{id}' was not found.");

    private static CategoryDto ToDto(Category c) =>
        new(c.Id, c.Name, c.NameEn, c.Slug, c.SortOrder, c.IsActive, c.CreatedAt, c.UpdatedAt);
}
