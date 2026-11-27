using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Application.Merchant;

/// <summary>
/// Shop settings (contract v1.2 §4.3). <c>PUT</c> replaces the whole set in one transaction,
/// so a client can never leave the shop with a half-applied configuration.
/// </summary>
public sealed class ShopSettingService : MerchantServiceBase
{
    public ShopSettingService(IAppDbContext db, ITenantContext tenantContext, IClock clock)
        : base(db, tenantContext, clock)
    {
    }

    public async Task<IReadOnlyList<ShopSettingDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await Db.ShopSettings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .Select(s => new ShopSettingDto(s.Key, s.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ShopSettingDto>> ReplaceAsync(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var now = Clock.UtcNow;

        await using var transaction = await Db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await Db.ShopSettings
            .ToDictionaryAsync(s => s.Key, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var incomingKeys = request.Settings.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var setting in request.Settings)
        {
            if (existing.TryGetValue(setting.Key, out var row))
            {
                row.Value = setting.Value;
                row.UpdatedAt = now;
            }
            else
            {
                Db.ShopSettings.Add(new ShopSetting
                {
                    TenantId = tenantId,
                    Key = setting.Key,
                    Value = setting.Value,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        foreach (var (key, row) in existing)
        {
            if (!incomingKeys.Contains(key))
            {
                Db.ShopSettings.Remove(row);
            }
        }

        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return await GetAsync(cancellationToken).ConfigureAwait(false);
    }
}
