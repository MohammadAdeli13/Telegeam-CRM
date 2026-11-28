namespace TelegramCrm.Application.Storefront;

/// <summary>Public shop metadata returned by <c>GET /storefront/{shopSlug}</c>.</summary>
public sealed record StorefrontShopDto(
    Guid Id,
    string Slug,
    string Name,
    string DefaultLanguage,
    string Currency,
    string? LogoUrl,
    string? DefaultProductImageUrl,
    bool RequireOrderApproval,
    bool EnableCardToCard,
    bool EnablePayOnDelivery,
    string? CardNumber,
    string? CardHolderName,
    string? BankName,
    int PaymentExpiryMinutes);

/// <summary>Active delivery method shown at storefront checkout.</summary>
public sealed record StorefrontShippingMethodDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    decimal? FreeAboveAmount,
    bool IsPayAtDestination,
    string? EstimatedDaysText,
    int SortOrder);

/// <summary>A saved delivery address of the authenticated customer.</summary>
public sealed record CustomerAddressDto(
    Guid Id,
    string Title,
    string RecipientName,
    string Phone,
    string Province,
    string City,
    string AddressLine,
    string? Plaque,
    string? Unit,
    string? PostalCode,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Request body of <c>POST /storefront/{shopSlug}/addresses</c>.</summary>
public sealed record CreateCustomerAddressRequest(
    string Title,
    string RecipientName,
    string Phone,
    string Province,
    string City,
    string AddressLine,
    string? Plaque = null,
    string? Unit = null,
    string? PostalCode = null,
    bool? IsDefault = null);

/// <summary>Request body of <c>PATCH /storefront/{shopSlug}/addresses/{id}</c>.</summary>
public sealed record UpdateCustomerAddressRequest(
    string? Title = null,
    string? RecipientName = null,
    string? Phone = null,
    string? Province = null,
    string? City = null,
    string? AddressLine = null,
    string? Plaque = null,
    string? Unit = null,
    string? PostalCode = null,
    bool? IsDefault = null);

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
public sealed record ValidateCartRequest(
    IReadOnlyList<CartLineRequest> Items,
    Guid? ShippingMethodId = null);

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
    decimal SubtotalAmount,
    decimal ShippingAmount,
    bool IsShippingPayAtDestination,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<ValidatedCartLineDto> Items);

/// <summary>Request body of <c>POST /storefront/{shopSlug}/orders</c>.</summary>
public sealed record PlaceOrderRequest(
    IReadOnlyList<CartLineRequest> Items,
    string? Note = null,
    string? Phone = null,
    Guid? AddressId = null,
    Guid? ShippingMethodId = null,
    string? PaymentMethod = null);
