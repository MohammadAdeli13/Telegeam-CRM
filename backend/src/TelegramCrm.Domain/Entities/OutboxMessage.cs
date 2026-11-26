using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// Transactional outbox row. Written in the same transaction as the state change it
/// describes and delivered asynchronously by the outbox BackgroundService.
/// </summary>
public class OutboxMessage : Entity
{
    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = "{}";

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public Guid? TenantId { get; set; }
}
