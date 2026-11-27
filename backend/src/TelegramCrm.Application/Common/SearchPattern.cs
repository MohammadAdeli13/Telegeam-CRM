namespace TelegramCrm.Application.Common;

/// <summary>
/// Builds escaped SQL <c>LIKE</c> patterns that translate cleanly on PostgreSQL (Npgsql)
/// and evaluate identically on the EF Core InMemory provider without triggering CA1304/CA1862.
/// </summary>
public static class SearchPattern
{
    public const string EscapeChar = "\\";

    public static string Contains(string rawTerm)
    {
        var lower = rawTerm.Trim().ToLowerInvariant();
        var escaped = lower
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
