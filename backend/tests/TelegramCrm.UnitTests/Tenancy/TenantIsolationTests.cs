using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Infrastructure.Persistence;

namespace TelegramCrm.UnitTests.Tenancy;

/// <summary>
/// Contract v1.2 §6.9 — the mandatory "tenant A cannot read/write tenant B" suite.
/// Exercises the global query filter and the SaveChanges tenant guard directly against
/// the real <see cref="CrmDbContext"/> mapping.
/// </summary>
public sealed class TenantIsolationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task QueryFilter_HidesTenantBRowsFromTenantA()
    {
        var database = Guid.NewGuid().ToString();
        var context = new MutableTenantContext();

        await using (var seed = CreateContext(database, context))
        {
            context.SetTenant(TenantA);
            seed.Categories.Add(NewCategory(TenantA, "a-category"));
            await seed.SaveChangesAsync();

            context.SetTenant(TenantB);
            seed.Categories.Add(NewCategory(TenantB, "b-category"));
            await seed.SaveChangesAsync();
        }

        await using var read = CreateContext(database, context);

        context.SetTenant(TenantA);
        var visibleToA = await read.Categories.ToListAsync();

        Assert.Single(visibleToA);
        Assert.All(visibleToA, c => Assert.Equal(TenantA, c.TenantId));
        Assert.DoesNotContain(visibleToA, c => c.Slug == "b-category");

        context.SetTenant(TenantB);
        var visibleToB = await read.Categories.ToListAsync();

        Assert.Single(visibleToB);
        Assert.All(visibleToB, c => Assert.Equal(TenantB, c.TenantId));
    }

    [Fact]
    public async Task QueryFilter_WithNoResolvedTenant_ReturnsNothing()
    {
        var database = Guid.NewGuid().ToString();
        var context = new MutableTenantContext();

        await using (var seed = CreateContext(database, context))
        {
            context.SetTenant(TenantA);
            seed.Products.Add(NewProduct(TenantA));
            await seed.SaveChangesAsync();
        }

        await using var read = CreateContext(database, context);
        context.SetTenant(null);

        Assert.Empty(await read.Products.ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_RejectsAnInsertWithoutAResolvedTenant()
    {
        var context = new MutableTenantContext();
        await using var db = CreateContext(Guid.NewGuid().ToString(), context);

        context.SetTenant(null);
        db.Categories.Add(NewCategory(TenantA, "orphan"));

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_RejectsAnInsertForAnotherTenant()
    {
        var context = new MutableTenantContext();
        await using var db = CreateContext(Guid.NewGuid().ToString(), context);

        context.SetTenant(TenantA);
        db.Categories.Add(NewCategory(TenantB, "smuggled"));

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_StampsTheResolvedTenantOnInsert()
    {
        var context = new MutableTenantContext();
        await using var db = CreateContext(Guid.NewGuid().ToString(), context);

        context.SetTenant(TenantA);
        var category = NewCategory(Guid.Empty, "stamped");
        db.Categories.Add(category);

        await db.SaveChangesAsync();

        Assert.Equal(TenantA, category.TenantId);
    }

    [Fact]
    public async Task SaveChanges_RejectsChangingTenantIdOnUpdate()
    {
        var database = Guid.NewGuid().ToString();
        var context = new MutableTenantContext();
        Guid categoryId;

        await using (var seed = CreateContext(database, context))
        {
            context.SetTenant(TenantA);
            var category = NewCategory(TenantA, "movable");
            seed.Categories.Add(category);
            await seed.SaveChangesAsync();
            categoryId = category.Id;
        }

        await using var db = CreateContext(database, context);
        context.SetTenant(TenantA);

        var loaded = await db.Categories.SingleAsync(c => c.Id == categoryId);
        loaded.TenantId = TenantB;

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CrossTenantLookup_ReturnsNull_NotAnotherTenantsRow()
    {
        var database = Guid.NewGuid().ToString();
        var context = new MutableTenantContext();
        Guid tenantBProductId;

        await using (var seed = CreateContext(database, context))
        {
            context.SetTenant(TenantB);
            var product = NewProduct(TenantB);
            seed.Products.Add(product);
            await seed.SaveChangesAsync();
            tenantBProductId = product.Id;
        }

        await using var read = CreateContext(database, context);
        context.SetTenant(TenantA);

        // The contract requires 404 (not 403) for cross-tenant access: the row is invisible.
        var found = await read.Products.FirstOrDefaultAsync(p => p.Id == tenantBProductId);

        Assert.Null(found);
    }

    private static CrmDbContext CreateContext(string databaseName, ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new TenantGuardInterceptor(tenantContext))
            .Options;

        return new CrmDbContext(options, tenantContext);
    }

    private static Category NewCategory(Guid tenantId, string slug) => new()
    {
        TenantId = tenantId,
        Name = slug,
        Slug = slug,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static Product NewProduct(Guid tenantId) => new()
    {
        TenantId = tenantId,
        CategoryId = Guid.NewGuid(),
        Name = "Widget",
        Price = 10.00m,
        Stock = 5,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private sealed class MutableTenantContext : ITenantContext
    {
        public Guid? TenantId { get; private set; }

        public bool IsResolved => TenantId.HasValue;

        public void SetTenant(Guid? tenantId) => TenantId = tenantId;
    }
}
