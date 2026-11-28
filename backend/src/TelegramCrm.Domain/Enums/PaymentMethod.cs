namespace TelegramCrm.Domain.Enums;

/// <summary>Payment method chosen at checkout. Persisted as text with a CHECK constraint.</summary>
public enum PaymentMethod
{
    None = 0,
    PayOnDelivery = 1,
    CardToCard = 2,
}
