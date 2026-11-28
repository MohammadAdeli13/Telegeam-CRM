namespace TelegramCrm.Application.Common;

/// <summary>
/// Supported shop currencies. <c>IRT</c> represents Iranian Toman alongside standard ISO codes.
/// </summary>
public static class ShopCurrencies
{
    public const string Default = "IRT";

    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "IRT",
            "IRR",
            "USD",
            "EUR",
        };

    public static bool IsSupported(string? currency) =>
        currency is not null && Supported.Contains(currency);
}
