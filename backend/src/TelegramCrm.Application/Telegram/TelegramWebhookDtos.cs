using System.Text.Json.Serialization;

namespace TelegramCrm.Application.Telegram;

/// <summary>Minimal Telegram Bot API update payload for <c>POST /api/v1/telegram/webhook</c>.</summary>
public sealed record TelegramWebhookUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramWebhookMessage? Message);

/// <summary>Incoming Telegram message subset needed for <c>/start</c> deep links.</summary>
public sealed record TelegramWebhookMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("chat")] TelegramWebhookChat? Chat,
    [property: JsonPropertyName("from")] TelegramWebhookUser? From,
    [property: JsonPropertyName("text")] string? Text);

/// <summary>Telegram chat descriptor.</summary>
public sealed record TelegramWebhookChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string? Type);

/// <summary>Telegram sender descriptor.</summary>
public sealed record TelegramWebhookUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("language_code")] string? LanguageCode);
