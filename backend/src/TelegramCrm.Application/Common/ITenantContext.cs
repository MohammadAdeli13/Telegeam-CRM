namespace TelegramCrm.Application.Common;

/// <summary>
/// The tenant resolved for the current request from the verified <c>tenant_id</c> claim.
/// Never populated from a client-supplied value (contract v1.2 §6.2).
/// </summary>
public interface ITenantContext
{
    /// <summary>The active tenant, or <c>null</c> when the caller has no active tenant.</summary>
    Guid? TenantId { get; }

    /// <summary>True when a tenant is resolved and tenant-scoped queries are legal.</summary>
    bool IsResolved { get; }
}

/// <summary>Mutable tenant context used by the request pipeline and by tests.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public bool IsResolved => TenantId.HasValue && TenantId.Value != Guid.Empty;

    public void SetTenant(Guid? tenantId) => TenantId = tenantId;
}
