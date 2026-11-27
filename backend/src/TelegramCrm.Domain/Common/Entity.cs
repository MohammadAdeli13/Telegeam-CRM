namespace TelegramCrm.Domain.Common;

/// <summary>
/// Base type for every persisted entity: surrogate key plus UTC audit timestamps.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Marker for entities that belong to exactly one tenant. Every implementation is
/// covered by the EF Core global query filter and the SaveChanges tenant guard.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
