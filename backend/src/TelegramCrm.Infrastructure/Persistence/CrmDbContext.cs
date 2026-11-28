using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Common;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context. Tenant isolation is enforced twice: a global query filter
/// on every <see cref="ITenantOwned"/> entity, and the <see cref="TenantGuardInterceptor"/>.
/// </summary>
public class CrmDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext _tenantContext;

    public CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<CustomerTag> CustomerTags => Set<CustomerTag>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ShopSetting> ShopSettings => Set<ShopSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies <c>e =&gt; e.TenantId == _tenantContext.TenantId</c> to every tenant-owned
    /// entity. When no tenant is resolved the filter compares against
    /// <see cref="Guid.Empty"/>, which matches nothing — a tenant-scoped query can never
    /// leak another tenant's rows, it simply returns none.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(CrmDbContext)
                .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }
}
