using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>
/// Contract v1.2 §6.4 — cross-tenant access must answer <c>404</c>, never <c>403</c>.
/// These are real HTTP-level tests: two shops are created through the public API, and
/// Tenant A then addresses Tenant B's resources by their real ids.
/// </summary>
public sealed class CrossTenantIsolationTests : IClassFixture<CrmApiFactory>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly CrmApiFactory _factory;

    public CrossTenantIsolationTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantA_ReadingTenantBsCategory_Returns404()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 90001);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 90002);

        var categoryB = await CreateCategoryAsync(tenantB, "b-category");

        var response = await tenantA.Client.GetAsync(
            new Uri($"/api/v1/merchant/categories/{categoryB}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_ModifyingTenantBsCategory_Returns404()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 90003);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 90004);

        var categoryB = await CreateCategoryAsync(tenantB, "b-category");

        var response = await tenantA.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/categories/{categoryB}", UriKind.Relative),
            new { name = "hijacked" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_DeletingTenantBsCategory_Returns404()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 90005);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 90006);

        var categoryB = await CreateCategoryAsync(tenantB, "b-category");

        var response = await tenantA.Client.DeleteAsync(
            new Uri($"/api/v1/merchant/categories/{categoryB}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_ReadingTenantBsProduct_Returns404()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 90007);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 90008);

        var productB = await CreateProductAsync(tenantB, "b-product");

        var response = await tenantA.Client.GetAsync(
            new Uri($"/api/v1/merchant/products/{productB}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PostgresFact]
    public async Task TenantA_AdjustingTenantBsStock_Returns404AndLeavesStockUntouched()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 90009);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 90010);

        var productB = await CreateProductAsync(tenantB, "b-product", stock: 7);

        var response = await tenantA.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/products/{productB}/stock-adjust", UriKind.Relative),
            new { delta = -3, reason = "cross-tenant attempt" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Tenant B's stock is unchanged.
        var product = await tenantB.Client.GetFromJsonAsync<ProductDto>(
            new Uri($"/api/v1/merchant/products/{productB}", UriKind.Relative), _json);

        Assert.Equal(7, product!.Stock);
    }

    [Fact]
    public async Task TenantA_ReadingTenantBsCustomer_Returns404()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 90011);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 90012);

        var customerB = await CreateCustomerAsync(tenantB, 555001);

        var response = await tenantA.Client.GetAsync(
            new Uri($"/api/v1/merchant/customers/{customerB}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_ListingItsOwnCategories_DoesNotSeeTenantBs()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 90013);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 90014);

        await CreateCategoryAsync(tenantA, "a-only");
        await CreateCategoryAsync(tenantB, "b-only");

        var page = await tenantA.Client.GetFromJsonAsync<PagedDto<CategoryDto>>(
            new Uri("/api/v1/merchant/categories", UriKind.Relative), _json);

        Assert.NotNull(page);
        Assert.All(page!.Items, c => Assert.Equal("a-only", c.Slug));
    }

    [Fact]
    public async Task MerchantRoute_WithoutATenantScopedToken_Returns403()
    {
        // A token with no tenant_id cannot satisfy the `Merchant` policy: that is a role
        // mismatch (403), not a cross-tenant read.
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: 90015) });

        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(_json);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync(new Uri("/api/v1/merchant/categories", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<Guid> CreateCategoryAsync(MerchantTestClient tenant, string slug)
    {
        var response = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = slug, slug, sortOrder = 0, isActive = true });

        response.EnsureSuccessStatusCode();
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>(_json);
        return category!.Id;
    }

    private static async Task<Guid> CreateProductAsync(MerchantTestClient tenant, string name, int stock = 5)
    {
        var categoryId = await CreateCategoryAsync(tenant, $"{name}-cat");

        var response = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId, name, price = 12.50m, stock, isActive = true });

        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(_json);
        return product!.Id;
    }

    private static async Task<Guid> CreateCustomerAsync(MerchantTestClient tenant, long telegramUserId)
    {
        var response = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/customers", UriKind.Relative),
            new { telegramUserId, displayName = "Cross Tenant Customer" });

        response.EnsureSuccessStatusCode();
        var customer = await response.Content.ReadFromJsonAsync<CustomerDto>(_json);
        return customer!.Id;
    }

    private sealed record AuthDto(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record PagedDto<T>(List<T> Items, int Total, int Page, int PageSize);

    private sealed record CategoryDto(Guid Id, string Name, string Slug);

    private sealed record ProductDto(Guid Id, string Name, int Stock);

    private sealed record CustomerDto(Guid Id, long TelegramUserId, string? DisplayName);
}
