using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        EntityMapping.MapBase(builder, "Orders");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(2000);
        builder.Property(e => e.PlacedAt).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => new { e.TenantId, e.Status, e.PlacedAt });

        builder.HasOne(e => e.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_TotalAmount", "\"TotalAmount\" >= 0");
            t.HasCheckConstraint(
                "CK_Orders_Status",
                "\"Status\" IN ('Pending','Confirmed','Preparing','Shipped','Delivered','Cancelled')");
        });
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        EntityMapping.MapBase(builder, "OrderItems");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.ProductNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.Quantity).IsRequired();

        builder.HasOne(e => e.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "\"UnitPrice\" >= 0");
        });
    }
}

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        EntityMapping.MapBase(builder, "OrderStatusHistories");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ToStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ActorRole).HasMaxLength(32);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.OccurredAt).IsRequired();

        builder.HasIndex(e => new { e.OrderId, e.OccurredAt });

        builder.HasOne(e => e.Order)
            .WithMany(o => o.StatusHistory)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
