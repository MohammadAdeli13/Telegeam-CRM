using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

public sealed class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> builder)
    {
        EntityMapping.MapBase(builder, "ShippingMethods");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.FreeAboveAmount).HasPrecision(18, 2);
        builder.Property(e => e.IsPayAtDestination).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.EstimatedDaysText).HasMaxLength(80);
        builder.Property(e => e.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(e => new { e.TenantId, e.IsActive, e.SortOrder });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ShippingMethods_Price", "\"Price\" >= 0");
            t.HasCheckConstraint(
                "CK_ShippingMethods_FreeAboveAmount",
                "\"FreeAboveAmount\" IS NULL OR \"FreeAboveAmount\" >= 0");
        });
    }
}
