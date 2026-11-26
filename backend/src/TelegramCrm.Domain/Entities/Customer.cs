using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>A shop's customer. UNIQUE (TenantId, TelegramUserId).</summary>
public class Customer : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public long TelegramUserId { get; set; }

    public string? DisplayName { get; set; }

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public ICollection<CustomerTag> Tags { get; set; } = new List<CustomerTag>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
