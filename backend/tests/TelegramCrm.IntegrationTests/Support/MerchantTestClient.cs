using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TelegramCrm.IntegrationTests.Support;

/// <summary>
/// Drives the real HTTP surface the way the Mini App does: bootstrap with a signed
/// <c>initData</c>, create a shop, then call the merchant back-office with the issued token.
/// </summary>
public sealed class MerchantTestClient
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    private MerchantTestClient(HttpClient client, Guid tenantId, string slug, string accessToken)
    {
        _client = client;
        TenantId = tenantId;
        Slug = slug;
        AccessToken = accessToken;
    }

    public Guid TenantId { get; }

    public string Slug { get; }

    public string AccessToken { get; }

    public HttpClient Client => _client;

    /// <summary>
    /// Registers a brand-new Telegram user, creates a shop for them and returns a client
    /// whose default request carries that shop's token.
    /// </summary>
    public static async Task<MerchantTestClient> CreateAsync(CrmApiFactory factory, long telegramUserId)
    {
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: telegramUserId) });

        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(_json);
        var token = auth!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slug = $"shop-{Guid.NewGuid():N}"[..20];
        var created = await client.PostAsJsonAsync(
            new Uri("/api/v1/merchants", UriKind.Relative),
            new { name = $"Shop {slug}", slug, defaultLanguage = "fa", currency = "IRR" });

        created.EnsureSuccessStatusCode();
        var merchant = await created.Content.ReadFromJsonAsync<MerchantDto>(_json);

        // Creating a shop makes the caller its owner, but the token issued at login had no
        // active tenant. Switch to the new shop so the `Merchant` policy is satisfied.
        var switched = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/switch-tenant", UriKind.Relative),
            new { tenantId = merchant!.Id });

        switched.EnsureSuccessStatusCode();
        var scoped = await switched.Content.ReadFromJsonAsync<AuthDto>(_json);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scoped!.AccessToken);

        return new MerchantTestClient(client, merchant.Id, slug, scoped.AccessToken);
    }

    private sealed record AuthDto(string AccessToken, DateTimeOffset ExpiresAt, TenantDto? ActiveTenant);

    private sealed record TenantDto(Guid Id, string Slug, string Name, string Role);

    private sealed record MerchantDto(Guid Id, string Slug, string Name);
}
