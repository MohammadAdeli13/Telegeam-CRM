using TelegramCrm.Domain.Common;
using TelegramCrm.Domain.Enums;

namespace TelegramCrm.Domain.Entities;

/// <summary>Membership of a user in a shop. UNIQUE (TenantId, UserId).</summary>
public class TenantMember : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public MemberRole Role { get; set; }

    public Tenant? Tenant { get; set; }

    public User? User { get; set; }
}
