namespace TelegramCrm.Application.Merchants;

/// <summary>Request body of <c>POST /api/v1/merchants</c>.</summary>
public sealed record CreateMerchantRequest(
    string Name,
    string Slug,
    string? DefaultLanguage,
    string? Currency);

/// <summary>A shop owned by the caller, as returned by <c>GET /api/v1/merchants/mine</c>.</summary>
public sealed record MerchantDto(
    Guid Id,
    string Slug,
    string Name,
    string DefaultLanguage,
    string Currency,
    bool IsActive,
    string Role,
    string? LogoUrl,
    string? DefaultProductImageUrl);
