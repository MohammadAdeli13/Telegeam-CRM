using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Common;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Infrastructure.Persistence;

/// <summary>
/// Rejects any write that would break tenant isolation (contract v1.2 §6.3):
/// <list type="bullet">
/// <item>an insert of a tenant-owned row without a resolvable tenant throws;</item>
/// <item>an insert whose <c>TenantId</c> differs from the resolved tenant throws;</item>
/// <item>an update that attempts to change <c>TenantId</c> throws.</item>
/// </list>
/// There is therefore no code path that writes a tenant-scoped row into the wrong tenant.
/// </summary>
public sealed class TenantGuardInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantGuardInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var resolved = _tenantContext.TenantId;

        // Onboarding bootstrap: when a brand-new Tenant is inserted in this same unit of
        // work, its first membership rows are legitimate even though the caller has no
        // active tenant yet. Only tenants created in THIS SaveChanges are accepted.
        var bootstrappedTenants = context.ChangeTracker
            .Entries<Tenant>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.Id)
            .ToHashSet();

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (!resolved.HasValue || resolved.Value == Guid.Empty)
                    {
                        if (entry.Entity.TenantId != Guid.Empty &&
                            bootstrappedTenants.Contains(entry.Entity.TenantId))
                        {
                            break;
                        }

                        throw new TenantIsolationException(
                            $"Cannot insert {entry.Entity.GetType().Name} without a resolved tenant.");
                    }

                    if (entry.Entity.TenantId == Guid.Empty)
                    {
                        entry.Entity.TenantId = resolved.Value;
                    }
                    else if (entry.Entity.TenantId != resolved.Value)
                    {
                        throw new TenantIsolationException(
                            $"Cannot insert {entry.Entity.GetType().Name} for tenant {entry.Entity.TenantId} " +
                            $"while the request is scoped to {resolved.Value}.");
                    }

                    break;

                case EntityState.Modified:
                    var original = entry.Property(nameof(ITenantOwned.TenantId)).OriginalValue;
                    if (original is Guid originalTenant && originalTenant != entry.Entity.TenantId)
                    {
                        throw new TenantIsolationException(
                            $"TenantId of {entry.Entity.GetType().Name} is immutable.");
                    }

                    break;

                default:
                    break;
            }
        }
    }
}

/// <summary>Raised when a write would cross a tenant boundary.</summary>
public sealed class TenantIsolationException : Exception
{
    public TenantIsolationException(string message)
        : base(message)
    {
    }
}
