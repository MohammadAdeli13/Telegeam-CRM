using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// A sellable product. <see cref="RowVersion"/> is the concurrency token used by the
/// atomic stock decrement (contract v1.2 §3.3). Images live in <see cref="ProductImage"/>.
/// </summary>
public class Product : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;

    public uint RowVersion { get; set; }

    public Category? Category { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
