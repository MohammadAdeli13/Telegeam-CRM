namespace TelegramCrm.Application.Telegram;

/// <summary>Well-known <c>OutboxMessage.Type</c> discriminators and their JSON payloads.</summary>
public static class OutboxEventTypes
{
    public const string OrderPlaced = "order.placed";
    public const string OrderStatusChanged = "order.status_changed";
    public const string BotReply = "bot.reply";
}

/// <summary>Outbox payload written when a customer places a new order.</summary>
public sealed record OrderPlacedOutboxPayload(
    Guid OrderId,
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
    DateTimeOffset OccurredAt);

/// <summary>Outbox payload written on every order status transition (contract v1.2 §3.5).</summary>
public sealed record OrderStatusChangedOutboxPayload(
    Guid OrderId,
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
    DateTimeOffset OccurredAt);

/// <summary>Outbox payload queued by the Telegram webhook for asynchronous reply delivery.</summary>
public sealed record BotReplyOutboxPayload(
    long ChatId,
    string Text,
    string? ButtonText,
    string? WebAppUrl);
