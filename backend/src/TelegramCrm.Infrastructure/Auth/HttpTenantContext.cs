using Microsoft.AspNetCore.Http;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Infrastructure.Auth;

/// <summary>
/// Resolves <see cref="ITenantContext"/> from an explicit scoped override (storefront
/// <c>{shopSlug}</c> filter or background system scope) or from the verified <c>tenant_id</c>
/// claim on merchant requests. Untrusted client-supplied tenant ids are never consulted.
/// </summary>
public sealed class HttpTenantContext : ITenantContext, IStorefrontTenantSetter, ISystemTenantSetter
{
    private readonly IHttpContextAccessor _accessor;
    private Guid? _scopedTenantId;

    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? TenantId
    {
        get
        {
            if (_scopedTenantId.HasValue && _scopedTenantId.Value != Guid.Empty)
            {
                return _scopedTenantId.Value;
            }

            var raw = _accessor.HttpContext?.User.FindFirst(JwtTokenService.TenantIdClaim)?.Value;
            return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
        }
    }

    public bool IsResolved => TenantId.HasValue;

    public void SetStorefrontTenant(Guid tenantId) => SetOverride(tenantId, nameof(tenantId));

    public void SetSystemTenant(Guid tenantId) => SetOverride(tenantId, nameof(tenantId));

    private void SetOverride(Guid tenantId, string paramName)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Scoped tenant id cannot be empty.", paramName);
        }

        _scopedTenantId = tenantId;
    }
}
