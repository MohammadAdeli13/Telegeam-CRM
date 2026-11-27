using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Application.Auth;

/// <summary>Configuration for Telegram WebApp <c>initData</c> validation.</summary>
public sealed class TelegramAuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Replay window for <c>auth_date</c>. Default 86400 (24 h) per contract v1.2 §5.1.</summary>
    public int InitDataMaxAgeSeconds { get; set; } = 86400;

    /// <summary>Access token lifetime. Default 60 minutes per contract v1.2 §5.2.</summary>
    public int JwtLifetimeMinutes { get; set; } = 60;

    /// <summary>Only honoured in Development; hard-disabled in Production.</summary>
    public bool AllowDevBypass { get; set; }
}

/// <summary>
/// Validates a raw Telegram WebApp <c>initData</c> string exactly as the official
/// documentation prescribes (contract v1.2 §5.1):
/// <list type="number">
/// <item>parse the query string and remove <c>hash</c> (and <c>signature</c>);</item>
/// <item>sort the remaining <c>key=value</c> pairs lexicographically and join with <c>\n</c>;</item>
/// <item><c>secret_key = HMAC_SHA256("WebAppData", bot_token)</c>;</item>
/// <item><c>hash == HMAC_SHA256(secret_key, data_check_string)</c>, compared in constant time;</item>
/// <item>reject when <c>now - auth_date</c> exceeds the configured window.</item>
/// </list>
/// <c>initDataUnsafe</c> is never trusted: only this HMAC path produces an identity.
/// </summary>
public sealed class TelegramInitDataValidator
{
    /// <summary>
    /// How far in the future <c>auth_date</c> may be before the payload is rejected.
    /// Telegram and the API are separate clocks, so a small skew is legitimate.
    /// </summary>
    public static readonly TimeSpan FutureAuthDateTolerance = TimeSpan.FromSeconds(60);

    private readonly IClock _clock;
    private readonly TelegramAuthOptions _options;

    public TelegramInitDataValidator(IClock clock, IOptions<TelegramAuthOptions> options)
    {
        _clock = clock;
        _options = options.Value;
    }

    public TelegramInitDataValidationResult Validate(string? initData, string? botToken)
    {
        if (string.IsNullOrWhiteSpace(initData))
        {
            return Invalid("init_data_missing");
        }

        if (string.IsNullOrWhiteSpace(botToken))
        {
            return Invalid("bot_token_not_configured");
        }

        var pairs = ParseQuery(initData);
        if (pairs.Count == 0)
        {
            return Invalid("init_data_malformed");
        }

        if (!pairs.TryGetValue("hash", out var providedHash) || string.IsNullOrWhiteSpace(providedHash))
        {
            return Invalid("init_data_hash_missing");
        }

        var dataCheckString = BuildDataCheckString(pairs);
        var secretKey = ComputeHmac(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var computedHash = Convert.ToHexString(ComputeHmac(secretKey, Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(computedHash),
                Encoding.ASCII.GetBytes(providedHash.ToLowerInvariant())))
        {
            return Invalid("init_data_invalid_hash");
        }

        if (!pairs.TryGetValue("auth_date", out var authDateRaw) ||
            !long.TryParse(authDateRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDateUnix))
        {
            return Invalid("init_data_auth_date_missing");
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        var now = _clock.UtcNow;
        var maxAge = TimeSpan.FromSeconds(_options.InitDataMaxAgeSeconds);
        if (now - authDate > maxAge)
        {
            return Invalid("init_data_expired");
        }

        // A payload signed in the future is not a legitimate Telegram payload: it would
        // otherwise stay valid for the whole replay window from a point that has not
        // happened yet. A small tolerance absorbs clock skew between Telegram and us.
        if (authDate - now > FutureAuthDateTolerance)
        {
            return Invalid("init_data_auth_date_in_future");
        }

        if (!pairs.TryGetValue("user", out var userJson) || string.IsNullOrWhiteSpace(userJson))
        {
            return Invalid("init_data_user_missing");
        }

        TelegramInitDataUser? user;
        try
        {
            user = JsonSerializer.Deserialize<TelegramInitDataUser>(userJson, _jsonOptions);
        }
        catch (JsonException)
        {
            return Invalid("init_data_user_malformed");
        }

        if (user is null || user.Id <= 0)
        {
            return Invalid("init_data_user_malformed");
        }

        return new TelegramInitDataValidationResult(true, null, user, authDate);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Telegram sends snake_case (first_name, language_code); the DTO is PascalCase.
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static TelegramInitDataValidationResult Invalid(string code) =>
        new(false, code, null, null);

    private static Dictionary<string, string> ParseQuery(string initData)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..separator]);
            var value = Uri.UnescapeDataString(part[(separator + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static string BuildDataCheckString(Dictionary<string, string> pairs) =>
        string.Join(
            '\n',
            pairs
                .Where(p => !string.Equals(p.Key, "hash", StringComparison.Ordinal) &&
                            !string.Equals(p.Key, "signature", StringComparison.Ordinal))
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={p.Value}"));

    private static byte[] ComputeHmac(byte[] key, byte[] message)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(message);
    }

}
