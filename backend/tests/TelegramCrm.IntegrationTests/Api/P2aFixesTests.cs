using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>Regression tests closing all 3 P2a QA findings on real PostgreSQL 16.</summary>
public sealed class P2aFixesTests : IClassFixture<CrmApiFactory>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly CrmApiFactory _factory;

    public P2aFixesTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task Products_SearchByQuery_MatchesNameAndNameEnCaseInsensitivelyOnPostgres()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 85001);

        var catResp = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = "Tech", slug = "tech" });
        catResp.EnsureSuccessStatusCode();
        var cat = await catResp.Content.ReadFromJsonAsync<IdDto>(_json);

        var p1 = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId = cat!.Id, name = "گوشی سامسونگ", nameEn = "Galaxy Ultra", price = 500m, stock = 5 });
        p1.EnsureSuccessStatusCode();

        var p2 = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId = cat.Id, name = "کابل شارژ", nameEn = "USB Cable", price = 10m, stock = 10 });
        p2.EnsureSuccessStatusCode();

        var byEn = await tenant.Client.GetFromJsonAsync<PagedDto<ProductDto>>(
            new Uri("/api/v1/merchant/products?q=gAlAxY", UriKind.Relative), _json);

        Assert.Single(byEn!.Items);
        Assert.Equal("Galaxy Ultra", byEn.Items[0].NameEn);

        var byFa = await tenant.Client.GetFromJsonAsync<PagedDto<ProductDto>>(
            new Uri("/api/v1/merchant/products?q=کابل", UriKind.Relative), _json);

        Assert.Single(byFa!.Items);
        Assert.Equal("USB Cable", byFa.Items[0].NameEn);
    }

    [PostgresFact]
    public async Task Customers_SearchByQuery_MatchesDisplayNameAndPhoneOnPostgres()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 85002);

        var c1 = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/customers", UriKind.Relative),
            new { telegramUserId = 850101, displayName = "Sara Karimi", phone = "+989121112233" });
        c1.EnsureSuccessStatusCode();

        var c2 = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/customers", UriKind.Relative),
            new { telegramUserId = 850102, displayName = "Ali Rezaei", phone = "+989359998877" });
        c2.EnsureSuccessStatusCode();

        var byName = await tenant.Client.GetFromJsonAsync<PagedDto<CustomerDto>>(
            new Uri("/api/v1/merchant/customers?q=sArA", UriKind.Relative), _json);
        Assert.Single(byName!.Items);
        Assert.Equal(850101, byName.Items[0].TelegramUserId);

        var byPhone = await tenant.Client.GetFromJsonAsync<PagedDto<CustomerDto>>(
            new Uri("/api/v1/merchant/customers?q=9998877", UriKind.Relative), _json);
        Assert.Single(byPhone!.Items);
        Assert.Equal(850102, byPhone.Items[0].TelegramUserId);
    }

    [Fact]
    public async Task Settings_WithInvalidJsonValue_Returns400ValidationProblem()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 85003);

        var response = await tenant.Client.PutAsJsonAsync(
            new Uri("/api/v1/merchant/settings", UriKind.Relative),
            new
            {
                settings = new[]
                {
                    new { key = "broken", value = "{not-valid-json" },
                },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProductImages_SpoofedPngMagicBytes_Returns415()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 85004);

        var catResp = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = "Media", slug = "media" });
        catResp.EnsureSuccessStatusCode();
        var cat = await catResp.Content.ReadFromJsonAsync<IdDto>(_json);

        var prodResp = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId = cat!.Id, name = "Item", price = 10m, stock = 1 });
        prodResp.EnsureSuccessStatusCode();
        var prod = await prodResp.Content.ReadFromJsonAsync<IdDto>(_json);

        using var content = new MultipartFormDataContent();
        var spoofed = new ByteArrayContent("<html><script>alert(1)</script></html>"u8.ToArray());
        spoofed.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(spoofed, "file", "fake.png");

        var response = await tenant.Client.PostAsync(
            new Uri($"/api/v1/merchant/products/{prod!.Id}/images", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private sealed record IdDto(Guid Id);

    private sealed record PagedDto<T>(List<T> Items, int Total, int Page, int PageSize);

    private sealed record ProductDto(Guid Id, string Name, string? NameEn);

    private sealed record CustomerDto(Guid Id, long TelegramUserId, string? DisplayName);
}
