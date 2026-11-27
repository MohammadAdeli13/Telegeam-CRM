using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TelegramCrm.Api.Endpoints;
using TelegramCrm.Infrastructure.Telegram;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>
/// End-to-end tests for P2b: storefront reads, cart validation, idempotent order placement,
/// concurrent stock oversell prevention, merchant/customer order lifecycle and stock restore,
/// cross-tenant order isolation, Telegram webhook secret check, and OutboxProcessor delivery.
/// </summary>
public sealed class StorefrontAndOrdersTests : IClassFixture<CrmApiFactory>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly CrmApiFactory _factory;

    public StorefrontAndOrdersTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Storefront_UnknownShopSlug_Returns404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            new Uri("/api/v1/storefront/non-existent-shop-xyz", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PostgresFact]
    public async Task Storefront_PublicCatalog_AndCartValidation_Work()
    {
        var merchant = await MerchantTestClient.CreateAsync(_factory, 91001);
        var productId = await CreateProductAsync(merchant, "Espresso", price: 4.50m, stock: 3);

        using var anon = _factory.CreateClient();

        var shop = await anon.GetFromJsonAsync<ShopDto>(
            new Uri($"/api/v1/storefront/{merchant.Slug}", UriKind.Relative), _json);
        Assert.Equal(merchant.Slug, shop!.Slug);

        var products = await anon.GetFromJsonAsync<PagedDto<StorefrontProductDto>>(
            new Uri($"/api/v1/storefront/{merchant.Slug}/products?q=espresso", UriKind.Relative), _json);
        Assert.Single(products!.Items);
        Assert.Equal(productId, products.Items[0].Id);

        using var customer = await CreateCustomerClientAsync(92001);

        var okCart = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/cart/validate", UriKind.Relative),
            new { items = new[] { new { productId, quantity = 2 } } });
        Assert.Equal(HttpStatusCode.OK, okCart.StatusCode);
        var okBody = await okCart.Content.ReadFromJsonAsync<CartValidationDto>(_json);
        Assert.True(okBody!.IsValid);
        Assert.Equal(9.00m, okBody.TotalAmount);

        var badCart = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/cart/validate", UriKind.Relative),
            new { items = new[] { new { productId, quantity = 10 } } });
        var badBody = await badCart.Content.ReadFromJsonAsync<CartValidationDto>(_json);
        Assert.False(badBody!.IsValid);
        Assert.Equal("insufficient_stock", badBody.Items[0].Issue);
    }

    [PostgresFact]
    public async Task PlaceOrder_RequiresIdempotencyKey_AndReplaysWithoutDoubleDecrement()
    {
        var merchant = await MerchantTestClient.CreateAsync(_factory, 91002);
        var productId = await CreateProductAsync(merchant, "Latte", price: 5.00m, stock: 10);
        using var customer = await CreateCustomerClientAsync(92002);

        // Missing Idempotency-Key -> 400.
        var missingKey = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/orders", UriKind.Relative),
            new { items = new[] { new { productId, quantity = 2 } } });
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);

        // First call -> 201 Created, stock drops 10 -> 8.
        var key = $"idem-{Guid.NewGuid():N}";
        using var req1 = CreateOrderRequest(merchant.Slug, key, productId, quantity: 2);
        var resp1 = await customer.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);
        var order1 = await resp1.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal("Pending", order1!.Status);
        Assert.Equal(10.00m, order1.TotalAmount);
        Assert.Single(order1.StatusHistory);

        // Replay with exact same Idempotency-Key -> 200 OK, same Id, stock stays 8.
        using var req2 = CreateOrderRequest(merchant.Slug, key, productId, quantity: 2);
        var resp2 = await customer.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var order2 = await resp2.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal(order1.Id, order2!.Id);

        var stock = await GetMerchantProductStockAsync(merchant, productId);
        Assert.Equal(8, stock);
    }

    [PostgresFact]
    public async Task PlaceOrder_ConcurrentOversell_AllowsOnlyOneAndNeverGoesNegative()
    {
        var merchant = await MerchantTestClient.CreateAsync(_factory, 91003);
        var productId = await CreateProductAsync(merchant, "Limited Watch", price: 100m, stock: 4);

        using var customer1 = await CreateCustomerClientAsync(92003);
        using var customer2 = await CreateCustomerClientAsync(92004);

        using var req1 = CreateOrderRequest(merchant.Slug, $"c1-{Guid.NewGuid():N}", productId, quantity: 3);
        using var req2 = CreateOrderRequest(merchant.Slug, $"c2-{Guid.NewGuid():N}", productId, quantity: 3);

        var responses = await Task.WhenAll(customer1.SendAsync(req1), customer2.SendAsync(req2));
        var codes = responses.Select(r => r.StatusCode).OrderBy(c => c).ToList();

        Assert.Equal(2, codes.Count);
        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.Conflict, codes);

        var finalStock = await GetMerchantProductStockAsync(merchant, productId);
        Assert.Equal(1, finalStock);
    }

    [PostgresFact]
    public async Task Orders_CustomerCancelPending_RestoresStock_AndCannotCancelConfirmed()
    {
        var merchant = await MerchantTestClient.CreateAsync(_factory, 91004);
        var productId = await CreateProductAsync(merchant, "Backpack", price: 30m, stock: 5);
        using var customer = await CreateCustomerClientAsync(92005);

        // Order 1: place 2 (stock 5 -> 3), then customer cancels Pending -> stock 3 -> 5.
        using var req1 = CreateOrderRequest(merchant.Slug, $"cancel-{Guid.NewGuid():N}", productId, quantity: 2);
        var placed1 = await customer.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, placed1.StatusCode);
        var order1 = await placed1.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal(3, await GetMerchantProductStockAsync(merchant, productId));

        var cancelResp = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/orders/{order1!.Id}/cancel", UriKind.Relative),
            new { reason = "Changed mind" });
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);
        var cancelled = await cancelResp.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal(5, await GetMerchantProductStockAsync(merchant, productId));

        // Second cancel attempt on terminal Cancelled -> 409, stock stays 5 (no double restore).
        var secondCancel = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/orders/{order1.Id}/cancel", UriKind.Relative),
            new { reason = "Retry" });
        Assert.Equal(HttpStatusCode.Conflict, secondCancel.StatusCode);
        Assert.Equal(5, await GetMerchantProductStockAsync(merchant, productId));

        // Order 2: merchant confirms it, then customer tries to cancel -> 409.
        using var req2 = CreateOrderRequest(merchant.Slug, $"confirm-{Guid.NewGuid():N}", productId, quantity: 1);
        var placed2 = await customer.SendAsync(req2);
        var order2 = await placed2.Content.ReadFromJsonAsync<OrderDetailDto>(_json);

        var confirmResp = await merchant.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{order2!.Id}/status", UriKind.Relative),
            new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        var illegalCustomerCancel = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/orders/{order2.Id}/cancel", UriKind.Relative),
            new { reason = "Too late" });
        Assert.Equal(HttpStatusCode.Conflict, illegalCustomerCancel.StatusCode);

        // Merchant cancels Confirmed -> restores stock (4 -> 5).
        var merchantCancel = await merchant.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{order2.Id}/status", UriKind.Relative),
            new { status = "Cancelled", reason = "Out of courier" });
        Assert.Equal(HttpStatusCode.OK, merchantCancel.StatusCode);
        Assert.Equal(5, await GetMerchantProductStockAsync(merchant, productId));
    }

    [PostgresFact]
    public async Task Orders_CrossTenantAndCrossCustomerAccess_Returns404()
    {
        var merchantA = await MerchantTestClient.CreateAsync(_factory, 91005);
        var merchantB = await MerchantTestClient.CreateAsync(_factory, 91006);
        var productB = await CreateProductAsync(merchantB, "Secret Lamp", price: 20m, stock: 5);

        using var customer1 = await CreateCustomerClientAsync(92006);
        using var customer2 = await CreateCustomerClientAsync(92007);

        using var req = CreateOrderRequest(merchantB.Slug, $"iso-{Guid.NewGuid():N}", productB, quantity: 1);
        var placed = await customer1.SendAsync(req);
        var orderB = await placed.Content.ReadFromJsonAsync<OrderDetailDto>(_json);

        // Merchant A cannot read or update Merchant B's order -> 404.
        var readByMerchantA = await merchantA.Client.GetAsync(
            new Uri($"/api/v1/merchant/orders/{orderB!.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, readByMerchantA.StatusCode);

        var patchByMerchantA = await merchantA.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{orderB.Id}/status", UriKind.Relative),
            new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.NotFound, patchByMerchantA.StatusCode);

        // Customer 2 cannot read Customer 1's order -> 404.
        var readByCustomer2 = await customer2.GetAsync(
            new Uri($"/api/v1/storefront/{merchantB.Slug}/orders/{orderB.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, readByCustomer2.StatusCode);
    }

    [PostgresFact]
    public async Task TelegramWebhook_AndOutboxProcessor_ValidateSecretAndDeliverNotifications()
    {
        _factory.TelegramSender.Clear();
        var merchant = await MerchantTestClient.CreateAsync(_factory, 91007);
        var productId = await CreateProductAsync(merchant, "Tea Box", price: 15m, stock: 5);
        using var customer = await CreateCustomerClientAsync(92008);
        using var client = _factory.CreateClient();

        // Wrong webhook secret -> 401.
        using var badWebhook = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/api/v1/telegram/webhook", UriKind.Relative))
        {
            Content = JsonContent.Create(new { update_id = 1 }),
        };
        badWebhook.Headers.Add(TelegramWebhookEndpoints.SecretTokenHeader, "wrong-secret");
        var badResp = await client.SendAsync(badWebhook);
        Assert.Equal(HttpStatusCode.Unauthorized, badResp.StatusCode);

        // Valid /start shop_{slug} -> 200 and queues bot.reply.
        using var goodWebhook = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/api/v1/telegram/webhook", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                update_id = 2,
                message = new
                {
                    message_id = 10,
                    chat = new { id = 92008L, type = "private" },
                    from = new { id = 92008L, first_name = "Mina", language_code = "fa" },
                    text = $"/start shop_{merchant.Slug}",
                },
            }),
        };
        goodWebhook.Headers.Add(TelegramWebhookEndpoints.SecretTokenHeader, CrmApiFactory.WebhookSecret);
        var goodResp = await client.SendAsync(goodWebhook);
        Assert.Equal(HttpStatusCode.OK, goodResp.StatusCode);

        // Place order (queues order.placed to merchant 91007) + confirm (queues order.status_changed to customer 92008).
        using var orderReq = CreateOrderRequest(merchant.Slug, $"bot-{Guid.NewGuid():N}", productId, quantity: 1);
        var orderResp = await customer.SendAsync(orderReq);
        var order = await orderResp.Content.ReadFromJsonAsync<OrderDetailDto>(_json);

        var confirmResp = await merchant.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{order!.Id}/status", UriKind.Relative),
            new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        // Run OutboxProcessor deterministically.
        var processor = _factory.Services.GetRequiredService<OutboxProcessor>();
        var delivered = await processor.ProcessBatchAsync();
        Assert.True(delivered >= 3);

        var sent = _factory.TelegramSender.Messages;
        Assert.Contains(
            sent,
            m => m.ChatId == 92008 &&
                 m.WebAppUrl is not null &&
                 m.WebAppUrl.EndsWith($"/s/{merchant.Slug}", StringComparison.Ordinal));
        Assert.Contains(sent, m => m.ChatId == 91007);
        Assert.Contains(sent, m => m.ChatId == 92008 && m.Text.Contains("Confirmed", StringComparison.Ordinal));
    }

    private async Task<HttpClient> CreateCustomerClientAsync(long telegramUserId)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/telegram", UriKind.Relative),
            new { initData = InitDataBuilder.Build(telegramUserId: telegramUserId) });

        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(_json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static HttpRequestMessage CreateOrderRequest(
        string shopSlug,
        string idempotencyKey,
        Guid productId,
        int quantity)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/v1/storefront/{shopSlug}/orders", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                items = new[] { new { productId, quantity } },
                note = "Deliver in evening",
            }),
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return req;
    }

    private static async Task<Guid> CreateProductAsync(
        MerchantTestClient merchant,
        string name,
        decimal price,
        int stock)
    {
        var catSlug = $"cat-{Guid.NewGuid():N}"[..16];
        var catResp = await merchant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/categories", UriKind.Relative),
            new { name = catSlug, slug = catSlug, isActive = true });
        catResp.EnsureSuccessStatusCode();
        var cat = await catResp.Content.ReadFromJsonAsync<IdDto>(_json);

        var prodResp = await merchant.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/products", UriKind.Relative),
            new { categoryId = cat!.Id, name, nameEn = name, price, stock, isActive = true });
        prodResp.EnsureSuccessStatusCode();
        var prod = await prodResp.Content.ReadFromJsonAsync<IdDto>(_json);
        return prod!.Id;
    }

    private static async Task<int> GetMerchantProductStockAsync(MerchantTestClient merchant, Guid productId)
    {
        var prod = await merchant.Client.GetFromJsonAsync<ProductStockDto>(
            new Uri($"/api/v1/merchant/products/{productId}", UriKind.Relative), _json);
        return prod!.Stock;
    }

    private sealed record AuthDto(string AccessToken);

    private sealed record IdDto(Guid Id);

    private sealed record ShopDto(Guid Id, string Slug, string Name, string Currency);

    private sealed record PagedDto<T>(List<T> Items, int Total, int Page, int PageSize);

    private sealed record StorefrontProductDto(Guid Id, string Name, decimal Price, int Stock);

    private sealed record ProductStockDto(Guid Id, int Stock);

    private sealed record CartValidationDto(bool IsValid, decimal TotalAmount, List<CartLineDto> Items);

    private sealed record CartLineDto(Guid ProductId, bool IsAvailable, string? Issue);

    private sealed record OrderDetailDto(
        Guid Id,
        string Status,
        decimal TotalAmount,
        List<OrderHistoryDto> StatusHistory);

    private sealed record OrderHistoryDto(string? FromStatus, string ToStatus);
}
