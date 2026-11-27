using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Merchant;

/// <summary>
/// Customer and tag management for the merchant back-office (contract v1.2 §4.3).
/// Uniqueness is tenant-scoped — <c>(TenantId, TelegramUserId)</c> and <c>(TenantId, Name)</c> —
/// so two shops may legitimately hold the same customer or tag name.
/// </summary>
public sealed class CustomerService : MerchantServiceBase
{
    public CustomerService(IAppDbContext db, ITenantContext tenantContext, IClock clock)
        : base(db, tenantContext, clock)
    {
    }

    public async Task<PagedResult<CustomerDto>> ListAsync(
        int? page,
        int? pageSize,
        string? q,
        Guid? tagId,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = Db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = SearchPattern.Contains(q);
#pragma warning disable CA1304, CA1311
            query = query.Where(c =>
                (c.DisplayName != null && EF.Functions.Like(c.DisplayName.ToLower(), pattern, SearchPattern.EscapeChar)) ||
                (c.Phone != null && EF.Functions.Like(c.Phone.ToLower(), pattern, SearchPattern.EscapeChar)));
#pragma warning restore CA1304, CA1311
        }

        if (tagId.HasValue)
        {
            query = query.Where(c => c.Tags.Any(t => t.TagId == tagId.Value));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var customers = await query
            .OrderBy(c => c.DisplayName)
            .ThenBy(c => c.TelegramUserId)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .Include(c => c.Tags)
            .ThenInclude(t => t.Tag)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<CustomerDto>(
            customers.Select(ToDto).ToList(),
            total,
            normalizedPage,
            normalizedSize);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await Db.Customers
            .AsNoTracking()
            .Include(c => c.Tags)
            .ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        return ToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var duplicate = await Db.Customers
            .AnyAsync(c => c.TelegramUserId == request.TelegramUserId, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate)
        {
            throw new DomainException(
                "customer_exists",
                $"A customer with Telegram user id {request.TelegramUserId} already exists in this shop.");
        }

        var now = Clock.UtcNow;
        var customer = new Customer
        {
            TenantId = TenantId,
            TelegramUserId = request.TelegramUserId,
            DisplayName = request.DisplayName?.Trim(),
            Phone = request.Phone?.Trim(),
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Db.Customers.Add(customer);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await Db.Customers
            .Include(c => c.Tags)
            .ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(id);

        if (request.DisplayName is not null)
        {
            customer.DisplayName = request.DisplayName.Trim();
        }

        if (request.Phone is not null)
        {
            customer.Phone = request.Phone.Trim();
        }

        if (request.Notes is not null)
        {
            customer.Notes = request.Notes;
        }

        customer.UpdatedAt = Clock.UtcNow;
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(customer);
    }

    /// <summary>A customer's order history, newest first.</summary>
    public async Task<PagedResult<CustomerOrderDto>> ListOrdersAsync(
        Guid customerId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        var exists = await Db.Customers
            .AnyAsync(c => c.Id == customerId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw NotFound(customerId);
        }

        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);

        var query = Db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(o => o.PlacedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .Select(o => new CustomerOrderDto(o.Id, o.Status.ToString(), o.TotalAmount, o.Note, o.PlacedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<CustomerOrderDto>(items, total, normalizedPage, normalizedSize);
    }

    public async Task<IReadOnlyList<TagDto>> ListTagsAsync(CancellationToken cancellationToken = default)
    {
        return await Db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TagDto> CreateTagAsync(
        CreateTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        var duplicate = await Db.Tags
            .AnyAsync(t => t.Name == name, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate)
        {
            throw new DomainException("tag_exists", $"The tag '{name}' already exists in this shop.");
        }

        var now = Clock.UtcNow;
        var tag = new Tag
        {
            TenantId = TenantId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Db.Tags.Add(tag);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TagDto(tag.Id, tag.Name, tag.CreatedAt);
    }

    public async Task DeleteTagAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        var tag = await Db.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("tag_not_found", $"Tag '{tagId}' was not found.");

        // CustomerTag rows cascade on delete, so the tag simply detaches from every customer.
        Db.Tags.Remove(tag);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CustomerDto> AttachTagAsync(
        Guid customerId,
        AttachTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await Db.Customers
            .Include(c => c.Tags)
            .ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(customerId);

        var tag = await Db.Tags
            .FirstOrDefaultAsync(t => t.Id == request.TagId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("tag_not_found", $"Tag '{request.TagId}' was not found.");

        if (customer.Tags.All(t => t.TagId != tag.Id))
        {
            customer.Tags.Add(new CustomerTag
            {
                TenantId = TenantId,
                CustomerId = customer.Id,
                TagId = tag.Id,
            });

            customer.UpdatedAt = Clock.UtcNow;
            await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ToDto(customer);
    }

    public async Task<CustomerDto> DetachTagAsync(
        Guid customerId,
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        var customer = await Db.Customers
            .Include(c => c.Tags)
            .ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(customerId);

        var link = customer.Tags.FirstOrDefault(t => t.TagId == tagId);
        if (link is not null)
        {
            customer.Tags.Remove(link);
            customer.UpdatedAt = Clock.UtcNow;
            await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ToDto(customer);
    }

    private static NotFoundException NotFound(Guid id) =>
        new("customer_not_found", $"Customer '{id}' was not found.");

    private static CustomerDto ToDto(Customer c) =>
        new(
            c.Id,
            c.TelegramUserId,
            c.DisplayName,
            c.Phone,
            c.Notes,
            c.Tags
                .Where(t => t.Tag is not null)
                .Select(t => new TagDto(t.Tag!.Id, t.Tag.Name, t.Tag.CreatedAt))
                .OrderBy(t => t.Name)
                .ToList(),
            c.CreatedAt,
            c.UpdatedAt);
}
