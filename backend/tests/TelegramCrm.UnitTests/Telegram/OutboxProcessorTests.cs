using TelegramCrm.Infrastructure.Telegram;

namespace TelegramCrm.UnitTests.Telegram;

public sealed class OutboxProcessorTests
{
    [Theory]
    [InlineData("Pending", "در انتظار تأیید")]
    [InlineData("Confirmed", "تأیید شده")]
    [InlineData("Preparing", "در حال آماده‌سازی")]
    [InlineData("Shipped", "ارسال شده")]
    [InlineData("Delivered", "تحویل شده")]
    [InlineData("Cancelled", "لغو شده")]
    public void FormatOrderStatus_InPersian_TranslatesAllOrderStatuses(string status, string expected)
    {
        var label = OutboxProcessor.FormatOrderStatus(status, isPersian: true);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void FormatOrderStatus_InEnglish_KeepsEnglishStatusName()
    {
        Assert.Equal("Confirmed", OutboxProcessor.FormatOrderStatus("Confirmed", isPersian: false));
    }

    [Theory]
    [InlineData(150000, "IRT", true, "150,000 تومان")]
    [InlineData(150000, "IRR", true, "150,000 ریال")]
    [InlineData(49.5, "USD", true, "49.50 دلار")]
    [InlineData(150000, "IRT", false, "150,000 Toman")]
    [InlineData(49.5, "USD", false, "49.50 USD")]
    public void FormatAmount_FormatsTomanRialAndForeignCurrencies(
        decimal amount,
        string currency,
        bool isPersian,
        string expected)
    {
        var formatted = OutboxProcessor.FormatAmount(amount, currency, isPersian);

        Assert.Equal(expected, formatted);
    }
}
