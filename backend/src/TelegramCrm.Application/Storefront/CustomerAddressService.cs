using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Storefront;

/// <summary>
/// Customer delivery address book scoped strictly to the active storefront tenant and
/// authenticated Telegram customer. Enforces at most one default address per customer.
/// </summary>
public sealed class CustomerAddressService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public CustomerAddressService(
        IAppDbContext db,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IClock clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CustomerAddressDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var telegramUserId = RequireTelegramUserId();

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TelegramUserId == telegramUserId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!customerId.HasValue)
        {
            return [];
        }

        var addresses = await _db.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId.Value)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return addresses.Select(ToDto).ToList();
    }

    public async Task<CustomerAddressDto> CreateAsync(
        CreateCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var telegramUserId = RequireTelegramUserId();
        var customer = await GetOrCreateCustomerAsync(tenantId, telegramUserId, request.Phone, cancellationToken)
            .ConfigureAwait(false);

        var hasAny = await _db.CustomerAddresses
            .AnyAsync(a => a.CustomerId == customer.Id, cancellationToken)
            .ConfigureAwait(false);

        var makeDefault = !hasAny || request.IsDefault == true;
        var now = _clock.UtcNow;
        var isInMemory = IsInMemory();
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        CustomerAddress address;
        try
        {
            if (makeDefault && hasAny)
            {
                await DemoteDefaultAsync(customer.Id, now, isInMemory, cancellationToken).ConfigureAwait(false);
            }

            address = new CustomerAddress
            {
                TenantId = tenantId,
                CustomerId = customer.Id,
                Title = request.Title.Trim(),
                RecipientName = request.RecipientName.Trim(),
                Phone = request.Phone.Trim(),
                Province = request.Province.Trim(),
                City = request.City.Trim(),
                AddressLine = request.AddressLine.Trim(),
                Plaque = string.IsNullOrWhiteSpace(request.Plaque) ? null : request.Plaque.Trim(),
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim(),
                IsDefault = makeDefault,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _db.CustomerAddresses.Add(address);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        return ToDto(address);
    }

    public async Task<CustomerAddressDto> UpdateAsync(
        Guid addressId,
        UpdateCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var customerId = await RequireCustomerIdAsync(addressId, cancellationToken).ConfigureAwait(false);

        var now = _clock.UtcNow;
        var isInMemory = IsInMemory();
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        CustomerAddress address;
        try
        {
            address = await _db.CustomerAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw NotFound(addressId);

            if (request.Title is not null)
            {
                address.Title = request.Title.Trim();
            }

            if (request.RecipientName is not null)
            {
                address.RecipientName = request.RecipientName.Trim();
            }

            if (request.Phone is not null)
            {
                address.Phone = request.Phone.Trim();
            }

            if (request.Province is not null)
            {
                address.Province = request.Province.Trim();
            }

            if (request.City is not null)
            {
                address.City = request.City.Trim();
            }

            if (request.AddressLine is not null)
            {
                address.AddressLine = request.AddressLine.Trim();
            }

            if (request.Plaque is not null)
            {
                address.Plaque = string.IsNullOrWhiteSpace(request.Plaque) ? null : request.Plaque.Trim();
            }

            if (request.Unit is not null)
            {
                address.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
            }

            if (request.PostalCode is not null)
            {
                address.PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim();
            }

            if (request.IsDefault == true && !address.IsDefault)
            {
                await DemoteDefaultAsync(customerId, now, isInMemory, cancellationToken).ConfigureAwait(false);
                address.IsDefault = true;
            }

            address.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        return ToDto(address);
    }

    public async Task DeleteAsync(Guid addressId, CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var customerId = await RequireCustomerIdAsync(addressId, cancellationToken).ConfigureAwait(false);

        var address = await _db.CustomerAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(addressId);

        var wasDefault = address.IsDefault;
        _db.CustomerAddresses.Remove(address);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (wasDefault)
        {
            var replacement = await _db.CustomerAddresses
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (replacement is not null)
            {
                replacement.IsDefault = true;
                replacement.UpdatedAt = _clock.UtcNow;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<CustomerAddressDto> SetDefaultAsync(Guid addressId, CancellationToken cancellationToken = default)
    {
        _ = RequireTenantId();
        var customerId = await RequireCustomerIdAsync(addressId, cancellationToken).ConfigureAwait(false);

        var target = await _db.CustomerAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(addressId);

        if (target.IsDefault)
        {
            return ToDto(target);
        }

        var now = _clock.UtcNow;
        var isInMemory = IsInMemory();
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await DemoteDefaultAsync(customerId, now, isInMemory, cancellationToken).ConfigureAwait(false);

            if (isInMemory)
            {
                target.IsDefault = true;
                target.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _db.CustomerAddresses
                    .Where(a => a.Id == addressId && a.CustomerId == customerId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(a => a.IsDefault, true)
                            .SetProperty(a => a.UpdatedAt, now),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        target.IsDefault = true;
        target.UpdatedAt = now;
        return ToDto(target);
    }

    private async Task DemoteDefaultAsync(
        Guid customerId,
        DateTimeOffset now,
        bool isInMemory,
        CancellationToken cancellationToken)
    {
        if (isInMemory)
        {
            var currentDefaults = await _db.CustomerAddresses
                .Where(a => a.CustomerId == customerId && a.IsDefault)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in currentDefaults)
            {
                row.IsDefault = false;
                row.UpdatedAt = now;
            }
        }
        else
        {
            await _db.CustomerAddresses
                .Where(a => a.CustomerId == customerId && a.IsDefault)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(a => a.IsDefault, false)
                        .SetProperty(a => a.UpdatedAt, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<Guid> RequireCustomerIdAsync(Guid addressId, CancellationToken cancellationToken)
    {
        var telegramUserId = RequireTelegramUserId();
        return await _db.Customers
            .AsNoTracking()
            .Where(c => c.TelegramUserId == telegramUserId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotFound(addressId);
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        Guid tenantId,
        long telegramUserId,
        string? phone,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Customers
            .FirstOrDefaultAsync(c => c.TelegramUserId == telegramUserId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, cancellationToken)
            .ConfigureAwait(false);

        var displayName = user is null
            ? null
            : string.Join(" ", new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var now = _clock.UtcNow;
        var customer = new Customer
        {
            TenantId = tenantId,
            TelegramUserId = telegramUserId,
            TelegramUsername = TelegramUsernameHelper.Normalize(user?.Username),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return customer;
    }

    private Guid RequireTenantId() =>
        _tenantContext.TenantId is { } id && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("CustomerAddressService ran without a resolved storefront tenant.");

    private long RequireTelegramUserId() =>
        _currentUser.TelegramUserId
        ?? throw new UnauthorizedAccessException("Authenticated Telegram user is required.");

    private bool IsInMemory() =>
        string.Equals(_db.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal);

    private static NotFoundException NotFound(Guid id) =>
        new("address_not_found", $"Address '{id}' was not found.");

    private static CustomerAddressDto ToDto(CustomerAddress a) =>
        new(
            a.Id,
            a.Title,
            a.RecipientName,
            a.Phone,
            a.Province,
            a.City,
            a.AddressLine,
            a.Plaque,
            a.Unit,
            a.PostalCode,
            a.IsDefault,
            a.CreatedAt,
            a.UpdatedAt);
}
