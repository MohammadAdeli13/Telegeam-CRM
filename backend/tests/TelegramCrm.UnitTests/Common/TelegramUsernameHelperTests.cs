using TelegramCrm.Application.Common;

namespace TelegramCrm.UnitTests.Common;

public sealed class TelegramUsernameHelperTests
{
    [Theory]
    [InlineData("@sara_shop", "sara_shop")]
    [InlineData("  @@NoorGallery  ", "NoorGallery")]
    [InlineData("ali123", "ali123")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("@@@", null)]
    [InlineData(null, null)]
    public void Normalize_StripsLeadingAtAndWhitespace(string? input, string? expected)
    {
        Assert.Equal(expected, TelegramUsernameHelper.Normalize(input));
    }

    [Theory]
    [InlineData("@sara_shop", true)]
    [InlineData("noor_123", true)]
    [InlineData("abcd", true)]
    [InlineData("123invalid", false)]
    [InlineData("ab", false)]
    [InlineData("bad name", false)]
    [InlineData("bad-dash", false)]
    public void IsValid_MatchesTelegramUsernameRules(string input, bool expected)
    {
        Assert.Equal(expected, TelegramUsernameHelper.IsValid(input));
    }
}
