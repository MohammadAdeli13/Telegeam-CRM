using Microsoft.EntityFrameworkCore;
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

    DbSet<Tag> Tags { get; }

    DbSet<CustomerTag> CustomerTags { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<OrderStatusHistory> OrderStatusHistories { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<ShopSetting> ShopSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
