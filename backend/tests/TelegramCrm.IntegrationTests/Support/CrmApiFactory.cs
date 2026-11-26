using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramCrm.Application.Common;
using TelegramCrm.Infrastructure.Persistence;

namespace TelegramCrm.IntegrationTests.Support;

/// <summary>
/// Boots the real API host with the PostgreSQL context swapped for an isolated in-memory
/// database, so the endpoint pipeline (auth, tenancy, error handling) is exercised end to end.
/// </summary>
public sealed class CrmApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"crm-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "telegram-crm",
                ["Jwt:Audience"] = "telegram-crm-web",
                ["Jwt:Secret"] = "integration-test-signing-key-at-least-32-bytes-long",
                ["Auth:InitDataMaxAgeSeconds"] = "86400",
                ["Auth:JwtLifetimeMinutes"] = "60",
                ["Auth:AllowDevBypass"] = "false",
                ["TELEGRAM_BOT_TOKEN"] = InitDataBuilder.BotToken,
            });
        });

        builder.ConfigureServices(services =>
        {
            var replaced = services
                .Where(d => d.ServiceType == typeof(CrmDbContext) ||
                            d.ServiceType == typeof(IAppDbContext) ||
                            d.ServiceType == typeof(DbContextOptions<CrmDbContext>))
                .ToList();

            foreach (var descriptor in replaced)
            {
                services.Remove(descriptor);
            }

            services.AddScoped(sp =>
            {
                var options = new DbContextOptionsBuilder<CrmDbContext>()
                    .UseInMemoryDatabase(_databaseName)
                    .AddInterceptors(new TenantGuardInterceptor(sp.GetRequiredService<ITenantContext>()))
                    .Options;

                return new CrmDbContext(options, sp.GetRequiredService<ITenantContext>());
            });

            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        });
    }
}
