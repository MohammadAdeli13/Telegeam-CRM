using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>
/// End-to-end coverage of the P1 surface: health, Telegram auth (valid / tampered / expired),
/// the tenant-less token path and merchant onboarding.
/// </summary>
public sealed class ApiEndpointTests : IClassFixture<CrmApiFactory>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly CrmApiFactory _factory;

    public ApiEndpointTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>(_json);
        Assert.NotNull(body);
        Assert.Equal("healthy", body!.Status);
        Assert.Equal("up", body.Database);
    }

    [Fact]
    public async Task LoginWithTelegram_WithValidInitData_IssuesAToken()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: 1001) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthDto>(_json);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal(1001, body.User.TelegramUserId);
        Assert.Empty(body.Tenants);
        Assert.Null(body.ActiveTenant);
    }

    [Fact]
    public async Task LoginWithTelegram_WithTamperedInitData_Returns401WithCode()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(tamperAfterSigning: true) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(_json);
        Assert.Equal("init_data_invalid_hash", problem!.Code);
    }

    [Fact]
    public async Task LoginWithTelegram_WithExpiredInitData_Returns401Expired()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(authDate: DateTimeOffset.UtcNow.AddHours(-25)) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(_json);
        Assert.Equal("init_data_expired", problem!.Code);
    }

    [Fact]
    public async Task Merchants_WithoutAToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/merchants/mine", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMerchant_ThenListMine_ReturnsTheNewShop()
    {
        using var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: 2002) });

        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(_json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var slug = $"shop-{Guid.NewGuid():N}"[..20];
        var created = await client.PostAsJsonAsync(
            new Uri("/api/v1/merchants", UriKind.Relative),
            new { name = "Test Shop", slug, defaultLanguage = "fa", currency = "IRR" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var mine = await client.GetFromJsonAsync<List<MerchantDto>>(
            new Uri("/api/v1/merchants/mine", UriKind.Relative), _json);

        Assert.NotNull(mine);
        Assert.Contains(mine!, m => m.Slug == slug && m.Role == "MerchantOwner");
    }

    [Fact]
    public async Task CreateMerchant_WithAnInvalidSlug_Returns400ValidationProblem()
    {
        using var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: 3003) });

        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(_json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/merchants", UriKind.Relative),
            new { name = "Bad Shop", slug = "INVALID SLUG", defaultLanguage = "fa", currency = "IRR" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record HealthDto(string Status, string Database, DateTimeOffset CheckedAt);

    private sealed record AuthDto(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        AuthUserDto User,
        List<TenantDto> Tenants,
        TenantDto? ActiveTenant);

    private sealed record AuthUserDto(Guid Id, long TelegramUserId, string? FirstName, string? LastName, string? LanguageCode);

    private sealed record TenantDto(Guid Id, string Slug, string Name, string Role);

    private sealed record MerchantDto(Guid Id, string Slug, string Name, string DefaultLanguage, string Currency, bool IsActive, string Role);

    private sealed record ProblemDto(string? Title, int? Status, string? Code);
}
