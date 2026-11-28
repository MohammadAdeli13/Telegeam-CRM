namespace TelegramCrm.Application.Merchant;

// ---------------------------------------------------------------------------
// Shop profile & branding
// ---------------------------------------------------------------------------

/// <summary>Active shop profile and branding as returned by <c>/merchant/shop</c>.</summary>
public sealed record MerchantShopDto(
    Guid Id,
    string Slug,
    string Name,
    string DefaultLanguage,
    string Currency,
    bool IsActive,
    string? LogoUrl,
    string? DefaultProductImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Request body of <c>PATCH /merchant/shop</c> — every field optional.</summary>
public sealed record UpdateMerchantShopRequest(
    string? Name,
    string? DefaultLanguage,
    string? Currency);

// ---------------------------------------------------------------------------
// Categories — contract v1.2 §4.3
// ---------------------------------------------------------------------------

/// <summary>A category as returned by the merchant back-office.</summary>
public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? NameEn,
    string Slug,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Request body of <c>POST /merchant/categories</c>.</summary>
public sealed record CreateCategoryRequest(
    string Name,
    string? NameEn,
    string Slug,
    int? SortOrder,
    bool? IsActive);

/// <summary>Request body of <c>PATCH /merchant/categories/{id}</c> — every field optional.</summary>
public sealed record UpdateCategoryRequest(
    string? Name,
    string? NameEn,
    string? Slug,
    int? SortOrder,
    bool? IsActive);

// ---------------------------------------------------------------------------
// Products — contract v1.2 §4.3
// ---------------------------------------------------------------------------

/// <summary>A product as returned by the merchant back-office.</summary>
public sealed record ProductDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? NameEn,
    string? Description,
    decimal Price,
    int Stock,
    bool IsActive,
    string? PrimaryImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Request body of <c>POST /merchant/products</c>.</summary>
public sealed record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string? NameEn,
    string? Description,
    decimal Price,
    int Stock,
    bool? IsActive);

/// <summary>Request body of <c>PATCH /merchant/products/{id}</c> — every field optional.</summary>
public sealed record UpdateProductRequest(
    Guid? CategoryId,
    string? Name,
    string? NameEn,
    string? Description,
    decimal? Price,
    bool? IsActive);

/// <summary>Request body of <c>POST /merchant/products/{id}/stock-adjust</c>.</summary>
public sealed record StockAdjustRequest(int Delta, string? Reason);

/// <summary>Result of a stock adjustment.</summary>
public sealed record StockAdjustResult(Guid ProductId, int Stock, int Delta);

// ---------------------------------------------------------------------------
// Product images — contract v1.2 §4.3
// ---------------------------------------------------------------------------

/// <summary>A product image as returned by the merchant back-office.</summary>
public sealed record ProductImageDto(
    Guid Id,
    Guid ProductId,
    string StoragePath,
    string Url,
    string? AltText,
    int SortOrder,
    bool IsPrimary);

/// <summary>Request body of <c>PATCH /merchant/products/{id}/images/{imageId}</c>.</summary>
public sealed record UpdateProductImageRequest(string? AltText, int? SortOrder);

// ---------------------------------------------------------------------------
// Customers & tags — contract v1.2 §4.3
// ---------------------------------------------------------------------------

/// <summary>A customer as returned by the merchant back-office.</summary>
public sealed record CustomerDto(
    Guid Id,
    long TelegramUserId,
    string? TelegramUsername,
    string? DisplayName,
    string? Phone,
    string? Notes,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Request body of <c>POST /merchant/customers</c>.</summary>
public sealed record CreateCustomerRequest(
    long TelegramUserId,
    string? DisplayName,
    string? Phone,
    string? Notes,
    string? TelegramUsername = null);

/// <summary>Request body of <c>PATCH /merchant/customers/{id}</c> — every field optional.</summary>
public sealed record UpdateCustomerRequest(
    string? DisplayName,
    string? Phone,
    string? Notes,
    string? TelegramUsername = null);

/// <summary>A tag as returned by the merchant back-office.</summary>
public sealed record TagDto(Guid Id, string Name, DateTimeOffset CreatedAt);

/// <summary>Request body of <c>POST /merchant/tags</c>.</summary>
public sealed record CreateTagRequest(string Name);

/// <summary>Request body of <c>POST /merchant/customers/{id}/tags</c>.</summary>
public sealed record AttachTagRequest(Guid TagId);

/// <summary>One order in a customer's order history.</summary>
public sealed record CustomerOrderDto(
    Guid Id,
    string Status,
    decimal TotalAmount,
    string? Note,
    DateTimeOffset PlacedAt);

// ---------------------------------------------------------------------------
// Settings — contract v1.2 §4.3
// ---------------------------------------------------------------------------

/// <summary>One shop setting. <see cref="Value"/> is an arbitrary JSON document.</summary>
public sealed record ShopSettingDto(string Key, string Value);

/// <summary>Request body of <c>PUT /merchant/settings</c> — replaces the whole set.</summary>
public sealed record UpdateSettingsRequest(IReadOnlyList<ShopSettingDto> Settings);
