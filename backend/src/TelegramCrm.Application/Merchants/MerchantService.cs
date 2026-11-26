using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Enums;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Merchants;

/// <summary>
/// Merchant onboarding. Creating a shop and the caller's <c>MerchantOwner</c> membership
/// happen in one transaction so a shop can never exist without an owner.
/// </summary>
public sealed class MerchantService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public MerchantService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<MerchantDto> CreateAsync(
        Guid userId,
        CreateMerchantRequest request,
        CancellationToken cancellationToken = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        var slugTaken = await _db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

        if (slugTaken)
        {
            throw new DomainException("slug_taken", $"The slug '{slug}' is already in use.");
        }

        var now = _clock.UtcNow;
        var tenant = new Tenant
        {
            Slug = slug,
            Name = request.Name.Trim(),
            DefaultLanguage = request.DefaultLanguage ?? "fa",
            Currency = request.Currency ?? "IRR",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Tenants.Add(tenant);

        _db.TenantMembers.Add(new TenantMember
        {
            TenantId = tenant.Id,
            UserId = userId,
            Role = MemberRole.MerchantOwner,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new MerchantDto(
            tenant.Id,
            tenant.Slug,
            tenant.Name,
            tenant.DefaultLanguage,
            tenant.Currency,
            tenant.IsActive,
            MemberRole.MerchantOwner.ToString());
    }

    public async Task<IReadOnlyList<MerchantDto>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.TenantMembers
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .Join(
                _db.Tenants.IgnoreQueryFilters(),
                m => m.TenantId,
                t => t.Id,
                (m, t) => new MerchantDto(
                    t.Id,
                    t.Slug,
                    t.Name,
                    t.DefaultLanguage,
                    t.Currency,
                    t.IsActive,
                    m.Role.ToString()))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
