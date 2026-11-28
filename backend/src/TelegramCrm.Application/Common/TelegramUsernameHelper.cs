using System.Text.RegularExpressions;

namespace TelegramCrm.Application.Common;

/// <summary>Normalises and validates Telegram usernames without a leading <c>@</c>.</summary>
public static partial class TelegramUsernameHelper
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim().TrimStart('@');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public static bool IsValid(string? raw)
    {
        var normalized = Normalize(raw);
        return normalized is not null && UsernameRegex().IsMatch(normalized);
    }

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9_]{3,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameRegex();
}
