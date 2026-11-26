using Microsoft.AspNetCore.Http;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Infrastructure.Auth;

/// <summary>
/// Resolves <see cref="ITenantContext"/> from the verified <c>tenant_id</c> claim of the
/// current request. A client-supplied tenant id is never consulted (contract v1.2 §6.2).
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? TenantId
    {
        get
        {
            var raw = _accessor.HttpContext?.User.FindFirst(JwtTokenService.TenantIdClaim)?.Value;
            return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
        }
    }

    public bool IsResolved => TenantId.HasValue;
}
