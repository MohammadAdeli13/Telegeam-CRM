using Microsoft.Extensions.Options;
using TelegramCrm.Application.Auth;
using TelegramCrm.Application.Common;
using TelegramCrm.UnitTests.Support;

namespace TelegramCrm.UnitTests.Auth;

/// <summary>
/// Contract v1.2 §5.1 — the HMAC-SHA256 validation of Telegram WebApp <c>initData</c>.
/// Covers the valid path, a tampered payload, a wrong bot token and an expired
/// <c>auth_date</c> against the configurable replay window.
/// </summary>
public sealed class TelegramInitDataValidatorTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private static TelegramInitDataValidator CreateValidator(int maxAgeSeconds = 86400) =>
        new(new FixedClock(_now), Options.Create(new TelegramAuthOptions
        {
            InitDataMaxAgeSeconds = maxAgeSeconds,
        }));

    [Fact]
    public void Validate_WithCorrectlySignedInitData_ReturnsUser()
    {
        var initData = InitDataBuilder.Build(telegramUserId: 777, authDate: _now.AddMinutes(-5));

        var result = CreateValidator().Validate(initData, InitDataBuilder.BotToken);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.User);
        Assert.Equal(777, result.User!.Id);
        Assert.Equal("Ada", result.User.FirstName);
    }

    [Fact]
    public void Validate_WhenPayloadIsTamperedAfterSigning_RejectsWithInvalidHash()
    {
        var initData = InitDataBuilder.Build(authDate: _now.AddMinutes(-5), tamperAfterSigning: true);

        var result = CreateValidator().Validate(initData, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_invalid_hash", result.ErrorCode);
        Assert.Null(result.User);
    }

    [Fact]
    public void Validate_WhenHashIsReplaced_RejectsWithInvalidHash()
    {
        var initData = InitDataBuilder.Build(
            authDate: _now.AddMinutes(-5),
            overrideHash: new string('a', 64));

        var result = CreateValidator().Validate(initData, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_invalid_hash", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenSignedWithAnotherBotToken_RejectsWithInvalidHash()
    {
        var initData = InitDataBuilder.Build(
            authDate: _now.AddMinutes(-5),
            botToken: "999999999:AAHsome-other-bot-token-value");

        var result = CreateValidator().Validate(initData, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_invalid_hash", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenAuthDateIsOlderThanTheWindow_RejectsAsExpired()
    {
        var initData = InitDataBuilder.Build(authDate: _now.AddHours(-25));

        var result = CreateValidator(maxAgeSeconds: 86400).Validate(initData, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_expired", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenAuthDateIsInsideTheWindow_Accepts()
    {
        var initData = InitDataBuilder.Build(authDate: _now.AddHours(-23));

        var result = CreateValidator(maxAgeSeconds: 86400).Validate(initData, InitDataBuilder.BotToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenWindowIsLowered_RejectsAnOtherwiseFreshPayload()
    {
        var initData = InitDataBuilder.Build(authDate: _now.AddMinutes(-10));

        var result = CreateValidator(maxAgeSeconds: 300).Validate(initData, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_expired", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenAuthDateIsFarInTheFuture_RejectsAsFutureAuthDate()
    {
        // P1 QA finding: a payload signed in the future must not be accepted, otherwise it
        // would remain valid for the whole replay window from a point that has not happened.
        var initData = InitDataBuilder.Build(authDate: _now.AddMinutes(10));

        var result = CreateValidator().Validate(initData, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_auth_date_in_future", result.ErrorCode);
        Assert.Null(result.User);
    }

    [Fact]
    public void Validate_WhenAuthDateIsJustBeyondTheSkewTolerance_RejectsAsFutureAuthDate()
    {
        var initData = InitDataBuilder.Build(authDate: _now.AddSeconds(61));

        var result = CreateValidator().Validate(initData, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_auth_date_in_future", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenAuthDateIsWithinTheSkewTolerance_Accepts()
    {
        // 30 s ahead is ordinary clock skew between Telegram and the API.
        var initData = InitDataBuilder.Build(authDate: _now.AddSeconds(30));

        var result = CreateValidator().Validate(initData, InitDataBuilder.BotToken);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenInitDataIsEmpty_Rejects()
    {
        var result = CreateValidator().Validate(string.Empty, InitDataBuilder.BotToken);

        Assert.False(result.IsValid);
        Assert.Equal("init_data_missing", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenBotTokenIsMissing_Rejects()
    {
        var initData = InitDataBuilder.Build(authDate: _now);

        var result = CreateValidator().Validate(initData, botToken: null);

        Assert.False(result.IsValid);
        Assert.Equal("bot_token_not_configured", result.ErrorCode);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; }
    }
}
