using TelegramCrm.Application.Common;

namespace TelegramCrm.Application.Merchant;

/// <summary>
/// Shared plumbing for the merchant back-office services. The tenant is always taken from
/// <see cref="ITenantContext"/> (the verified <c>tenant_id</c> claim) — never from the
/// request body or route (contract v1.2 §6.2).
/// </summary>
public abstract class MerchantServiceBase
{
    protected MerchantServiceBase(IAppDbContext db, ITenantContext tenantContext, IClock clock)
    {
        Db = db;
        TenantContext = tenantContext;
        Clock = clock;
    }

    protected IAppDbContext Db { get; }

    protected ITenantContext TenantContext { get; }

    protected IClock Clock { get; }

    /// <summary>
    /// The active tenant. The <c>Merchant</c> policy already guarantees a <c>tenant_id</c>
    /// claim, so reaching this without one is a programming error, not a client error.
    /// </summary>
    protected Guid TenantId =>
        TenantContext.TenantId is { } id && id != Guid.Empty
            ? id
            : throw new InvalidOperationException(
                "A merchant endpoint ran without a resolved tenant; the Merchant policy should have rejected it.");
}
