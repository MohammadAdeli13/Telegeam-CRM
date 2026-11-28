namespace TelegramCrm.Domain.Enums;

/// <summary>Payment verification status independent of order fulfillment status.</summary>
public enum PaymentStatus
{
    NotRequired = 0,
    AwaitingReceipt = 1,
    ReceiptSubmitted = 2,
    Verified = 3,
    Rejected = 4,
}
