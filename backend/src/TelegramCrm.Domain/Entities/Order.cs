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
    private static readonly Dictionary<OrderStatus, OrderStatus[]> _allowedEdges = new()
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

    /// <summary>Human-friendly sequential order number unique within the tenant (e.g. 1001).</summary>
    public int OrderNumber { get; set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public decimal SubtotalAmount { get; set; }

    public decimal ShippingAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public Guid? ShippingMethodId { get; set; }

    public string? ShippingMethodNameSnapshot { get; set; }

    public bool IsShippingPayAtDestination { get; set; }

    public string? RecipientName { get; set; }

    public string? RecipientPhone { get; set; }

    public string? ShippingProvince { get; set; }

    public string? ShippingCity { get; set; }

    public string? ShippingAddressSnapshot { get; set; }

    public string? ShippingPostalCode { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.NotRequired;

    public string? ReceiptStoragePath { get; set; }

    public DateTimeOffset? ReceiptSubmittedAt { get; set; }

    public DateTimeOffset? PaymentVerifiedAt { get; set; }

    public Guid? PaymentVerifiedByUserId { get; set; }

    public string? PaymentRejectionReason { get; set; }

    public DateTimeOffset? PaymentExpiresAt { get; set; }

    public string? CarrierName { get; set; }

    public string? TrackingCode { get; set; }

    public DateTimeOffset? ShippedAt { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public uint RowVersion { get; set; }

    public Customer? Customer { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();

    /// <summary>True when the target state is one of the legal edges from <paramref name="from"/>.</summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        _allowedEdges.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

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
