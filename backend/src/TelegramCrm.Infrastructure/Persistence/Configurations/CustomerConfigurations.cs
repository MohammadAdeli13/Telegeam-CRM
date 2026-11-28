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
