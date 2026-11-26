namespace TelegramCrm.Application.Auth;

/// <summary>Claims carried by the access JWT (contract v1.2 §5.2).</summary>
public sealed record AccessTokenClaims(
    Guid UserId,
    long TelegramUserId,
    Guid? TenantId,
    string? Role);

/// <summary>Issues and validates the single HS256 access token used by the API.</summary>
public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Issue(AccessTokenClaims claims);
}
