using TelegramCrm.Domain.Common;
using TelegramCrm.Domain.Enums;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// A customer order. The status may only change through <see cref="TransitionTo"/>,
/// which enforces the state machine of contract v1.2 §3.
/// </summary>
public class Order : Entity, ITenantOwned
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedEdges = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Cancelled],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
    };

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public uint RowVersion { get; set; }

    public Customer? Customer { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();

    /// <summary>True when the target state is one of the legal edges from <paramref name="from"/>.</summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        AllowedEdges.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>True when entering <paramref name="status"/> must restore stock (contract v1.2 §3.4).</summary>
    public static bool RestoresStock(OrderStatus status) => status == OrderStatus.Cancelled;

    /// <summary>
    /// Moves the order to <paramref name="target"/>. Throws
    /// <see cref="InvalidOrderStatusTransitionException"/> for any edge that is not legal,
    /// including any transition out of a terminal state.
    /// </summary>
    public void TransitionTo(OrderStatus target)
    {
        if (!CanTransition(Status, target))
        {
            throw new InvalidOrderStatusTransitionException(Status, target);
        }

        Status = target;
    }
}
