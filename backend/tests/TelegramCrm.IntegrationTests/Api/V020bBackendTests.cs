using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TelegramCrm.Infrastructure.Orders;
using TelegramCrm.Infrastructure.Persistence;
using TelegramCrm.Infrastructure.Telegram;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>
/// End-to-end tests for v0.2.0-b: CustomerAddresses (multi-address + snapshot + isolation),
/// ShippingMethods (fees, free threshold, pay-at-destination, isolation), Card-to-Card receipt
/// upload/reject/re-upload/approve, OrderExpiryProcessor auto-cancel + stock restore,
/// sequential per-tenant OrderNumber, RequireOrderApproval=false auto-confirm + cancel policy,
/// and CarrierName/TrackingCode notifications.
/// </summary>
public sealed class V020bBackendTests : IClassFixture<CrmApiFactory>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private static readonly byte[] _pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly CrmApiFactory _factory;

    public V020bBackendTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShippingMethods_CrudFreeThresholdPayAtDestination_AndCrossTenantIsolation()
    {
        var tenantA = await MerchantTestClient.CreateAsync(_factory, 97001);
        var tenantB = await MerchantTestClient.CreateAsync(_factory, 97002);

        // Paid method with free-above threshold.
        var postResp = await tenantA.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/shipping-methods", UriKind.Relative),
            new
            {
                name = "پست پیشتاز",
                description = "ارسال سراسری",
                price = 50000m,
                freeAboveAmount = 300000m,
                isPayAtDestination = false,
                estimatedDaysText = "۲ تا ۴ روز کاری",
                sortOrder = 1,
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, postResp.StatusCode);
        var postMethod = await postResp.Content.ReadFromJsonAsync<ShippingMethodDto>(_json);

        // Pay-at-destination method (Tipax).
        var tipaxResp = await tenantA.Client.PostAsJsonAsync(
            new Uri("/api/v1/merchant/shipping-methods", UriKind.Relative),
            new
            {
                name = "تیپاکس",
                price = 90000m,
                isPayAtDestination = true,
                sortOrder = 2,
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, tipaxResp.StatusCode);
        var tipaxMethod = await tipaxResp.Content.ReadFromJsonAsync<ShippingMethodDto>(_json);

        // Cross-tenant access must return 404.
        var crossRead = await tenantB.Client.GetAsync(
            new Uri($"/api/v1/merchant/shipping-methods/{postMethod!.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, crossRead.StatusCode);

        // Public storefront lists Tenant A's methods only.
        using var anon = _factory.CreateClient();
        var publicList = await anon.GetFromJsonAsync<List<ShippingMethodDto>>(
            new Uri($"/api/v1/storefront/{tenantA.Slug}/shipping-methods", UriKind.Relative), _json);
        Assert.Equal(2, publicList!.Count);

        var productId = await CreateProductAsync(tenantA, "قهوه", 150000m, 10);
        using var customer = await CreateCustomerClientAsync(98001);

        // Below free threshold (150k < 300k) -> shipping = 50k, total = 200k.
        var valBelow = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{tenantA.Slug}/cart/validate", UriKind.Relative),
            new { items = new[] { new { productId, quantity = 1 } }, shippingMethodId = postMethod.Id });
        var belowBody = await valBelow.Content.ReadFromJsonAsync<CartValidationDto>(_json);
        Assert.Equal(150000m, belowBody!.SubtotalAmount);
        Assert.Equal(50000m, belowBody.ShippingAmount);
        Assert.Equal(200000m, belowBody.TotalAmount);

        // Meets free threshold (300k >= 300k) -> shipping = 0, total = 300k.
        var valFree = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{tenantA.Slug}/cart/validate", UriKind.Relative),
            new { items = new[] { new { productId, quantity = 2 } }, shippingMethodId = postMethod.Id });
        var freeBody = await valFree.Content.ReadFromJsonAsync<CartValidationDto>(_json);
        Assert.Equal(300000m, freeBody!.SubtotalAmount);
        Assert.Equal(0m, freeBody.ShippingAmount);
        Assert.Equal(300000m, freeBody.TotalAmount);

        // Pay-at-destination -> shipping added to total is 0, flag is true.
        var valTipax = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{tenantA.Slug}/cart/validate", UriKind.Relative),
            new { items = new[] { new { productId, quantity = 1 } }, shippingMethodId = tipaxMethod!.Id });
        var tipaxBody = await valTipax.Content.ReadFromJsonAsync<CartValidationDto>(_json);
        Assert.True(tipaxBody!.IsShippingPayAtDestination);
        Assert.Equal(0m, tipaxBody.ShippingAmount);
        Assert.Equal(150000m, tipaxBody.TotalAmount);
    }

    [Fact]
    public async Task CustomerAddresses_MultiAddressDefaultSwapSnapshot_AndCrossCustomerIsolation()
    {
        var merchant = await MerchantTestClient.CreateAsync(_factory, 97003);
        var productId = await CreateProductAsync(merchant, "ماگ", 200000m, 10);

        using var customer1 = await CreateCustomerClientAsync(98002);
        using var customer2 = await CreateCustomerClientAsync(98003);

        // First address becomes default automatically.
        var addr1Resp = await customer1.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/addresses", UriKind.Relative),
            new
            {
                title = "خانه",
                recipientName = "سارا کریمی",
                phone = "+989121112233",
                province = "تهران",
                city = "تهران",
                addressLine = "خیابان ولیعصر",
                plaque = "12",
                unit = "4",
                postalCode = "1234567890",
            });
        Assert.Equal(HttpStatusCode.Created, addr1Resp.StatusCode);
        var addr1 = await addr1Resp.Content.ReadFromJsonAsync<AddressDto>(_json);
        Assert.True(addr1!.IsDefault);

        // Second address marked default swaps cleanly.
        var addr2Resp = await customer1.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/addresses", UriKind.Relative),
            new
            {
                title = "محل کار",
                recipientName = "سارا کریمی",
                phone = "+989121112233",
                province = "اصفهان",
                city = "اصفهان",
                addressLine = "خیابان چهارباغ",
                isDefault = true,
            });
        Assert.Equal(HttpStatusCode.Created, addr2Resp.StatusCode);
        var addr2 = await addr2Resp.Content.ReadFromJsonAsync<AddressDto>(_json);
        Assert.True(addr2!.IsDefault);

        // Cross-customer update returns 404.
        var crossPatch = await customer2.PatchAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/addresses/{addr2.Id}", UriKind.Relative),
            new { city = "شیراز" });
        Assert.Equal(HttpStatusCode.NotFound, crossPatch.StatusCode);

        // Place order with addr2 -> snapshots immutable city/address.
        using var req = CreateOrderRequest(
            merchant.Slug,
            $"addr-{Guid.NewGuid():N}",
            new
            {
                items = new[] { new { productId, quantity = 1 } },
                addressId = addr2.Id,
            });
        var placed = await customer1.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal("اصفهان", order!.ShippingCity);
        Assert.Contains("چهارباغ", order.ShippingAddressSnapshot!, StringComparison.Ordinal);

        // Editing addr2 afterward does NOT mutate the existing order snapshot.
        var editResp = await customer1.PatchAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchant.Slug}/addresses/{addr2.Id}", UriKind.Relative),
            new { city = "مشهد", addressLine = "بلوار وکیل‌آباد" });
        Assert.Equal(HttpStatusCode.OK, editResp.StatusCode);

        var reloadedOrder = await customer1.GetFromJsonAsync<OrderDetailDto>(
            new Uri($"/api/v1/storefront/{merchant.Slug}/orders/{order.Id}", UriKind.Relative), _json);
        Assert.Equal("اصفهان", reloadedOrder!.ShippingCity);
    }

    [Fact]
    public async Task AutoConfirmDefault_AllowsSelfCancelBeforePreparing_AndSequentialOrderNumbersPerTenant()
    {
        var merchantA = await MerchantTestClient.CreateAsync(_factory, 97004);
        var merchantB = await MerchantTestClient.CreateAsync(_factory, 97005);
        var productA = await CreateProductAsync(merchantA, "تی‌شرت", 100000m, 5);
        var productB = await CreateProductAsync(merchantB, "شال", 100000m, 5);

        using var customer = await CreateCustomerClientAsync(98004);

        // Order #1 in Shop A -> OrderNumber = 1001, auto-Confirmed.
        using var reqA1 = CreateOrderRequest(
            merchantA.Slug,
            $"a1-{Guid.NewGuid():N}",
            new { items = new[] { new { productId = productA, quantity = 2 } }, paymentMethod = "PayOnDelivery" });
        var resA1 = await customer.SendAsync(reqA1);
        Assert.Equal(HttpStatusCode.Created, resA1.StatusCode);
        var ordA1 = await resA1.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal(1001, ordA1!.OrderNumber);
        Assert.Equal("Confirmed", ordA1.Status);

        // Customer CAN cancel an auto-confirmed order before Preparing -> stock restored 3 -> 5.
        var cancelA1 = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchantA.Slug}/orders/{ordA1.Id}/cancel", UriKind.Relative),
            new { reason = "انصراف" });
        Assert.Equal(HttpStatusCode.OK, cancelA1.StatusCode);
        Assert.Equal(5, await GetStockAsync(merchantA, productA));

        // Order #2 in Shop A -> OrderNumber = 1002; once merchant moves to Preparing, customer cannot cancel.
        using var reqA2 = CreateOrderRequest(
            merchantA.Slug,
            $"a2-{Guid.NewGuid():N}",
            new { items = new[] { new { productId = productA, quantity = 1 } } });
        var resA2 = await customer.SendAsync(reqA2);
        var ordA2 = await resA2.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal(1002, ordA2!.OrderNumber);

        var prepResp = await merchantA.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{ordA2.Id}/status", UriKind.Relative),
            new { status = "Preparing" });
        Assert.Equal(HttpStatusCode.OK, prepResp.StatusCode);

        var lateCancel = await customer.PostAsJsonAsync(
            new Uri($"/api/v1/storefront/{merchantA.Slug}/orders/{ordA2.Id}/cancel", UriKind.Relative),
            new { reason = "دیر شد" });
        Assert.Equal(HttpStatusCode.Conflict, lateCancel.StatusCode);

        // Order #1 in Shop B starts independently at 1001.
        using var reqB1 = CreateOrderRequest(
            merchantB.Slug,
            $"b1-{Guid.NewGuid():N}",
            new { items = new[] { new { productId = productB, quantity = 1 } } });
        var resB1 = await customer.SendAsync(reqB1);
        var ordB1 = await resB1.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal(1001, ordB1!.OrderNumber);
    }

    [Fact]
    public async Task CardToCard_ReceiptRejectReuploadApproveAutoConfirm_AndTrackingCodeNotification()
    {
        _factory.TelegramSender.Clear();
        var merchant = await MerchantTestClient.CreateAsync(_factory, 97006);

        // Enabling card-to-card without card number -> 422.
        var missingCard = await merchant.Client.PatchAsJsonAsync(
            new Uri("/api/v1/merchant/shop", UriKind.Relative),
            new { enableCardToCard = true });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingCard.StatusCode);

        // Enable card-to-card properly (RequireOrderApproval remains default false).
        var cfgResp = await merchant.Client.PatchAsJsonAsync(
            new Uri("/api/v1/merchant/shop", UriKind.Relative),
            new
            {
                enableCardToCard = true,
                cardNumber = "6037-9911-2233-4455",
                cardHolderName = "گالری نور",
                bankName = "بانک ملی",
                paymentExpiryMinutes = 30,
            });
        Assert.Equal(HttpStatusCode.OK, cfgResp.StatusCode);

        var productId = await CreateProductAsync(merchant, "کتان", 400000m, 5);
        using var customer = await CreateCustomerClientAsync(98005);

        // Card-to-card order stays Pending + AwaitingReceipt with deadline.
        using var orderReq = CreateOrderRequest(
            merchant.Slug,
            $"c2c-{Guid.NewGuid():N}",
            new { items = new[] { new { productId, quantity = 1 } }, paymentMethod = "CardToCard" });
        var placed = await customer.SendAsync(orderReq);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal("Pending", order!.Status);
        Assert.Equal("CardToCard", order.PaymentMethod);
        Assert.Equal("AwaitingReceipt", order.PaymentStatus);
        Assert.NotNull(order.PaymentExpiresAt);

        // Spoofed receipt -> 415.
        using (var badForm = CreateImageForm("not-an-image"u8.ToArray(), "image/png", "bad.png"))
        {
            var badUpload = await customer.PostAsync(
                new Uri($"/api/v1/storefront/{merchant.Slug}/orders/{order.Id}/receipt", UriKind.Relative),
                badForm);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, badUpload.StatusCode);
        }

        // Valid receipt upload -> ReceiptSubmitted, expiry paused.
        using (var okForm = CreateImageForm(_pngHeader, "image/png", "receipt1.png"))
        {
            var uploadResp = await customer.PostAsync(
                new Uri($"/api/v1/storefront/{merchant.Slug}/orders/{order.Id}/receipt", UriKind.Relative),
                okForm);
            Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);
            var uploaded = await uploadResp.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
            Assert.Equal("ReceiptSubmitted", uploaded!.PaymentStatus);
            Assert.Null(uploaded.PaymentExpiresAt);
            Assert.False(string.IsNullOrWhiteSpace(uploaded.ReceiptUrl));
        }

        // Merchant rejects receipt -> Rejected + fresh expiry deadline.
        var rejectResp = await merchant.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{order.Id}/payment-decision", UriKind.Relative),
            new { approve = false, reason = "مبلغ واریزی ناقص است" });
        Assert.Equal(HttpStatusCode.OK, rejectResp.StatusCode);
        var rejected = await rejectResp.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal("Rejected", rejected!.PaymentStatus);
        Assert.Equal("Pending", rejected.Status);
        Assert.Equal("مبلغ واریزی ناقص است", rejected.PaymentRejectionReason);
        Assert.NotNull(rejected.PaymentExpiresAt);

        // Customer re-uploads valid receipt -> ReceiptSubmitted.
        using (var okForm2 = CreateImageForm(_pngHeader, "image/png", "receipt2.png"))
        {
            var reupload = await customer.PostAsync(
                new Uri($"/api/v1/storefront/{merchant.Slug}/orders/{order.Id}/receipt", UriKind.Relative),
                okForm2);
            Assert.Equal(HttpStatusCode.OK, reupload.StatusCode);
        }

        // Merchant approves -> PaymentStatus=Verified AND auto-confirms order (Pending -> Confirmed).
        var approveResp = await merchant.Client.PostAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{order.Id}/payment-decision", UriKind.Relative),
            new { approve = true });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approved = await approveResp.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal("Verified", approved!.PaymentStatus);
        Assert.Equal("Confirmed", approved.Status);

        // Transition Confirmed -> Preparing -> Shipped with CarrierName & TrackingCode.
        var prep = await merchant.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{order.Id}/status", UriKind.Relative),
            new { status = "Preparing" });
        Assert.Equal(HttpStatusCode.OK, prep.StatusCode);

        var ship = await merchant.Client.PatchAsJsonAsync(
            new Uri($"/api/v1/merchant/orders/{order.Id}/status", UriKind.Relative),
            new
            {
                status = "Shipped",
                carrierName = "پست پیشتاز",
                trackingCode = "98765432109876543210",
            });
        Assert.Equal(HttpStatusCode.OK, ship.StatusCode);
        var shipped = await ship.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal("Shipped", shipped!.Status);
        Assert.Equal("پست پیشتاز", shipped.CarrierName);
        Assert.Equal("98765432109876543210", shipped.TrackingCode);
        Assert.NotNull(shipped.ShippedAt);

        // Drain Outbox and verify Persian tracking code + receipt messages were sent.
        var outbox = _factory.Services.GetRequiredService<OutboxProcessor>();
        while (await outbox.ProcessBatchAsync() > 0)
        {
        }

        var sent = _factory.TelegramSender.Messages;
        Assert.Contains(sent, m => m.ChatId == 97006 && m.Text.Contains("فیش واریزی", StringComparison.Ordinal));
        Assert.Contains(sent, m => m.ChatId == 98005 && m.Text.Contains("مبلغ واریزی ناقص است", StringComparison.Ordinal));
        Assert.Contains(
            sent,
            m => m.ChatId == 98005 &&
                 m.Text.Contains("ارسال شده", StringComparison.Ordinal) &&
                 m.Text.Contains("پست پیشتاز", StringComparison.Ordinal) &&
                 m.Text.Contains("98765432109876543210", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OrderExpiryProcessor_CancelsExpiredUnpaidOrder_AndRestoresStockAtomically()
    {
        _factory.TelegramSender.Clear();
        var merchant = await MerchantTestClient.CreateAsync(_factory, 97007);
        var cfg = await merchant.Client.PatchAsJsonAsync(
            new Uri("/api/v1/merchant/shop", UriKind.Relative),
            new { enableCardToCard = true, cardNumber = "6037-9911-0000-1111" });
        cfg.EnsureSuccessStatusCode();

        var productId = await CreateProductAsync(merchant, "کفش", 500000m, 5);
        using var customer = await CreateCustomerClientAsync(98006);

        using var orderReq = CreateOrderRequest(
            merchant.Slug,
            $"exp-{Guid.NewGuid():N}",
            new { items = new[] { new { productId, quantity = 2 } }, paymentMethod = "CardToCard" });
        var placed = await customer.SendAsync(orderReq);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<OrderDetailDto>(_json);
        Assert.Equal(3, await GetStockAsync(merchant, productId));

        // Backdate PaymentExpiresAt in DB so the order is overdue.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var entity = await db.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == order!.Id);
            entity.PaymentExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        // Run expiry sweep deterministically.
        var expiryProcessor = _factory.Services.GetRequiredService<OrderExpiryProcessor>();
        var cancelledCount = await expiryProcessor.ProcessExpiredOrdersAsync();
        Assert.True(cancelledCount >= 1);

        // Order is now Cancelled and stock is restored (3 -> 5).
        var afterExpiry = await merchant.Client.GetFromJsonAsync<OrderDetailDto>(
            new Uri($"/api/v1/merchant/orders/{order!.Id}", UriKind.Relative), _json);
        Assert.Equal("Cancelled", afterExpiry!.Status);
        Assert.Equal(5, await GetStockAsync(merchant, productId));

        // Running sweep a second time is an idempotent no-op (stock remains 5).
        Assert.Equal(0, await expiryProcessor.ProcessExpiredOrdersAsync());
        Assert.Equal(5, await GetStockAsync(merchant, productId));

        // Outbox notifies customer in Persian about payment expiry.
        var outbox = _factory.Services.GetRequiredService<OutboxProcessor>();
        while (await outbox.ProcessBatchAsync() > 0)
        {
        }

        Assert.Contains(
            _factory.TelegramSender.Messages,
            m => m.ChatId == 98006 && m.Text.Contains("مهلت ارسال فیش", StringComparison.Ordinal));
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

    private static HttpRequestMessage CreateOrderRequest(string shopSlug, string idempotencyKey, object body)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/v1/storefront/{shopSlug}/orders", UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return req;
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
            new { categoryId = cat!.Id, name, price, stock, isActive = true });
        prodResp.EnsureSuccessStatusCode();
        var prod = await prodResp.Content.ReadFromJsonAsync<IdDto>(_json);
        return prod!.Id;
    }

    private static async Task<int> GetStockAsync(MerchantTestClient merchant, Guid productId)
    {
        var prod = await merchant.Client.GetFromJsonAsync<ProductStockDto>(
            new Uri($"/api/v1/merchant/products/{productId}", UriKind.Relative), _json);
        return prod!.Stock;
    }

    private sealed record AuthDto(string AccessToken);

    private sealed record IdDto(Guid Id);

    private sealed record ProductStockDto(Guid Id, int Stock);

    private sealed record ShippingMethodDto(
        Guid Id,
        string Name,
        decimal Price,
        decimal? FreeAboveAmount,
        bool IsPayAtDestination);

    private sealed record AddressDto(
        Guid Id,
        string Title,
        string City,
        bool IsDefault);

    private sealed record CartValidationDto(
        bool IsValid,
        decimal SubtotalAmount,
        decimal ShippingAmount,
        bool IsShippingPayAtDestination,
        decimal TotalAmount);

    private sealed record OrderDetailDto(
        Guid Id,
        int OrderNumber,
        string Status,
        decimal SubtotalAmount,
        decimal ShippingAmount,
        decimal TotalAmount,
        string? ShippingCity,
        string? ShippingAddressSnapshot,
        string PaymentMethod,
        string PaymentStatus,
        string? ReceiptUrl,
        string? PaymentRejectionReason,
        DateTimeOffset? PaymentExpiresAt,
        string? CarrierName,
        string? TrackingCode,
        DateTimeOffset? ShippedAt);
}
