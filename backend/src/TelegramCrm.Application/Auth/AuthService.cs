using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Auth;

/// <summary>
/// The single authentication use case. Both the REST endpoint and (later) the Telegram
/// bot call this service — no auth logic is duplicated anywhere else.
/// </summary>
public sealed class AuthService
{
    private readonly IAppDbContext _db;
    private readonly TelegramInitDataValidator _validator;
    private readonly IJwtTokenService _tokens;
    private readonly IClock _clock;

    public AuthService(
        IAppDbContext db,
        TelegramInitDataValidator validator,
        IJwtTokenService tokens,
        IClock clock)
    {
        _db = db;
        _validator = validator;
        _tokens = tokens;
        _clock = clock;
    }

    /// <summary>
    /// Validates <c>initData</c>, upserts the platform user, loads memberships and issues a
    /// token. A single membership becomes the active tenant; several leave it null so the
    /// client must call <c>/auth/switch-tenant</c>; none issues a tenant-less token.
    /// </summary>
    public async Task<AuthResponse> LoginWithTelegramAsync(
        string initData,
        string? botToken,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(initData, botToken);
        if (!validation.IsValid || validation.User is null)
        {
            throw new DomainException(
                validation.ErrorCode ?? "init_data_invalid",
                "Telegram initData could not be validated.");
        }

        var telegramUser = validation.User;
        var now = _clock.UtcNow;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUser.Id, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            user = new User
            {
                TelegramUserId = telegramUser.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Users.Add(user);
        }

        user.FirstName = telegramUser.FirstName;
        user.LastName = telegramUser.LastName;
        user.LanguageCode = telegramUser.LanguageCode;
        user.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var memberships = await LoadMembershipsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var activeTenant = memberships.Count == 1 ? memberships[0] : null;

        return BuildResponse(user, memberships, activeTenant);
    }

    /// <summary>
    /// Re-issues a token scoped to <paramref name="tenantId"/>. Throws
    /// <c>not_a_tenant_member</c> (mapped to 403) when the caller has no membership.
    /// </summary>
    public async Task<AuthResponse> SwitchTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("user_not_found", "The authenticated user no longer exists.");

        var memberships = await LoadMembershipsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var target = memberships.FirstOrDefault(m => m.Id == tenantId)
            ?? throw new DomainException(
                "not_a_tenant_member",
                "The caller is not a member of the requested tenant.");

        return BuildResponse(user, memberships, target);
    }

    private async Task<List<TenantSummary>> LoadMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // TenantMember is tenant-scoped, so the global filter would hide every row here.
        // This is the one legitimate cross-tenant read: it is scoped to the caller's own
        // user id and only ever returns shops that user is a member of.
        return await _db.TenantMembers
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .Join(
                _db.Tenants.IgnoreQueryFilters(),
                m => m.TenantId,
                t => t.Id,
                (m, t) => new { Member = m, Tenant = t })
            .OrderBy(x => x.Tenant.Name)
            .Select(x => new TenantSummary(
                x.Tenant.Id,
                x.Tenant.Slug,
                x.Tenant.Name,
                x.Member.Role.ToString()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private AuthResponse BuildResponse(User user, List<TenantSummary> memberships, TenantSummary? activeTenant)
    {
        var claims = new AccessTokenClaims(
            user.Id,
            user.TelegramUserId,
            activeTenant?.Id,
            activeTenant?.Role);

        var (token, expiresAt) = _tokens.Issue(claims);

        return new AuthResponse(
            token,
            expiresAt,
            new AuthUserDto(user.Id, user.TelegramUserId, user.FirstName, user.LastName, user.LanguageCode),
            memberships,
            activeTenant);
    }
}
