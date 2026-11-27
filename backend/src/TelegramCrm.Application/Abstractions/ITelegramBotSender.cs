namespace TelegramCrm.Application.Abstractions;

/// <summary>
/// Outbound Telegram Bot API transport. Backed by <c>Telegram.Bot</c> in production and
/// replaced by an in-memory spy in integration tests.
/// </summary>
public interface ITelegramBotSender
{
    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default);

    Task SendWebAppButtonAsync(
        long chatId,
        string text,
        string buttonText,
        string webAppUrl,
        CancellationToken cancellationToken = default);
}
