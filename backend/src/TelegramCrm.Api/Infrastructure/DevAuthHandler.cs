using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TelegramCrm.Infrastructure.Auth;

namespace TelegramCrm.Api.Infrastructure;

/// <summary>
/// Development-only authentication bypass. It is registered only when
/// <c>ASPNETCORE_ENVIRONMENT=Development</c> AND <c>Auth:AllowDevBypass=true</c>; the
/// startup guard in <see cref="AuthenticationSetup"/> throws if the flag is set anywhere
/// else, so this handler can never be reachable in a deployed environment (§5.5).
/// </summary>
public sealed class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-Dev-UserId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsed))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("sub", parsed.ToString()),
            new(JwtTokenService.TelegramUserIdClaim, "0"),
        };

        var tenantId = Request.Headers["X-Dev-TenantId"].FirstOrDefault();
        if (Guid.TryParse(tenantId, out var tenant))
        {
            claims.Add(new Claim(JwtTokenService.TenantIdClaim, tenant.ToString()));
            claims.Add(new Claim(JwtTokenService.RoleClaim, "MerchantOwner"));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
