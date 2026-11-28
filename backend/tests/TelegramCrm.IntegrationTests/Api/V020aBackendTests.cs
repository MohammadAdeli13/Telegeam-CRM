using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>
/// End-to-end coverage for v0.2.0-a backend features: IRT (Toman) currency, shop profile &amp;
/// branding uploads (logo &amp; default product image), TelegramUsername persistence &amp; sync,
/// and ProductDto.PrimaryImageUrl.
/// </summary>
public sealed class V020aBackendTests : IClassFixture<CrmApiFactory>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private static readonly byte[] _pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly CrmApiFactory _factory;

    public V020aBackendTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Auth_PersistsTelegramUsername_AndReturnsTenantCurrencyAndBranding()
    {
        using var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: 95001) });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(_json);
        Assert.NotNull(auth);
        Assert.Equal("ada", auth!.User.Username);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var slug = $"irt-shop-{Guid.NewGuid():N}"[..20];
        var created = await client.PostAsJsonAsync(
            new Uri("/api/v1/merchants", UriKind.Relative),
            new { name = "فروشگاه تومانی", slug, defaultLanguage = "fa", currency = "IRT" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var merchant = await created.Content.ReadFromJsonAsync<MerchantDto>(_json);
        Assert.Equal("IRT", merchant!.Currency);

        var switched = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/switch-tenant", UriKind.Relative),
            new { tenantId = merchant.Id });

        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var scoped = await switched.Content.ReadFromJsonAsync<AuthDto>(_json);
        Assert.Equal("IRT", scoped!.ActiveTenant!.Currency);
        Assert.Equal("fa", scoped.ActiveTenant.DefaultLanguage);
    }

    [Fact]
    public async Task MerchantShop_UpdateProfileAndUploadDeleteBranding_ReflectsOnStorefrontAndIsolatesTenants()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 95002);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 95003);

        // Update profile to IRT.
        var patchResp = await tenantA.Client.PatchAsJsonAsync(
            new Uri("/api/v1/merchant/shop", UriKind.Relative),
            new { name = "گالری برند", defaultLanguage = "fa", currency = "IRT" });

        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
        var updated = await patchResp.Content.ReadFromJsonAsync<MerchantShopDto>(_json);
        Assert.Equal("گالری برند", updated!.Name);
        Assert.Equal("IRT", updated.Currency);

        // Invalid currency -> 400.
        var badCurrency = await tenantA.Client.PatchAsJsonAsync(
            new Uri("/api/v1/merchant/shop", UriKind.Relative),
            new { currency = "XYZ" });
        Assert.Equal(HttpStatusCode.BadRequest, badCurrency.StatusCode);

        // Spoofed logo magic bytes -> 415.
        using (var spoofedForm = CreateImageForm("<html>bad</html>"u8.ToArray(), "image/png", "fake.png"))
        {
            var spoofedResp = await tenantA.Client.PostAsync(
                new Uri("/api/v1/merchant/shop/logo", UriKind.Relative), spoofedForm);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, spoofedResp.StatusCode);
        }

        // Upload valid logo & default product image.
        MerchantShopDto? withLogo;
        using (var logoForm = CreateImageForm(_pngHeader, "image/png", "logo.png"))
        {
            var logoResp = await tenantA.Client.PostAsync(
                new Uri("/api/v1/merchant/shop/logo", UriKind.Relative), logoForm);
            Assert.Equal(HttpStatusCode.OK, logoResp.StatusCode);
            withLogo = await logoResp.Content.ReadFromJsonAsync<MerchantShopDto>(_json);
            Assert.NotNull(withLogo!.LogoUrl);
            Assert.Contains(tenantA.TenantId.ToString(), withLogo.LogoUrl, StringComparison.Ordinal);
        }

        MerchantShopDto? withDefaultImg;
        using (var defaultForm = CreateImageForm(_pngHeader, "image/png", "placeholder.png"))
        {
            var defResp = await tenantA.Client.PostAsync(
                new Uri("/api/v1/merchant/shop/default-product-image", UriKind.Relative), defaultForm);
            Assert.Equal(HttpStatusCode.OK, defResp.StatusCode);
            withDefaultImg = await defResp.Content.ReadFromJsonAsync<MerchantShopDto>(_json);
            Assert.NotNull(withDefaultImg!.DefaultProductImageUrl);
        }

        // Public storefront reflects Tenant A's branding and leaves Tenant B untouched.
        using var anon = _factory.CreateClient();
        var shopA = await anon.GetFromJsonAsync<StorefrontShopDto>(
            new Uri($"/api/v1/storefront/{tenantA.Slug}", UriKind.Relative), _json);
        Assert.Equal(withLogo.LogoUrl, shopA!.LogoUrl);
        Assert.Equal(withDefaultImg.DefaultProductImageUrl, shopA.DefaultProductImageUrl);
        Assert.Equal("IRT", shopA.Currency);

        var shopB = await anon.GetFromJsonAsync<StorefrontShopDto>(
            new Uri($"/api/v1/storefront/{tenantB.Slug}", UriKind.Relative), _json);
        Assert.Null(shopB!.LogoUrl);
        Assert.Null(shopB.DefaultProductImageUrl);

        // Delete logo & default product image.
        var delLogo = await tenantA.Client.DeleteAsync(new Uri("/api/v1/merchant/shop/logo", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, delLogo.StatusCode);
        var afterDelLogo = await delLogo.Content.ReadFromJsonAsync<MerchantShopDto>(_json);
        Assert.Null(afterDelLogo!.LogoUrl);

        var delDef = await tenantA.Client.DeleteAsync(
            new Uri("/api/v1/merchant/shop/default-product-image", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, delDef.StatusCode);
        var afterDelDef = await delDef.Content.ReadFromJsonAsync<MerchantShopDto>(_json);
        Assert.Null(afterDelDef!.DefaultProductImageUrl);
    }

    [Fact]
    public async Task Customers_TelegramUsername_NormalizesSearchesAndSyncsFromStorefrontOrder()
    {
        var merchant = await MerchantTestClient.CreateAsync(_factory, 95004);

        // Manual customer creation normalizes leading '@'.
        var createResp = await merchant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/customers", UriKind.Relative),
            new
            {
                telegramUserId = 960001,
                telegramUsername = "@sara_shop",
                displayName = "Sara",
            });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<CustomerDto>(_json);
        Assert.Equal("sara_shop", created!.TelegramUsername);

        // Search by username.
        var search = await merchant.Client.GetFromJsonAsync<PagedDto<CustomerDto>>(
            new Uri("/api/v1/merchant/customers?q=sara_shop", UriKind.Relative), _json);
        Assert.Single(search!.Items);
        Assert.Equal(created.Id, search.Items[0].Id);

        // Storefront order automatically syncs customer's TelegramUsername from Users ("ada").
        var catResp = await merchant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = "Coffee", slug = "coffee", isActive = true });
        catResp.EnsureSuccessStatusCode();
        var cat = await catResp.Content.ReadFromJsonAsync<IdDto>(_json);

        var prodResp = await merchant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId = cat!.Id, name = "Bean", price = 250000m, stock = 5, isActive = true });
        prodResp.EnsureSuccessStatusCode();
        var prod = await prodResp.Content.ReadFromJsonAsync<ProductDto>(_json);

        // Upload product image & confirm ProductDto.PrimaryImageUrl is populated.
        using (var imgForm = CreateImageForm(_pngHeader, "image/png", "bean.png"))
        {
            var imgResp = await merchant.Client.PostAsync(
                new Uri($"/api/v1/merchant/products/{prod!.Id}/images", UriKind.Relative), imgForm);
            Assert.Equal(HttpStatusCode.Created, imgResp.StatusCode);
        }

        var prodWithImg = await merchant.Client.GetFromJsonAsync<ProductDto>(
            new Uri($"/api/v1/merchant/products/{prod!.Id}", UriKind.Relative), _json);
        Assert.False(string.IsNullOrWhiteSpace(prodWithImg!.PrimaryImageUrl));

        using var customerClient = _factory.CreateClient();
        var custLogin = await customerClient.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: 960002, firstName: "Ada") });
        custLogin.EnsureSuccessStatusCode();
        var custAuth = await custLogin.Content.ReadFromJsonAsync<AuthDto>(_json);
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", custAuth!.AccessToken);

        using var orderReq = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/v1/storefront/{merchant.Slug}/orders", UriKind.Relative))
        {
            Content = JsonContent.Create(new { items = new[] { new { productId = prod.Id, quantity = 1 } } }),
        };
        orderReq.Headers.Add("Idempotency-Key", $"u-{Guid.NewGuid():N}");

        var orderResp = await customerClient.SendAsync(orderReq);
        Assert.Equal(HttpStatusCode.Created, orderResp.StatusCode);

        var customers = await merchant.Client.GetFromJsonAsync<PagedDto<CustomerDto>>(
            new Uri("/api/v1/merchant/customers?q=ada", UriKind.Relative), _json);
        Assert.Contains(customers!.Items, c => c.TelegramUserId == 960002 && c.TelegramUsername == "ada");
    }

    private static MultipartFormDataContent CreateImageForm(byte[] bytes, string contentType, string fileName)
    {
        var mediaType = new MediaTypeHeaderValue(contentType);
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = mediaType;
        content.Add(file, "file", fileName);
        return content;
    }

    private sealed record IdDto(Guid Id);

    private sealed record PagedDto<T>(List<T> Items, int Total, int Page, int PageSize);

    private sealed record AuthDto(string AccessToken, AuthUserDto User, TenantDto? ActiveTenant);

    private sealed record AuthUserDto(Guid Id, long TelegramUserId, string? Username);

    private sealed record TenantDto(
        Guid Id,
        string Slug,
        string Name,
        string Role,
        string DefaultLanguage,
        string Currency,
        string? LogoUrl,
        string? DefaultProductImageUrl);

    private sealed record MerchantDto(Guid Id, string Slug, string Name, string Currency);

    private sealed record MerchantShopDto(
        Guid Id,
        string Slug,
        string Name,
        string DefaultLanguage,
        string Currency,
        string? LogoUrl,
        string? DefaultProductImageUrl);

    private sealed record StorefrontShopDto(
        Guid Id,
        string Slug,
        string Name,
        string Currency,
        string? LogoUrl,
        string? DefaultProductImageUrl);

    private sealed record ProductDto(Guid Id, string Name, string? PrimaryImageUrl);

    private sealed record CustomerDto(Guid Id, long TelegramUserId, string? TelegramUsername, string? DisplayName);
}
