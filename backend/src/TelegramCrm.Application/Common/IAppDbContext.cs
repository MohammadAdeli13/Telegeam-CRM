using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Application.Common;

/// <summary>
/// The persistence surface the application layer is allowed to use. It exposes the
/// EF Core <see cref="DbSet{TEntity}"/>s directly — there is deliberately no generic
/// repository wrapper (contract v1.2 §1.1). Implemented by the infrastructure DbContext.
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }

    DbSet<TenantMember> TenantMembers { get; }

    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductImage> ProductImages { get; }

    DbSet<Customer> Customers { get; }

    DbSet<CustomerAddress> CustomerAddresses { get; }

    DbSet<ShippingMethod> ShippingMethods { get; }

    DbSet<Tag> Tags { get; }

    DbSet<CustomerTag> CustomerTags { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<OrderStatusHistory> OrderStatusHistories { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<ShopSetting> ShopSettings { get; }

    /// <summary>
    /// Relational database facade, used for explicit transactions and set-based atomic
    /// updates (stock decrement / restore, contract v1.2 §3.3–§3.4).
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
