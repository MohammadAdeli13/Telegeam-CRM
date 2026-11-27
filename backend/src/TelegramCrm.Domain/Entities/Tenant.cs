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

    public string Currency { get; set; } = "IRR";

    public bool IsActive { get; set; } = true;

    public ICollection<TenantMember> Members { get; set; } = new List<TenantMember>();
}
