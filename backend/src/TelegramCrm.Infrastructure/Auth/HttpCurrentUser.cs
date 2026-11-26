using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Infrastructure.Auth;

/// <summary>Reads the caller identity from the validated JWT claims only.</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      Principal?.FindFirstValue("sub"), out var id)
            ? id
            : null;

    public long? TelegramUserId =>
        long.TryParse(Principal?.FindFirstValue(JwtTokenService.TelegramUserIdClaim), out var id) ? id : null;

    public string? Role => Principal?.FindFirstValue(JwtTokenService.RoleClaim);
}
