using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Merchant;

/// <summary>
/// Manages the active merchant shop's profile (name, language, currency) and branding
/// assets (shop logo and default product placeholder image). Strictly scoped to
/// <see cref="MerchantServiceBase.TenantId"/> derived from the verified JWT claim.
/// </summary>
public sealed class MerchantShopService : MerchantServiceBase
{
    private readonly IFileStorageService _storage;

    public MerchantShopService(
        IAppDbContext db,
        ITenantContext tenantContext,
        IClock clock,
        IFileStorageService storage)
        : base(db, tenantContext, clock)
    {
        _storage = storage;
    }

    public async Task<MerchantShopDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await FindActiveTenantAsync(asNoTracking: true, cancellationToken).ConfigureAwait(false);
        return ToDto(tenant);
    }

    public async Task<MerchantShopDto> UpdateAsync(
        UpdateMerchantShopRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await FindActiveTenantAsync(asNoTracking: false, cancellationToken).ConfigureAwait(false);

        if (request.Name is not null)
        {
            tenant.Name = request.Name.Trim();
        }

        if (request.DefaultLanguage is not null)
        {
            tenant.DefaultLanguage = request.DefaultLanguage;
        }

        if (request.Currency is not null)
        {
            tenant.Currency = request.Currency;
        }

        tenant.UpdatedAt = Clock.UtcNow;
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(tenant);
    }

    public Task<MerchantShopDto> UploadLogoAsync(
        string? contentType,
        long length,
        Stream content,
        CancellationToken cancellationToken = default) =>
        UploadBrandingImageAsync(
            assetPrefix: "logo",
            getCurrentPath: t => t.LogoStoragePath,
            setPath: (t, path) => t.LogoStoragePath = path,
            contentType,
            length,
            content,
            cancellationToken);

    public Task<MerchantShopDto> DeleteLogoAsync(CancellationToken cancellationToken = default) =>
        DeleteBrandingImageAsync(
            getCurrentPath: t => t.LogoStoragePath,
            setPath: (t, path) => t.LogoStoragePath = path,
            cancellationToken);

    public Task<MerchantShopDto> UploadDefaultProductImageAsync(
        string? contentType,
        long length,
        Stream content,
        CancellationToken cancellationToken = default) =>
        UploadBrandingImageAsync(
            assetPrefix: "default-product",
            getCurrentPath: t => t.DefaultProductImageStoragePath,
            setPath: (t, path) => t.DefaultProductImageStoragePath = path,
            contentType,
            length,
            content,
            cancellationToken);

    public Task<MerchantShopDto> DeleteDefaultProductImageAsync(CancellationToken cancellationToken = default) =>
        DeleteBrandingImageAsync(
            getCurrentPath: t => t.DefaultProductImageStoragePath,
            setPath: (t, path) => t.DefaultProductImageStoragePath = path,
            cancellationToken);

    private async Task<MerchantShopDto> UploadBrandingImageAsync(
        string assetPrefix,
        Func<Tenant, string?> getCurrentPath,
        Action<Tenant, string?> setPath,
        string? contentType,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
        var tenant = await FindActiveTenantAsync(asNoTracking: false, cancellationToken).ConfigureAwait(false);

        using var buffer = await ImageUploadValidator
            .ValidateAndBufferAsync(contentType, length, content, cancellationToken)
            .ConfigureAwait(false);

        var extension = ImageUploadValidator.GetExtensionForContentType(contentType!);
        var safeName = $"{assetPrefix}{extension}";
        var tenantId = TenantId;

        // Fresh Guid per upload guarantees a cache-busted public URL under Nginx /files/*.
        var newPath = await _storage
            .SaveAsync(tenantId, "branding", Guid.NewGuid(), safeName, buffer, cancellationToken)
            .ConfigureAwait(false);

        var previousPath = getCurrentPath(tenant);

        try
        {
            setPath(tenant, newPath);
            tenant.UpdatedAt = Clock.UtcNow;
            await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        return ToDto(tenant);
    }

    private async Task<MerchantShopDto> DeleteBrandingImageAsync(
        Func<Tenant, string?> getCurrentPath,
        Action<Tenant, string?> setPath,
        CancellationToken cancellationToken)
    {
        var tenant = await FindActiveTenantAsync(asNoTracking: false, cancellationToken).ConfigureAwait(false);
        var previousPath = getCurrentPath(tenant);

        if (!string.IsNullOrWhiteSpace(previousPath))
        {
            setPath(tenant, null);
            tenant.UpdatedAt = Clock.UtcNow;
            await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _storage.DeleteAsync(TenantId, previousPath, cancellationToken).ConfigureAwait(false);
        }

        return ToDto(tenant);
    }

    private async Task<Tenant> FindActiveTenantAsync(bool asNoTracking, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var query = asNoTracking ? Db.Tenants.AsNoTracking() : Db.Tenants;

        return await query
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("shop_not_found", $"Shop '{tenantId}' was not found.");
    }

    private MerchantShopDto ToDto(Tenant t) =>
        new(
            t.Id,
            t.Slug,
            t.Name,
            t.DefaultLanguage,
            t.Currency,
            t.IsActive,
            string.IsNullOrWhiteSpace(t.LogoStoragePath) ? null : _storage.GetPublicUrl(t.LogoStoragePath),
            string.IsNullOrWhiteSpace(t.DefaultProductImageStoragePath)
                ? null
                : _storage.GetPublicUrl(t.DefaultProductImageStoragePath),
            t.CreatedAt,
            t.UpdatedAt);
}
