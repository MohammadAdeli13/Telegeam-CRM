using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Merchant;

/// <summary>CRUD service for merchant delivery methods (courier, post, Tipax, pay-at-destination).</summary>
public sealed class ShippingMethodService : MerchantServiceBase
{
    public ShippingMethodService(IAppDbContext db, ITenantContext tenantContext, IClock clock)
        : base(db, tenantContext, clock)
    {
    }

    public async Task<IReadOnlyList<ShippingMethodDto>> ListAsync(
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = Db.ShippingMethods.AsNoTracking();
        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        var items = await query
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items.Select(ToDto).ToList();
    }

    public async Task<ShippingMethodDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var method = await Db.ShippingMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        return ToDto(method);
    }

    public async Task<ShippingMethodDto> CreateAsync(
        CreateShippingMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = Clock.UtcNow;
        var method = new ShippingMethod
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Price = request.Price,
            FreeAboveAmount = request.FreeAboveAmount,
            IsPayAtDestination = request.IsPayAtDestination ?? false,
            EstimatedDaysText = string.IsNullOrWhiteSpace(request.EstimatedDaysText)
                ? null
                : request.EstimatedDaysText.Trim(),
            SortOrder = request.SortOrder ?? 0,
            IsActive = request.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Db.ShippingMethods.Add(method);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(method);
    }

    public async Task<ShippingMethodDto> UpdateAsync(
        Guid id,
        UpdateShippingMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var method = await Db.ShippingMethods
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        if (request.Name is not null)
        {
            method.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            method.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.Price.HasValue)
        {
            method.Price = request.Price.Value;
        }

        if (request.ClearFreeAboveAmount == true)
        {
            method.FreeAboveAmount = null;
        }
        else if (request.FreeAboveAmount.HasValue)
        {
            method.FreeAboveAmount = request.FreeAboveAmount.Value;
        }

        if (request.IsPayAtDestination.HasValue)
        {
            method.IsPayAtDestination = request.IsPayAtDestination.Value;
        }

        if (request.EstimatedDaysText is not null)
        {
            method.EstimatedDaysText = string.IsNullOrWhiteSpace(request.EstimatedDaysText)
                ? null
                : request.EstimatedDaysText.Trim();
        }

        if (request.SortOrder.HasValue)
        {
            method.SortOrder = request.SortOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            method.IsActive = request.IsActive.Value;
        }

        method.UpdatedAt = Clock.UtcNow;
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(method);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var method = await Db.ShippingMethods
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        Db.ShippingMethods.Remove(method);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static NotFoundException NotFound(Guid id) =>
        new("shipping_method_not_found", $"Shipping method '{id}' was not found.");

    private static ShippingMethodDto ToDto(ShippingMethod m) =>
        new(
            m.Id,
            m.Name,
            m.Description,
            m.Price,
            m.FreeAboveAmount,
            m.IsPayAtDestination,
            m.EstimatedDaysText,
            m.SortOrder,
            m.IsActive,
            m.CreatedAt,
            m.UpdatedAt);
}
