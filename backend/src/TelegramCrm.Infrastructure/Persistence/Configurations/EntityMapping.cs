using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Common;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

/// <summary>Shared mapping for every entity: table name, key, UTC timestamps.</summary>
internal static class EntityMapping
{
    public static void MapBase<TEntity>(EntityTypeBuilder<TEntity> builder, string table)
        where TEntity : Entity
    {
        builder.ToTable(table);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
    }

    public static void MapTenant<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ITenantOwned
    {
        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => e.TenantId);
    }
}
