using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TelegramCrm.Api.Endpoints;
using TelegramCrm.Api.Infrastructure;
using TelegramCrm.Infrastructure;
using TelegramCrm.Infrastructure.Persistence;
using TelegramCrm.Infrastructure.Storage;

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
var openApiOutputPath = OpenApiExporter.GetOutputPath(args);

app.UseMiddleware<ExceptionHandlingMiddleware>();

// MapOpenApi also registers the document provider used by the contract exporter below.
if (app.Environment.IsDevelopment() || openApiOutputPath is not null)
{
    app.MapOpenApi();
}

// Serve uploaded product images from the configured tenant-namespaced storage directory.
if (openApiOutputPath is null)
{
    var storageOptions = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(storageOptions.RootPath))
    {
        var storageRoot = Path.GetFullPath(storageOptions.RootPath);
        Directory.CreateDirectory(storageRoot);

        var requestPath = storageOptions.PublicBaseUrl.StartsWith('/')
            ? storageOptions.PublicBaseUrl.TrimEnd('/')
            : $"/{storageOptions.PublicBaseUrl.TrimEnd('/')}";

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storageRoot),
            RequestPath = requestPath,
            ServeUnknownFileTypes = false,
        });
    }
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

// Container / production startup: apply EF Core migrations when Database:AutoMigrate is enabled.
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<CrmDbContext>();
    await dbContext.Database.MigrateAsync().ConfigureAwait(false);
}

app.Run();

/// <summary>Entry point marker so <c>WebApplicationFactory</c> can bind to the assembly.</summary>
public partial class Program;
