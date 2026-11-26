using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>Join row between a customer and a tag. Composite PK (CustomerId, TagId).</summary>
public class CustomerTag : ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid TagId { get; set; }

    public Customer? Customer { get; set; }

    public Tag? Tag { get; set; }
}
