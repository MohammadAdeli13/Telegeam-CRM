namespace TelegramCrm.Application.Auth;

/// <summary>Result of validating a raw Telegram WebApp <c>initData</c> query string.</summary>
public sealed record TelegramInitDataValidationResult(
    bool IsValid,
    string? ErrorCode,
    TelegramInitDataUser? User,
    DateTimeOffset? AuthDate);

/// <summary>The subset of the Telegram user object the API needs.</summary>
public sealed record TelegramInitDataUser(
    long Id,
    string? FirstName,
    string? LastName,
    string? Username,
    string? LanguageCode);
