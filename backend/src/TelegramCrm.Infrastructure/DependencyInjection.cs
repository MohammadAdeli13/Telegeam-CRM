using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Auth;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Merchant;
using TelegramCrm.Application.Merchants;
using TelegramCrm.Application.Orders;
using TelegramCrm.Application.Storefront;
using TelegramCrm.Application.Telegram;
using TelegramCrm.Infrastructure.Auth;
using TelegramCrm.Infrastructure.Persistence;
using TelegramCrm.Infrastructure.Storage;
using TelegramCrm.Infrastructure.Telegram;

namespace TelegramCrm.Infrastructure;

/// <summary>Composition root for the infrastructure module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<TelegramAuthOptions>(configuration.GetSection(TelegramAuthOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<TelegramBotOptions>(configuration.GetSection(TelegramBotOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<HttpTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());
        services.AddScoped<IStorefrontTenantSetter>(sp => sp.GetRequiredService<HttpTenantContext>());

        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<TenantGuardInterceptor>();

        services.AddScoped<CrmDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<CrmDbContext>()
                .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .AddInterceptors(sp.GetRequiredService<TenantGuardInterceptor>())
                .Options;

            return new CrmDbContext(options, sp.GetRequiredService<ITenantContext>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CrmDbContext>());

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<TelegramInitDataValidator>();
        services.AddScoped<AuthService>();
        services.AddScoped<MerchantService>();
        services.AddScoped<CreateMerchantRequestValidator>();

        // Merchant back-office.
        services.AddScoped<MerchantShopService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ProductService>();
        services.AddScoped<ProductImageService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<ShopSettingService>();

        services.AddScoped<UpdateMerchantShopRequestValidator>();
        services.AddScoped<CreateCategoryRequestValidator>();
        services.AddScoped<UpdateCategoryRequestValidator>();
        services.AddScoped<CreateProductRequestValidator>();
        services.AddScoped<UpdateProductRequestValidator>();
        services.AddScoped<StockAdjustRequestValidator>();
        services.AddScoped<UpdateProductImageRequestValidator>();
        services.AddScoped<CreateCustomerRequestValidator>();
        services.AddScoped<UpdateCustomerRequestValidator>();
        services.AddScoped<CreateTagRequestValidator>();
        services.AddScoped<AttachTagRequestValidator>();
        services.AddScoped<UpdateSettingsRequestValidator>();

        // Orders, Storefront & Telegram Bot.
        services.AddScoped<OrderService>();
        services.AddScoped<StorefrontService>();
        services.AddScoped<TelegramWebhookService>();

        services.AddScoped<UpdateOrderStatusRequestValidator>();
        services.AddScoped<CancelOrderRequestValidator>();
        services.AddScoped<ValidateCartRequestValidator>();
        services.AddScoped<PlaceOrderRequestValidator>();

        services.AddSingleton<ITelegramBotSender, TelegramBotSender>();
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService<OutboxWorker>();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
