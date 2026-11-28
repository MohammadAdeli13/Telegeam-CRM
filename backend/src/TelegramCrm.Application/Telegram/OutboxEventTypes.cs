namespace TelegramCrm.Application.Telegram;

/// <summary>Well-known <c>OutboxMessage.Type</c> discriminators and their JSON payloads.</summary>
public static class OutboxEventTypes
{
    public const string OrderPlaced = "order.placed";
    public const string OrderStatusChanged = "order.status_changed";
    public const string OrderReceiptSubmitted = "order.receipt_submitted";
    public const string OrderPaymentReviewed = "order.payment_reviewed";
    public const string BotReply = "bot.reply";
}

/// <summary>Outbox payload written when a customer places a new order.</summary>
public sealed record OrderPlacedOutboxPayload(
    Guid OrderId,
    int OrderNumber,
    Guid TenantId,
    string ShopName,
    string ShopSlug,
    string ShopLanguage,
    string Currency,
    decimal TotalAmount,
    int ItemCount,
    long CustomerTelegramUserId,
    string? CustomerDisplayName,
    IReadOnlyList<long> MerchantTelegramUserIds,
    string? ShippingMethodName,
    string PaymentMethod,
    DateTimeOffset OccurredAt);

/// <summary>Outbox payload written on every order status transition (contract v1.2 §3.5).</summary>
public sealed record OrderStatusChangedOutboxPayload(
    Guid OrderId,
    int OrderNumber,
    Guid TenantId,
    string ShopName,
    string ShopLanguage,
    string Currency,
    decimal TotalAmount,
    string FromStatus,
    string ToStatus,
    long CustomerTelegramUserId,
    Guid? ActorUserId,
    string? ActorRole,
    string? Reason,
    string? CarrierName,
    string? TrackingCode,
    DateTimeOffset OccurredAt);

/// <summary>Outbox payload written when a customer uploads a card-to-card payment receipt.</summary>
public sealed record OrderReceiptSubmittedOutboxPayload(
    Guid OrderId,
    int OrderNumber,
    Guid TenantId,
    string ShopName,
    string ShopLanguage,
    string Currency,
    decimal TotalAmount,
    string? CustomerDisplayName,
    IReadOnlyList<long> MerchantTelegramUserIds,
    DateTimeOffset OccurredAt);

/// <summary>Outbox payload written when a merchant approves or rejects a payment receipt.</summary>
public sealed record OrderPaymentReviewedOutboxPayload(
    Guid OrderId,
    int OrderNumber,
    Guid TenantId,
    string ShopName,
    string ShopLanguage,
    long CustomerTelegramUserId,
    bool Approved,
    bool AutoConfirmed,
    string? Reason,
    DateTimeOffset OccurredAt);

/// <summary>Outbox payload queued by the Telegram webhook for asynchronous reply delivery.</summary>
public sealed record BotReplyOutboxPayload(
    long ChatId,
    string Text,
    string? ButtonText,
    string? WebAppUrl);
