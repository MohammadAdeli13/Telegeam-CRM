using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// A shop. Root of tenancy; <see cref="Slug"/> is the globally unique storefront key.
/// There is no per-tenant bot token in the MVP (contract v1.2 §2).
/// </summary>
public class Tenant : Entity
{
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DefaultLanguage { get; set; } = "fa";

    public string Currency { get; set; } = "IRT";

    public bool IsActive { get; set; } = true;

    public string? LogoStoragePath { get; set; }

    public string? DefaultProductImageStoragePath { get; set; }

    /// <summary>
    /// When <c>false</c> (default), non-card-to-card orders auto-confirm on placement and
    /// card-to-card orders auto-confirm upon receipt verification.
    /// </summary>
    public bool RequireOrderApproval { get; set; }

    /// <summary>Next sequential human-friendly order number issued for this shop (starts at 1001).</summary>
    public int NextOrderNumber { get; set; } = 1001;

    /// <summary>Minutes before an unpaid card-to-card order expires and restores stock.</summary>
    public int PaymentExpiryMinutes { get; set; } = 60;

    public bool EnableCardToCard { get; set; }

    public bool EnablePayOnDelivery { get; set; } = true;

    public string? CardNumber { get; set; }

    public string? CardHolderName { get; set; }

    public string? BankName { get; set; }

    public ICollection<TenantMember> Members { get; set; } = new List<TenantMember>();
}
