namespace TelegramCrm.Application.Orders;

/// <summary>Compact order row used by list endpoints.</summary>
public sealed record OrderSummaryDto(
    Guid Id,
    int OrderNumber,
    Guid CustomerId,
    string? CustomerDisplayName,
    string Status,
    decimal SubtotalAmount,
    decimal ShippingAmount,
    decimal TotalAmount,
    string PaymentMethod,
    string PaymentStatus,
    string? ShippingCity,
    string? CarrierName,
    string? TrackingCode,
    string? Note,
    DateTimeOffset PlacedAt,
    uint RowVersion);

/// <summary>One snapshotted line item of an order.</summary>
public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductNameSnapshot,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

/// <summary>One append-only audit row of an order's status timeline.</summary>
public sealed record OrderStatusHistoryDto(
    Guid Id,
    string? FromStatus,
    string ToStatus,
    Guid? ActorUserId,
    string? ActorRole,
    string? Reason,
    DateTimeOffset OccurredAt);

/// <summary>Full order detail including snapshotted items, shipping, payment, and timeline.</summary>
public sealed record OrderDetailDto(
    Guid Id,
    int OrderNumber,
    Guid CustomerId,
    string? CustomerDisplayName,
    string Status,
    decimal SubtotalAmount,
    decimal ShippingAmount,
    decimal TotalAmount,
    string? ShippingMethodNameSnapshot,
    bool IsShippingPayAtDestination,
    string? RecipientName,
    string? RecipientPhone,
    string? ShippingProvince,
    string? ShippingCity,
    string? ShippingAddressSnapshot,
    string? ShippingPostalCode,
    string PaymentMethod,
    string PaymentStatus,
    string? ReceiptUrl,
    DateTimeOffset? ReceiptSubmittedAt,
    DateTimeOffset? PaymentVerifiedAt,
    string? PaymentRejectionReason,
    DateTimeOffset? PaymentExpiresAt,
    string? CarrierName,
    string? TrackingCode,
    DateTimeOffset? ShippedAt,
    string? Note,
    DateTimeOffset PlacedAt,
    uint RowVersion,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory);

/// <summary>Request body of <c>PATCH /merchant/orders/{id}/status</c>.</summary>
public sealed record UpdateOrderStatusRequest(
    string Status,
    string? Reason = null,
    uint? RowVersion = null,
    string? CarrierName = null,
    string? TrackingCode = null);

/// <summary>Request body of <c>POST /merchant/orders/{id}/payment-decision</c>.</summary>
public sealed record PaymentDecisionRequest(
    bool Approve,
    string? Reason = null);

/// <summary>Optional request body of <c>POST /storefront/{shopSlug}/orders/{id}/cancel</c>.</summary>
public sealed record CancelOrderRequest(string? Reason);
