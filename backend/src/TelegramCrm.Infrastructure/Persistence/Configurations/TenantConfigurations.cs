using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        EntityMapping.MapBase(builder, "Tenants");

        builder.Property(e => e.Slug).IsRequired().HasMaxLength(40);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(120);
        builder.Property(e => e.DefaultLanguage).IsRequired().HasMaxLength(8);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Slug).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_Tenants_Slug", "\"Slug\" ~ '^[a-z0-9-]{3,40}$'"));
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        EntityMapping.MapBase(builder, "Users");

        builder.Property(e => e.TelegramUserId).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(128);
        builder.Property(e => e.LastName).HasMaxLength(128);
        builder.Property(e => e.LanguageCode).HasMaxLength(8);

        builder.HasIndex(e => e.TelegramUserId).IsUnique();
    }
}

public sealed class TenantMemberConfiguration : IEntityTypeConfiguration<TenantMember>
{
    public void Configure(EntityTypeBuilder<TenantMember> builder)
    {
        EntityMapping.MapBase(builder, "TenantMembers");
        EntityMapping.MapTenant(builder);

        builder.Property(e => e.Role).IsRequired().HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();

        builder.HasOne(e => e.Tenant)
            .WithMany(t => t.Members)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
