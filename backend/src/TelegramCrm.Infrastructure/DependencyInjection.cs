using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Auth;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Merchant;
using TelegramCrm.Application.Merchants;
using TelegramCrm.Infrastructure.Auth;
using TelegramCrm.Infrastructure.Persistence;
using TelegramCrm.Infrastructure.Storage;

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

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ITenantContext, HttpTenantContext>();
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

        // Merchant back-office (P2a).
        services.AddScoped<CategoryService>();
        services.AddScoped<ProductService>();
        services.AddScoped<ProductImageService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<ShopSettingService>();

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

        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
