namespace TelegramCrm.Application.Common;

/// <summary>
/// One page of a collection, as returned by every list endpoint
/// (<c>?page=&amp;pageSize=</c> per contract v1.2 §4).
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>Normalises and clamps the paging query string.</summary>
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page is null or < 1 ? 1 : page.Value;
        var normalizedSize = pageSize is null or < 1
            ? DefaultPageSize
            : Math.Min(pageSize.Value, MaxPageSize);

        return (normalizedPage, normalizedSize);
    }
}
