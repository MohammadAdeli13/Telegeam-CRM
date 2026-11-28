using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        EntityMapping.MapBase(builder, "Customers");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.TelegramUserId).IsRequired();
        builder.Property(e => e.TelegramUsername).HasMaxLength(64);
        builder.Property(e => e.DisplayName).HasMaxLength(200);
        builder.Property(e => e.Phone).HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasIndex(e => new { e.TenantId, e.TelegramUserId }).IsUnique();
    }
}

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        EntityMapping.MapBase(builder, "CustomerAddresses");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Title).IsRequired().HasMaxLength(60);
        builder.Property(e => e.RecipientName).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Phone).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Province).IsRequired().HasMaxLength(80);
        builder.Property(e => e.City).IsRequired().HasMaxLength(80);
        builder.Property(e => e.AddressLine).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Plaque).HasMaxLength(20);
        builder.Property(e => e.Unit).HasMaxLength(20);
        builder.Property(e => e.PostalCode).HasMaxLength(20);
        builder.Property(e => e.IsDefault).IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => new { e.TenantId, e.CustomerId });

        // At most one default address per customer.
        builder.HasIndex(e => e.CustomerId)
            .IsUnique()
            .HasFilter("\"IsDefault\"")
            .HasDatabaseName("UX_CustomerAddresses_Default");

        builder.HasOne(e => e.Customer)
            .WithMany(c => c.Addresses)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        EntityMapping.MapBase(builder, "Tags");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(60);

        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
    }
}

public sealed class CustomerTagConfiguration : IEntityTypeConfiguration<CustomerTag>
{
    public void Configure(EntityTypeBuilder<CustomerTag> builder)
    {
        builder.ToTable("CustomerTags");
        builder.HasKey(e => new { e.CustomerId, e.TagId });

        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => e.TenantId);

        builder.HasOne(e => e.Customer)
            .WithMany(c => c.Tags)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Tag)
            .WithMany(t => t.Customers)
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
