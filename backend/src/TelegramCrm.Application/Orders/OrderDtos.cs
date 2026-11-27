namespace TelegramCrm.Application.Orders;

/// <summary>Compact order row used by list endpoints.</summary>
public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    string? CustomerDisplayName,
    string Status,
    decimal TotalAmount,
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

/// <summary>Full order detail including snapshotted items and status timeline.</summary>
public sealed record OrderDetailDto(
    Guid Id,
    Guid CustomerId,
    string? CustomerDisplayName,
    string Status,
    decimal TotalAmount,
    string? Note,
    DateTimeOffset PlacedAt,
    uint RowVersion,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory);

/// <summary>Request body of <c>PATCH /merchant/orders/{id}/status</c>.</summary>
public sealed record UpdateOrderStatusRequest(
    string Status,
    string? Reason,
    uint? RowVersion);

/// <summary>Optional request body of <c>POST /storefront/{shopSlug}/orders/{id}/cancel</c>.</summary>
public sealed record CancelOrderRequest(string? Reason);
