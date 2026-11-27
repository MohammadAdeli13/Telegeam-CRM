using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TelegramCrm.UnitTests.Support;

/// <summary>
/// Builds a Telegram WebApp <c>initData</c> query string signed exactly the way the
/// official documentation prescribes, so the validator can be tested against real
/// signatures rather than hand-written fixtures.
/// </summary>
public static class InitDataBuilder
{
    public const string BotToken = "123456789:AAHtest-token-for-unit-tests-only";

    public static string Build(
        long telegramUserId = 42,
        string firstName = "Ada",
        DateTimeOffset? authDate = null,
        string botToken = BotToken,
        string? overrideHash = null,
        bool tamperAfterSigning = false)
    {
        var authDateUnix = (authDate ?? DateTimeOffset.UtcNow)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var user = JsonSerializer.Serialize(new
        {
            id = telegramUserId,
            first_name = firstName,
            last_name = "Lovelace",
            username = "ada",
            language_code = "en",
        });

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDateUnix,
            ["query_id"] = "AAHtestqueryid",
            ["user"] = user,
        };

        var dataCheckString = string.Join('\n', fields.Select(f => $"{f.Key}={f.Value}"));
        var secretKey = Hmac(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexString(Hmac(secretKey, Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();

        var query = string.Join('&', fields.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}"));
        query += $"&hash={overrideHash ?? hash}";

        if (tamperAfterSigning)
        {
            // Change the payload but keep the original hash: the signature must fail.
            query = query.Replace("first_name%22%3A%22Ada", "first_name%22%3A%22Mallory", StringComparison.Ordinal);
        }

        return query;
    }

    private static byte[] Hmac(byte[] key, byte[] message)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(message);
    }
}
