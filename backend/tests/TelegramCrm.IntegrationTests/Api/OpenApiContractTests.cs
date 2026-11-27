using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using TelegramCrm.Api.Infrastructure;
using TelegramCrm.IntegrationTests.Support;

namespace TelegramCrm.IntegrationTests.Api;

/// <summary>
/// Guards the P1 QA finding that the committed contract must be codegen-ready: a
/// <c>Bearer</c> security scheme plus a per-operation <c>security</c> requirement on every
/// operation that is not explicitly anonymous.
/// </summary>
public sealed class OpenApiContractTests : IClassFixture<CrmApiFactory>
{
    private readonly CrmApiFactory _factory;

    public OpenApiContractTests(CrmApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Document_DeclaresBearerScheme_AndRequiresItOnProtectedOperations()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider
            .GetRequiredKeyedService<IOpenApiDocumentProvider>(OpenApiExporter.DocumentName);

        var document = await provider.GetOpenApiDocumentAsync(CancellationToken.None);

        using var buffer = new MemoryStream();
        await using (var writer = new StreamWriter(buffer, leaveOpen: true))
        {
            var jsonWriter = new Microsoft.OpenApi.OpenApiJsonWriter(writer);
            document.SerializeAsV31(jsonWriter);
            await jsonWriter.FlushAsync();
        }

        buffer.Position = 0;
        using var json = await JsonDocument.ParseAsync(buffer);

        var schemes = json.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.True(schemes.TryGetProperty(OpenApiSetup.BearerSchemeName, out var bearer));
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());

        // Genuinely anonymous operations (plus webhook validated by secret-token header).
        var anonymous = new HashSet<string>(StringComparer.Ordinal)
        {
            "POST /api/v1/auth/telegram",
            "GET /api/v1/health",
            "GET /api/v1/storefront/{shopSlug}",
            "GET /api/v1/storefront/{shopSlug}/categories",
            "GET /api/v1/storefront/{shopSlug}/products",
            "GET /api/v1/storefront/{shopSlug}/products/{id}",
            "POST /api/v1/telegram/webhook",
        };

        var missing = new List<string>();
        foreach (var path in json.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var key = $"{operation.Name.ToUpperInvariant()} {path.Name}";
                if (anonymous.Contains(key))
                {
                    continue;
                }

                if (!operation.Value.TryGetProperty("security", out var security) ||
                    security.GetArrayLength() == 0)
                {
                    missing.Add(key);
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public async Task Document_CoversTheWholeP2BackendSurface()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider
            .GetRequiredKeyedService<IOpenApiDocumentProvider>(OpenApiExporter.DocumentName);

        var document = await provider.GetOpenApiDocumentAsync(CancellationToken.None);

        using var buffer = new MemoryStream();
        await using (var writer = new StreamWriter(buffer, leaveOpen: true))
        {
            var jsonWriter = new Microsoft.OpenApi.OpenApiJsonWriter(writer);
            document.SerializeAsV31(jsonWriter);
            await jsonWriter.FlushAsync();
        }

        buffer.Position = 0;
        using var json = await JsonDocument.ParseAsync(buffer);

        var paths = json.RootElement.GetProperty("paths").EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] expected =
        [
            "/api/v1/merchant/categories",
            "/api/v1/merchant/categories/{id}",
            "/api/v1/merchant/products",
            "/api/v1/merchant/products/{id}",
            "/api/v1/merchant/products/{id}/stock-adjust",
            "/api/v1/merchant/products/{productId}/images",
            "/api/v1/merchant/products/{productId}/images/{imageId}",
            "/api/v1/merchant/products/{productId}/images/{imageId}/primary",
            "/api/v1/merchant/customers",
            "/api/v1/merchant/customers/{id}",
            "/api/v1/merchant/customers/{id}/orders",
            "/api/v1/merchant/customers/{id}/tags",
            "/api/v1/merchant/customers/{id}/tags/{tagId}",
            "/api/v1/merchant/tags",
            "/api/v1/merchant/tags/{id}",
            "/api/v1/merchant/settings",
            "/api/v1/merchant/orders",
            "/api/v1/merchant/orders/{id}",
            "/api/v1/merchant/orders/{id}/status",
            "/api/v1/merchant/orders/{id}/history",
            "/api/v1/storefront/{shopSlug}",
            "/api/v1/storefront/{shopSlug}/categories",
            "/api/v1/storefront/{shopSlug}/products",
            "/api/v1/storefront/{shopSlug}/products/{id}",
            "/api/v1/storefront/{shopSlug}/cart/validate",
            "/api/v1/storefront/{shopSlug}/orders",
            "/api/v1/storefront/{shopSlug}/orders/{id}",
            "/api/v1/storefront/{shopSlug}/orders/{id}/cancel",
            "/api/v1/telegram/webhook",
        ];

        var absent = expected.Where(p => !paths.Contains(p)).ToList();
        Assert.Empty(absent);
    }
}
