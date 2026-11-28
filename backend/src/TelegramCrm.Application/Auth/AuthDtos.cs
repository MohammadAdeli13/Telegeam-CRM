namespace TelegramCrm.Application.Auth;

/// <summary>Request body of <c>POST /api/v1/auth/telegram</c>.</summary>
public sealed record TelegramLoginRequest(string InitData);

/// <summary>A shop the caller belongs to, as returned by the auth and merchant endpoints.</summary>
public sealed record TenantSummary(
    Guid Id,
    string Slug,
    string Name,
    string Role,
    string DefaultLanguage,
    string Currency,
    string? LogoUrl,
    string? DefaultProductImageUrl);

/// <summary>The authenticated user as exposed to the client.</summary>
public sealed record AuthUserDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? FirstName,
    string? LastName,
    string? LanguageCode);

/// <summary>Response of <c>POST /api/v1/auth/telegram</c> and <c>/auth/switch-tenant</c>.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    AuthUserDto User,
    IReadOnlyList<TenantSummary> Tenants,
    TenantSummary? ActiveTenant);

/// <summary>Request body of <c>POST /api/v1/auth/switch-tenant</c>.</summary>
public sealed record SwitchTenantRequest(Guid TenantId);
