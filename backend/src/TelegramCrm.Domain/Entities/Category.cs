using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>Product category. UNIQUE (TenantId, Slug).</summary>
public class Category : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    public string Slug { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
