using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// A merchant-configured delivery option (courier, express post, Tipax, or pay-at-destination).
/// </summary>
public class ShippingMethod : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public decimal? FreeAboveAmount { get; set; }

    public bool IsPayAtDestination { get; set; }

    public string? EstimatedDaysText { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
