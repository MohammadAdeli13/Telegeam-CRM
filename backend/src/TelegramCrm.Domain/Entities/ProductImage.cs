using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// One image of a product. At most one row per product may have <see cref="IsPrimary"/>
/// (partial unique index). <see cref="StoragePath"/> is a tenant-namespaced key produced
/// by <c>IFileStorageService</c>.
/// </summary>
public class ProductImage : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    public Product? Product { get; set; }
}
