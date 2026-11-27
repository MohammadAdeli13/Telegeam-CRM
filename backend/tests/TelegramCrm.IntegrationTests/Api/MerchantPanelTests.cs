using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>
/// End-to-end coverage of the P2a merchant back-office: categories, products, atomic stock
/// adjustment, images, customers, tags and settings — all through real HTTP.
/// </summary>
public sealed class MerchantPanelTests : IClassFixture<CrmApiFactory>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly CrmApiFactory _factory;

    public MerchantPanelTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Categories_CreateListUpdateDelete_RoundTrips()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80001);

        var created = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = "Shoes", nameEn = "Shoes", slug = "shoes", sortOrder = 1, isActive = true });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = await created.Content.ReadFromJsonAsync<CategoryDto>(_json);
        Assert.NotNull(category);
        Assert.Equal("shoes", category!.Slug);

        var list = await tenant.Client.GetFromJsonAsync<PagedDto<CategoryDto>>(
            new Uri("/api/v1/merchant/categories?page=1&pageSize=10", UriKind.Relative), _json);
        Assert.Contains(list!.Items, c => c.Id == category.Id);

        var patched = await tenant.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/categories/{category.Id}", UriKind.Relative),
            new { name = "Footwear" });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var updated = await patched.Content.ReadFromJsonAsync<CategoryDto>(_json);
        Assert.Equal("Footwear", updated!.Name);

        var deleted = await tenant.Client.DeleteAsync(
            new Uri($"/api/v1/merchant/categories/{category.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Categories_DuplicateSlug_Returns409()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80002);

        var first = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = "Bags", slug = "bags" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = "Bags 2", slug = "bags" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDto>(_json);
        Assert.Equal("slug_taken", problem!.Code);
    }

    [Fact]
    public async Task Categories_InvalidPayload_Returns400ValidationProblem()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80003);

        var response = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = "", slug = "INVALID SLUG" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Products_CreateThenList_FiltersByCategory()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80004);
        var categoryId = await CreateCategoryAsync(tenant, "electronics");

        var created = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId, name = "Phone", price = 199.99m, stock = 10, isActive = true });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = await created.Content.ReadFromJsonAsync<ProductDto>(_json);
        Assert.Equal(199.99m, product!.Price);

        var list = await tenant.Client.GetFromJsonAsync<PagedDto<ProductDto>>(
            new Uri($"/api/v1/merchant/products?categoryId={categoryId}&page=1&pageSize=5", UriKind.Relative),
            _json);

        Assert.Single(list!.Items);
        Assert.Equal("Phone", list.Items[0].Name);
    }

    [Fact]
    public async Task Products_WithUnknownCategory_Returns422()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80005);

        var response = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId = Guid.NewGuid(), name = "Orphan", price = 1m, stock = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(_json);
        Assert.Equal("category_not_found", problem!.Code);
    }

    [PostgresFact]
    public async Task StockAdjust_PositiveAndNegative_UpdatesStock()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80006);
        var productId = await CreateProductAsync(tenant, "Widget", stock: 10);

        var add = await tenant.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/products/{productId}/stock-adjust", UriKind.Relative),
            new { delta = 5, reason = "restock" });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var afterAdd = await add.Content.ReadFromJsonAsync<StockAdjustDto>(_json);
        Assert.Equal(15, afterAdd!.Stock);

        var remove = await tenant.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/products/{productId}/stock-adjust", UriKind.Relative),
            new { delta = -4, reason = "damaged" });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        var afterRemove = await remove.Content.ReadFromJsonAsync<StockAdjustDto>(_json);
        Assert.Equal(11, afterRemove!.Stock);
    }

    [PostgresFact]
    public async Task StockAdjust_ThatWouldGoNegative_Returns409InsufficientStock()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80007);
        var productId = await CreateProductAsync(tenant, "Scarce", stock: 2);

        var response = await tenant.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/products/{productId}/stock-adjust", UriKind.Relative),
            new { delta = -5, reason = "oversell" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(_json);
        Assert.Equal("insufficient_stock", problem!.Code);

        // The stock is untouched.
        var product = await tenant.Client.GetFromJsonAsync<ProductDto>(
            new Uri($"/api/v1/merchant/products/{productId}", UriKind.Relative), _json);
        Assert.Equal(2, product!.Stock);
    }

    [Fact]
    public async Task StockAdjust_WithZeroDelta_Returns400()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80008);
        var productId = await CreateProductAsync(tenant, "Zero", stock: 3);

        var response = await tenant.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/products/{productId}/stock-adjust", UriKind.Relative),
            new { delta = 0, reason = "noop" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task ProductImages_UploadThenSetPrimary_SwapsAtomically()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80009);
        var productId = await CreateProductAsync(tenant, "WithImages", stock: 1);

        var first = await UploadImageAsync(tenant, productId, "first.png");
        var second = await UploadImageAsync(tenant, productId, "second.png");

        // The first upload becomes primary automatically.
        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);

        var promoted = await tenant.Client.PostAsync(
            new Uri($"/api/v1/merchant/products/{productId}/images/{second.Id}/primary", UriKind.Relative),
            content: null);

        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        var promotedImage = await promoted.Content.ReadFromJsonAsync<ProductImageDto>(_json);
        Assert.True(promotedImage!.IsPrimary);

        var images = await tenant.Client.GetFromJsonAsync<List<ProductImageDto>>(
            new Uri($"/api/v1/merchant/products/{productId}/images", UriKind.Relative), _json);

        Assert.Single(images!, i => i.IsPrimary);
        Assert.Equal(second.Id, images!.Single(i => i.IsPrimary).Id);
    }

    [Fact]
    public async Task ProductImages_UnsupportedContentType_Returns415()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80010);
        var productId = await CreateProductAsync(tenant, "BadUpload", stock: 1);

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "document.pdf");

        var response = await tenant.Client.PostAsync(
            new Uri($"/api/v1/merchant/products/{productId}/images", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Customers_CreateAttachTagAndListOrders()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80011);

        var customerResponse = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/customers", UriKind.Relative),
            new { telegramUserId = 700001, displayName = "Nima", phone = "+989121234567" });

        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>(_json);

        var tagResponse = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/tags", UriKind.Relative),
            new { name = "vip" });
        Assert.Equal(HttpStatusCode.Created, tagResponse.StatusCode);
        var tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>(_json);

        var attached = await tenant.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/customers/{customer!.Id}/tags", UriKind.Relative),
            new { tagId = tag!.Id });
        Assert.Equal(HttpStatusCode.OK, attached.StatusCode);
        var withTag = await attached.Content.ReadFromJsonAsync<CustomerDto>(_json);
        Assert.Contains(withTag!.Tags, t => t.Id == tag.Id);

        var orders = await tenant.Client.GetFromJsonAsync<PagedDto<OrderDto>>(
            new Uri($"/api/v1/merchant/customers/{customer.Id}/orders", UriKind.Relative), _json);
        Assert.Empty(orders!.Items);

        var detached = await tenant.Client.DeleteAsync(
            new Uri($"/api/v1/merchant/customers/{customer.Id}/tags/{tag.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, detached.StatusCode);
        var withoutTag = await detached.Content.ReadFromJsonAsync<CustomerDto>(_json);
        Assert.DoesNotContain(withoutTag!.Tags, t => t.Id == tag.Id);
    }

    [Fact]
    public async Task Customers_DuplicateTelegramUserId_Returns409()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80012);

        var first = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/customers", UriKind.Relative),
            new { telegramUserId = 700002, displayName = "First" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/customers", UriKind.Relative),
            new { telegramUserId = 700002, displayName = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [PostgresFact]
    public async Task Settings_ReplaceThenRead_RoundTrips()
    {
        var tenant = await MerchantTestClient.CreateAsync(_factory, 80013);

        var put = await tenant.Client.PutAsJsonAsync(
            new Uri("/api/v1/merchant/settings", UriKind.Relative),
            new
            {
                settings = new[]
                {
                    new { key = "theme", value = "\"dark\"" },
                    new { key = "currency", value = "\"IRR\"" },
                },
            });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var read = await tenant.Client.GetFromJsonAsync<List<SettingDto>>(
            new Uri("/api/v1/merchant/settings", UriKind.Relative), _json);

        Assert.Equal(2, read!.Count);
        Assert.Contains(read, s => s.Key == "theme");

        // A second PUT replaces the whole set.
        var replace = await tenant.Client.PutAsJsonAsync(
            new Uri("/api/v1/merchant/settings", UriKind.Relative),
            new { settings = new[] { new { key = "theme", value = "\"light\"" } } });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);

        var afterReplace = await tenant.Client.GetFromJsonAsync<List<SettingDto>>(
            new Uri("/api/v1/merchant/settings", UriKind.Relative), _json);

        Assert.Single(afterReplace!);
        Assert.Equal("\"light\"", afterReplace![0].Value);
    }

    [Fact]
    public async Task MerchantEndpoints_WithoutAToken_Return401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/merchant/products", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<Guid> CreateCategoryAsync(MerchantTestClient tenant, string slug)
    {
        var response = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = slug, slug });

        response.EnsureSuccessStatusCode();
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>(_json);
        return category!.Id;
    }

    private static async Task<Guid> CreateProductAsync(MerchantTestClient tenant, string name, int stock)
    {
        var categoryId = await CreateCategoryAsync(tenant, $"{name.ToLowerInvariant()}-cat");

        var response = await tenant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId, name, price = 9.99m, stock, isActive = true });

        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(_json);
        return product!.Id;
    }

    private static async Task<ProductImageDto> UploadImageAsync(
        MerchantTestClient tenant,
        Guid productId,
        string fileName)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", fileName);

        var response = await tenant.Client.PostAsync(
            new Uri($"/api/v1/merchant/products/{productId}/images", UriKind.Relative), content);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductImageDto>(_json))!;
    }

    private sealed record PagedDto<T>(List<T> Items, int Total, int Page, int PageSize);

    private sealed record CategoryDto(Guid Id, string Name, string Slug, int SortOrder, bool IsActive);

    private sealed record ProductDto(Guid Id, string Name, decimal Price, int Stock);

    private sealed record StockAdjustDto(Guid ProductId, int Stock, int Delta);

    private sealed record ProductImageDto(Guid Id, Guid ProductId, string StoragePath, string Url, bool IsPrimary);

    private sealed record CustomerDto(Guid Id, long TelegramUserId, string? DisplayName, List<TagDto> Tags);

    private sealed record TagDto(Guid Id, string Name);

    private sealed record OrderDto(Guid Id, string Status, decimal TotalAmount);

    private sealed record SettingDto(string Key, string Value);

    private sealed record ProblemDto(string? Title, int? Status, string? Code);
}
