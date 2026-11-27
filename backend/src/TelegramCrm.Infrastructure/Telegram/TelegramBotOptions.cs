namespace TelegramCrm.Infrastructure.Telegram;

/// <summary>Configuration for the single platform Telegram Bot and Outbox worker (contract v1.2 §5.8).</summary>
public sealed class TelegramBotOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string MiniAppBaseUrl { get; set; } = "https://localhost:8080";

    public int OutboxPollingIntervalSeconds { get; set; } = 5;

    public int OutboxBatchSize { get; set; } = 20;

    public int OutboxMaxAttempts { get; set; } = 5;

    public bool EnableOutboxWorker { get; set; } = true;
}
