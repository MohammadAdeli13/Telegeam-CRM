namespace TelegramCrm.Domain.Enums;

/// <summary>
/// Order lifecycle states. Persisted as text with a CHECK constraint (contract v1.2 §3).
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Preparing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
}
