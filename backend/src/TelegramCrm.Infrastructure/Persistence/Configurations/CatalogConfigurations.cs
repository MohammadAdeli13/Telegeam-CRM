using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        EntityMapping.MapBase(builder, "Categories");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(120);
        builder.Property(e => e.NameEn).HasMaxLength(120);
        builder.Property(e => e.Slug).IsRequired().HasMaxLength(40);
        builder.Property(e => e.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Slug }).IsUnique();
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        EntityMapping.MapBase(builder, "Products");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.NameEn).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(4000);
        builder.Property(e => e.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.Stock).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        // PostgreSQL system column used as the concurrency token (contract v1.2 §3.7).
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => new { e.TenantId, e.IsActive });

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_Price", "\"Price\" >= 0");
            t.HasCheckConstraint("CK_Products_Stock", "\"Stock\" >= 0");
        });
    }
}

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        EntityMapping.MapBase(builder, "ProductImages");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.StoragePath).IsRequired().HasMaxLength(512);
        builder.Property(e => e.AltText).HasMaxLength(300);
        builder.Property(e => e.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.IsPrimary).IsRequired();

        builder.HasIndex(e => new { e.ProductId, e.SortOrder });

        // At most one primary image per product.
        builder.HasIndex(e => e.ProductId)
            .IsUnique()
            .HasFilter("\"IsPrimary\"")
            .HasDatabaseName("UX_ProductImages_Primary");

        builder.HasOne(e => e.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
