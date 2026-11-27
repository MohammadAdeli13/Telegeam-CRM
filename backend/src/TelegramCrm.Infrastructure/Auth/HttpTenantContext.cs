using Microsoft.AspNetCore.Http;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Infrastructure.Auth;

/// <summary>
/// Resolves <see cref="ITenantContext"/> from the active storefront <c>{shopSlug}</c> route
/// (when bound by <c>StorefrontTenantFilter</c>) or from the verified <c>tenant_id</c> claim
/// on merchant requests. An untrusted client-supplied tenant id is never consulted (contract v1.2 §6.2).
/// </summary>
public sealed class HttpTenantContext : ITenantContext, IStorefrontTenantSetter
{
    private readonly IHttpContextAccessor _accessor;
    private Guid? _storefrontTenantId;

    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? TenantId
    {
        get
        {
            if (_storefrontTenantId.HasValue && _storefrontTenantId.Value != Guid.Empty)
            {
                return _storefrontTenantId.Value;
            }

            var raw = _accessor.HttpContext?.User.FindFirst(JwtTokenService.TenantIdClaim)?.Value;
            return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
        }
    }

    public bool IsResolved => TenantId.HasValue;

    public void SetStorefrontTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Storefront tenant id cannot be empty.", nameof(tenantId));
        }

        _storefrontTenantId = tenantId;
    }
}
