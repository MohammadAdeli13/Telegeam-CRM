using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Api.Infrastructure;

/// <summary>
/// Resolves <c>{shopSlug}</c> on <c>/api/v1/storefront/{shopSlug}/**</c> against active
/// tenants and binds the resolved tenant to the scoped <see cref="IStorefrontTenantSetter"/>
/// before any handler runs (contract v1.2 §4.4). Unknown or inactive slugs return 404.
/// </summary>
public sealed class StorefrontTenantFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var rawSlug = context.HttpContext.Request.RouteValues["shopSlug"]?.ToString();
        if (string.IsNullOrWhiteSpace(rawSlug))
        {
            throw new NotFoundException("shop_not_found", "Shop was not found.");
        }

        var slug = rawSlug.Trim().ToLowerInvariant();
        var db = context.HttpContext.RequestServices.GetRequiredService<IAppDbContext>();
        var setter = context.HttpContext.RequestServices.GetRequiredService<IStorefrontTenantSetter>();

        var tenantId = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug && t.IsActive)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!tenantId.HasValue)
        {
            throw new NotFoundException("shop_not_found", $"Shop '{slug}' was not found.");
        }

        setter.SetStorefrontTenant(tenantId.Value);

        return await next(context).ConfigureAwait(false);
    }
}
