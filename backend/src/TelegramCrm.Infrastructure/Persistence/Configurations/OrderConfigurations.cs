using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Enums;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        EntityMapping.MapBase(builder, "Orders");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.OrderNumber).IsRequired().HasDefaultValue(0);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.SubtotalAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.ShippingAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(e => e.ShippingMethodNameSnapshot).HasMaxLength(120);
        builder.Property(e => e.IsShippingPayAtDestination).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RecipientName).HasMaxLength(150);
        builder.Property(e => e.RecipientPhone).HasMaxLength(20);
        builder.Property(e => e.ShippingProvince).HasMaxLength(80);
        builder.Property(e => e.ShippingCity).HasMaxLength(80);
        builder.Property(e => e.ShippingAddressSnapshot).HasMaxLength(1000);
        builder.Property(e => e.ShippingPostalCode).HasMaxLength(20);

        builder.Property(e => e.PaymentMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(24)
            .HasDefaultValue(PaymentMethod.None);

        builder.Property(e => e.PaymentStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(24)
            .HasDefaultValue(PaymentStatus.NotRequired);

        builder.Property(e => e.ReceiptStoragePath).HasMaxLength(512);
        builder.Property(e => e.PaymentRejectionReason).HasMaxLength(500);
        builder.Property(e => e.CarrierName).HasMaxLength(80);
        builder.Property(e => e.TrackingCode).HasMaxLength(100);

        builder.Property(e => e.Note).HasMaxLength(2000);
        builder.Property(e => e.PlacedAt).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => new { e.TenantId, e.Status, e.PlacedAt });
        builder.HasIndex(e => new { e.TenantId, e.ShippingCity });

        // Partial unique index guarantees sequential order numbers are unique per tenant while
        // remaining safe during online migrations over existing rows.
        builder.HasIndex(e => new { e.TenantId, e.OrderNumber })
            .IsUnique()
            .HasFilter("\"OrderNumber\" > 0")
            .HasDatabaseName("UX_Orders_TenantId_OrderNumber");

        // Partial index so OrderExpiryWorker scans only pending orders with a deadline.
        builder.HasIndex(e => e.PaymentExpiresAt)
            .HasFilter("\"PaymentExpiresAt\" IS NOT NULL AND \"Status\" = 'Pending'")
            .HasDatabaseName("IX_Orders_PaymentExpiresAt_Pending");

        builder.HasOne(e => e.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_SubtotalAmount", "\"SubtotalAmount\" >= 0");
            t.HasCheckConstraint("CK_Orders_ShippingAmount", "\"ShippingAmount\" >= 0");
            t.HasCheckConstraint("CK_Orders_TotalAmount", "\"TotalAmount\" >= 0");
            t.HasCheckConstraint(
                "CK_Orders_Status",
                "\"Status\" IN ('Pending','Confirmed','Preparing','Shipped','Delivered','Cancelled')");
            t.HasCheckConstraint(
                "CK_Orders_PaymentMethod",
                "\"PaymentMethod\" IN ('None','PayOnDelivery','CardToCard')");
            t.HasCheckConstraint(
                "CK_Orders_PaymentStatus",
                "\"PaymentStatus\" IN ('NotRequired','AwaitingReceipt','ReceiptSubmitted','Verified','Rejected')");
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
