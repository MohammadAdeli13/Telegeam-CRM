using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>Customer tag. UNIQUE (TenantId, Name).</summary>
public class Tag : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<CustomerTag> Customers { get; set; } = new List<CustomerTag>();
}
