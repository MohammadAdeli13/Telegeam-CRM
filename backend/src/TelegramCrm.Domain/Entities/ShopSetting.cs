using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>Key/value shop setting. UNIQUE (TenantId, Key); <see cref="Value"/> is jsonb.</summary>
public class ShopSetting : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = "{}";
}
