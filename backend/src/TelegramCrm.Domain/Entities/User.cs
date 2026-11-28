using TelegramCrm.Domain.Common;

namespace TelegramCrm.Domain.Entities;

/// <summary>Platform-level identity, keyed by the Telegram user id. Not tenant-scoped.</summary>
public class User : Entity
{
    public long TelegramUserId { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? LanguageCode { get; set; }

    public ICollection<TenantMember> Memberships { get; set; } = new List<TenantMember>();
}
