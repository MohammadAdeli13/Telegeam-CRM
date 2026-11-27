using System.Collections.Concurrent;
using TelegramCrm.Application.Abstractions;

namespace TelegramCrm.IntegrationTests.Support;

/// <summary>In-memory outbound Telegram spy used by integration tests.</summary>
public sealed class FakeTelegramBotSender : ITelegramBotSender
{
    private readonly ConcurrentQueue<SentTelegramMessage> _messages = new();

    public IReadOnlyList<SentTelegramMessage> Messages => _messages.ToArray();

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new SentTelegramMessage(chatId, text, null, null));
        return Task.CompletedTask;
    }

    public Task SendWebAppButtonAsync(
        long chatId,
        string text,
        string buttonText,
        string webAppUrl,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new SentTelegramMessage(chatId, text, buttonText, webAppUrl));
        return Task.CompletedTask;
    }
}

/// <summary>Recorded outbound Telegram message.</summary>
public sealed record SentTelegramMessage(
    long ChatId,
    string Text,
    string? ButtonText,
    string? WebAppUrl);
