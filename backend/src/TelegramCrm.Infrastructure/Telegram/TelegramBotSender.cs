using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramCrm.Application.Abstractions;

namespace TelegramCrm.Infrastructure.Telegram;

/// <summary>
/// Sends outbound Telegram notifications through the official <c>Telegram.Bot</c> client
/// using the single platform <c>TELEGRAM_BOT_TOKEN</c>.
/// </summary>
public sealed class TelegramBotSender : ITelegramBotSender
{
    private readonly Lazy<TelegramBotClient> _client;

    public TelegramBotSender(IOptions<TelegramBotOptions> options, IConfiguration configuration)
    {
        var token = configuration["TELEGRAM_BOT_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            token = options.Value.BotToken;
        }

        _client = new Lazy<TelegramBotClient>(() =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("TELEGRAM_BOT_TOKEN is not configured.");
            }

            return new TelegramBotClient(token);
        });
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        await _client.Value
            .SendMessage(chatId: chatId, text: text, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SendWebAppButtonAsync(
        long chatId,
        string text,
        string buttonText,
        string webAppUrl,
        CancellationToken cancellationToken = default)
    {
        var button = InlineKeyboardButton.WithWebApp(buttonText, new WebAppInfo { Url = webAppUrl });
        var markup = new InlineKeyboardMarkup(button);

        await _client.Value
            .SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: markup,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
