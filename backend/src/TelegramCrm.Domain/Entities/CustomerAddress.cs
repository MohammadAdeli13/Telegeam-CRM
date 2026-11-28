using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// A saved delivery address belonging to a shop's customer. At most one address per customer
/// may have <see cref="IsDefault"/> (enforced by a partial unique index).
/// </summary>
public class CustomerAddress : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string? Plaque { get; set; }

    public string? Unit { get; set; }

    public string? PostalCode { get; set; }

    public bool IsDefault { get; set; }

    public Customer? Customer { get; set; }
}
