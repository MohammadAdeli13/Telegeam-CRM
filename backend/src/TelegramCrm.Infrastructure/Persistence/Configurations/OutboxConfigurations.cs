using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        EntityMapping.MapBase(builder, "OutboxMessages");

        builder.Property(e => e.Type).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Attempts).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.LastError).HasMaxLength(2000);

        // Partial index: the worker only ever scans undelivered rows.
        builder.HasIndex(e => e.ProcessedAt)
            .HasFilter("\"ProcessedAt\" IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Unprocessed");
    }
}

public sealed class ShopSettingConfiguration : IEntityTypeConfiguration<ShopSetting>
{
    public void Configure(EntityTypeBuilder<ShopSetting> builder)
    {
        EntityMapping.MapBase(builder, "ShopSettings");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Key).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Value).IsRequired().HasColumnType("jsonb");

        builder.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
    }
}
