using TelegramCrm.Domain.Common;
using TelegramCrm.Domain.Enums;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// Append-only audit row written for every order transition, including creation
/// (<see cref="FromStatus"/> is null for the <c>null → Pending</c> row).
/// There is no update or delete path in the application (contract v1.2 §3.6).
/// </summary>
public class OrderStatusHistory : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }

    public OrderStatus? FromStatus { get; set; }

    public OrderStatus ToStatus { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorRole { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Order? Order { get; set; }
}
