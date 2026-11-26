using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TelegramCrm.Application.Auth;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Infrastructure.Auth;

/// <summary>Issues the single HS256 access token described in contract v1.2 §5.2.</summary>
public sealed class JwtTokenService : IJwtTokenService
{
    public const string TenantIdClaim = "tenant_id";
    public const string TelegramUserIdClaim = "telegram_user_id";
    public const string RoleClaim = "role";

    private readonly JwtOptions _jwt;
    private readonly TelegramAuthOptions _auth;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> jwt, IOptions<TelegramAuthOptions> auth, IClock clock)
    {
        _jwt = jwt.Value;
        _auth = auth.Value;
        _clock = clock;
    }

    public (string Token, DateTimeOffset ExpiresAt) Issue(AccessTokenClaims claims)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_auth.JwtLifetimeMinutes);

        var tokenClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, claims.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TelegramUserIdClaim, claims.TelegramUserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };

        if (claims.TenantId.HasValue)
        {
            tokenClaims.Add(new Claim(TenantIdClaim, claims.TenantId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(claims.Role))
        {
            tokenClaims.Add(new Claim(RoleClaim, claims.Role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: tokenClaims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
