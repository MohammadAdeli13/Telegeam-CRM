namespace TelegramCrm.Application.Common;

/// <summary>
/// The tenant resolved for the current request from the verified <c>tenant_id</c> claim
/// (or from an active storefront <c>{shopSlug}</c> route segment via
/// <see cref="IStorefrontTenantSetter"/>). Never populated from an untrusted request body
/// (contract v1.2 §6.2).
/// </summary>
public interface ITenantContext
{
    /// <summary>The active tenant, or <c>null</c> when the caller has no active tenant.</summary>
    Guid? TenantId { get; }

    /// <summary>True when a tenant is resolved and tenant-scoped queries are legal.</summary>
    bool IsResolved { get; }
}

/// <summary>
/// Narrow write-only surface used exclusively by the storefront route filter to bind an
/// active <c>{shopSlug}</c> to the current request scope so EF Core global query filters
/// and the SaveChanges tenant guard enforce isolation on storefront operations too.
/// </summary>
public interface IStorefrontTenantSetter
{
    void SetStorefrontTenant(Guid tenantId);
}

/// <summary>
/// Narrow write-only surface used by trusted background workers (such as order expiry)
/// to bind an isolated DI scope to a single tenant so EF Core global query filters and
/// <c>TenantGuardInterceptor</c> remain strictly enforced outside HTTP requests.
/// </summary>
public interface ISystemTenantSetter
{
    void SetSystemTenant(Guid tenantId);
}

/// <summary>Mutable tenant context used by the request pipeline and by tests.</summary>
public sealed class TenantContext : ITenantContext, IStorefrontTenantSetter, ISystemTenantSetter
{
    public Guid? TenantId { get; private set; }

    public bool IsResolved => TenantId.HasValue && TenantId.Value != Guid.Empty;

    public void SetTenant(Guid? tenantId) => TenantId = tenantId;

    public void SetStorefrontTenant(Guid tenantId) => TenantId = tenantId;

    public void SetSystemTenant(Guid tenantId) => TenantId = tenantId;
}
