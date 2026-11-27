using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Common;
using TelegramCrm.Infrastructure.Persistence;

namespace TelegramCrm.IntegrationTests.Support;

/// <summary>
/// Boots the real API host with the persistence layer swapped for a test database.
/// <para>
/// When <c>TEST_POSTGRES_CONNECTION_STRING</c> is set the suite runs against a real
/// PostgreSQL server: each factory instance owns a private database, migrations are applied
/// with <c>MigrateAsync()</c>, and <see cref="ResetAsync"/> drops and recreates the
/// <c>public</c> schema so tests never observe each other's rows. This is the mode CI uses,
/// and the only mode that exercises the real Npgsql provider (partial indexes, <c>xmin</c>
/// concurrency, <c>ExecuteUpdate</c>, transactions).
/// </para>
/// <para>
/// When the variable is unset — a local sandbox with no PostgreSQL — the factory falls back
/// to an isolated in-memory database so the endpoint pipeline can still be exercised.
/// </para>
/// </summary>
public sealed class CrmApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Environment variable that switches the suite onto real PostgreSQL.</summary>
    public const string PostgresConnectionStringVariable = "TEST_POSTGRES_CONNECTION_STRING";

    /// <summary>Test webhook secret configured on the test host.</summary>
    public const string WebhookSecret = "test-webhook-secret-value-12345";

    private readonly string _databaseName = $"crm-tests-{Guid.NewGuid()}";
    private readonly string _postgresDatabaseName = $"crm_test_{Guid.NewGuid():N}";
    private readonly string? _postgresConnectionString;

    public CrmApiFactory()
    {
        _postgresConnectionString = Environment.GetEnvironmentVariable(PostgresConnectionStringVariable);
    }

    /// <summary>True when the suite is running against real PostgreSQL.</summary>
    public bool UsesPostgres => !string.IsNullOrWhiteSpace(_postgresConnectionString);

    /// <summary>In-memory spy capturing outbound Telegram messages dispatched by OutboxProcessor.</summary>
    public FakeTelegramBotSender TelegramSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "telegram-crm-tests",
                ["Jwt:Audience"] = "telegram-crm-tests-web",
                ["Jwt:Secret"] = "test-signing-key-at-least-32-bytes-long-0123456789",
                ["Auth:InitDataMaxAgeSeconds"] = "86400",
                ["Auth:JwtLifetimeMinutes"] = "60",
                ["Auth:AllowDevBypass"] = "false",
                ["TELEGRAM_BOT_TOKEN"] = InitDataBuilder.BotToken,
                ["TELEGRAM_WEBHOOK_SECRET"] = WebhookSecret,
                ["Telegram:MiniAppBaseUrl"] = "https://miniapp.test.local",
                ["Telegram:EnableOutboxWorker"] = "false",
                ["FileStorage:RootPath"] = Path.Combine(Path.GetTempPath(), "crm-test-storage"),
                ["FileStorage:PublicBaseUrl"] = "/files",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(CrmDbContext) ||
                    d.ServiceType == typeof(IAppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<CrmDbContext>) ||
                    d.ServiceType == typeof(ITelegramBotSender))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<ITelegramBotSender>(TelegramSender);

            services.AddScoped(sp =>
            {
                var tenantContext = sp.GetRequiredService<ITenantContext>();
                var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();

                if (UsesPostgres)
                {
                    optionsBuilder.UseNpgsql(BuildTestDatabaseConnectionString());
                }
                else
                {
                    optionsBuilder.UseInMemoryDatabase(_databaseName);
                }

                optionsBuilder.AddInterceptors(new TenantGuardInterceptor(tenantContext));

                return new CrmDbContext(optionsBuilder.Options, tenantContext);
            });

            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        EnsureDatabaseReady(host.Services);
        return host;
    }

    /// <summary>
    /// Returns the database to a pristine state: for PostgreSQL the <c>public</c> schema is
    /// dropped and re-migrated, for the in-memory fallback the store is deleted and recreated.
    /// Call it at the start of a test that must not see rows written by an earlier one.
    /// </summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        if (UsesPostgres)
        {
            await context.Database
                .ExecuteSqlAsync($"DROP SCHEMA IF EXISTS public CASCADE")
                .ConfigureAwait(false);
            await context.Database
                .ExecuteSqlAsync($"CREATE SCHEMA public")
                .ConfigureAwait(false);
            await context.Database.MigrateAsync().ConfigureAwait(false);
        }
        else
        {
            await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }
    }

    private void EnsureDatabaseReady(IServiceProvider services)
    {
        if (UsesPostgres)
        {
            CreateDatabaseIfMissing();
        }

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        if (UsesPostgres)
        {
            context.Database.Migrate();
        }
        else
        {
            context.Database.EnsureCreated();
        }
    }

    /// <summary>
    /// Creates this factory's private database. It connects to the server's maintenance
    /// database first, because the target database does not exist yet.
    /// </summary>
    private void CreateDatabaseIfMissing()
    {
        var maintenance = new DbConnectionStringBuilder
        {
            ConnectionString = _postgresConnectionString!,
        };
        maintenance["Database"] = "postgres";

        using var connection = new Npgsql.NpgsqlConnection(maintenance.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE "{_postgresDatabaseName}"";
        command.ExecuteNonQuery();
    }

    private string BuildTestDatabaseConnectionString()
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = _postgresConnectionString!,
        };

        builder["Database"] = _postgresDatabaseName;
        return builder.ConnectionString;
    }
}
