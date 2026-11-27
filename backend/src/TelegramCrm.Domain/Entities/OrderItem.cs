using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>
/// One line of an order. Product name and unit price are snapshotted at order time so a
/// later catalogue edit never rewrites history.
/// </summary>
public class OrderItem : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public Order? Order { get; set; }
}
