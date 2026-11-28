namespace TelegramCrm.Application.Storefront;

/// <summary>Public shop metadata returned by <c>GET /storefront/{shopSlug}</c>.</summary>
public sealed record StorefrontShopDto(
    Guid Id,
    string Slug,
    string Name,
    string DefaultLanguage,
    string Currency,
    string? LogoUrl,
    string? DefaultProductImageUrl);

/// <summary>Active category shown in the storefront.</summary>
public sealed record StorefrontCategoryDto(
    Guid Id,
    string Name,
    string? NameEn,
    string Slug,
    int SortOrder);

/// <summary>Public image reference for a storefront product.</summary>
public sealed record StorefrontProductImageDto(
    Guid Id,
    string Url,
    string? AltText,
    int SortOrder,
    bool IsPrimary);

/// <summary>Compact product card returned by <c>GET /storefront/{shopSlug}/products</c>.</summary>
public sealed record StorefrontProductSummaryDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? NameEn,
    decimal Price,
    int Stock,
    string? PrimaryImageUrl);

/// <summary>Full product detail returned by <c>GET /storefront/{shopSlug}/products/{id}</c>.</summary>
public sealed record StorefrontProductDetailDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? NameEn,
    string? Description,
    decimal Price,
    int Stock,
    IReadOnlyList<StorefrontProductImageDto> Images);

/// <summary>One product/quantity line in a cart validation or checkout request.</summary>
public sealed record CartLineRequest(Guid ProductId, int Quantity);

/// <summary>Request body of <c>POST /storefront/{shopSlug}/cart/validate</c>.</summary>
public sealed record ValidateCartRequest(IReadOnlyList<CartLineRequest> Items);

/// <summary>Server-validated status of one cart line.</summary>
public sealed record ValidatedCartLineDto(
    Guid ProductId,
    string? ProductName,
    decimal UnitPrice,
    int RequestedQuantity,
    int AvailableStock,
    decimal LineTotal,
    bool IsAvailable,
    string? Issue);

/// <summary>Response body of <c>POST /storefront/{shopSlug}/cart/validate</c>.</summary>
public sealed record CartValidationResponse(
    bool IsValid,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<ValidatedCartLineDto> Items);

/// <summary>Request body of <c>POST /storefront/{shopSlug}/orders</c>.</summary>
public sealed record PlaceOrderRequest(
    IReadOnlyList<CartLineRequest> Items,
    string? Note,
    string? Phone);
