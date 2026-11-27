namespace TelegramCrm.Domain.Enums;

/// <summary>
/// Role of a user inside a tenant. <c>PlatformAdmin</c> is deliberately absent:
/// it is a platform-level principal served from <c>/api/v1/platform/**</c> and is
/// never a <c>TenantMember</c> (contract v1.2 §1.4).
/// </summary>
public enum MemberRole
{
    MerchantOwner = 0,
    MerchantStaff = 1,
}
