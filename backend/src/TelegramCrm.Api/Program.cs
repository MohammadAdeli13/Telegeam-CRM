using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using TelegramCrm.Api.Endpoints;
using TelegramCrm.Api.Infrastructure;
using TelegramCrm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCrmOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// MapOpenApi also registers the document provider used by the contract exporter below.
if (app.Environment.IsDevelopment() || OpenApiExporter.GetOutputPath(args) is not null)
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMerchantEndpoints();

// Merchant back-office (P2a + P2b) — every group requires the `Merchant` policy.
app.MapCategoryEndpoints();
app.MapProductEndpoints();
app.MapProductImageEndpoints();
app.MapCustomerEndpoints();
app.MapTagEndpoints();
app.MapSettingsEndpoints();
app.MapMerchantOrderEndpoints();

// Storefront & Telegram Bot webhook (P2b).
app.MapStorefrontEndpoints();
app.MapTelegramWebhookEndpoints();

// Contract generation: `dotnet run --project src/TelegramCrm.Api -- --generate-openapi <path>`
// emits the document from the real endpoint metadata and exits (see contracts/README.md).
// The host is started on an ephemeral loopback port first, because the endpoint data
// source is only materialised once the application has started.
var openApiOutputPath = OpenApiExporter.GetOutputPath(args);
if (openApiOutputPath is not null)
{
    app.Urls.Add("http://127.0.0.1:0");
    await app.StartAsync().ConfigureAwait(false);
    try
    {
        await OpenApiExporter.WriteAsync(app.Services, openApiOutputPath).ConfigureAwait(false);
    }
    finally
    {
        await app.StopAsync().ConfigureAwait(false);
    }

    return;
}

app.Run();

/// <summary>Entry point marker so <c>WebApplicationFactory</c> can bind to the assembly.</summary>
public partial class Program;
