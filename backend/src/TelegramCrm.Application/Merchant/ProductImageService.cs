using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Abstractions;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;
using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Merchant;

/// <summary>
/// Product image management (contract v1.2 §4.3). Uploads go through
/// <see cref="IFileStorageService"/>, which namespaces every path by tenant (§6.7), validates
/// image magic bytes against the declared content type, and performs primary-image swaps in
/// one transaction so the partial unique index <c>(ProductId) WHERE IsPrimary</c> is never violated.
/// </summary>
public sealed class ProductImageService : MerchantServiceBase
{
    /// <summary>Accepted image content types. Anything else is rejected before it is stored.</summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
        };

    /// <summary>Maximum accepted upload size (5 MiB).</summary>
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    /// <summary>Maximum length of <c>AltText</c>, matching the database column.</summary>
    public const int MaxAltTextLength = 300;

    private readonly IFileStorageService _storage;

    public ProductImageService(
        IAppDbContext db,
        ITenantContext tenantContext,
        IClock clock,
        IFileStorageService storage)
        : base(db, tenantContext, clock)
    {
        _storage = storage;
    }

    public async Task<IReadOnlyList<ProductImageDto>> ListAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken).ConfigureAwait(false);

        var images = await Db.ProductImages
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return images.Select(ToDto).ToList();
    }

    /// <summary>
    /// Stores an uploaded image and appends it to the product. The first image of a product
    /// automatically becomes primary, so a product with images always has exactly one.
    /// </summary>
    public async Task<ProductImageDto> UploadAsync(
        Guid productId,
        string fileName,
        string? contentType,
        long length,
        Stream content,
        string? altText,
        CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken).ConfigureAwait(false);

        if (altText is not null && altText.Length > MaxAltTextLength)
        {
            throw new DomainException(
                "invalid_alt_text",
                $"AltText must not exceed {MaxAltTextLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            throw new DomainException(
                "unsupported_media_type",
                $"Content type '{contentType}' is not allowed. Use one of: {string.Join(", ", AllowedContentTypes)}.");
        }

        if (length <= 0)
        {
            throw new DomainException("empty_file", "The uploaded file is empty.");
        }

        if (length > MaxFileSizeBytes)
        {
            throw new DomainException(
                "file_too_large",
                $"The uploaded file exceeds the {MaxFileSizeBytes / (1024 * 1024)} MiB limit.");
        }

        using var buffer = new MemoryStream();
        await CopyWithLimitAsync(content, buffer, MaxFileSizeBytes, cancellationToken).ConfigureAwait(false);
        if (buffer.Length == 0)
        {
            throw new DomainException("empty_file", "The uploaded file is empty.");
        }

        var payload = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        if (!MatchesMagicBytes(contentType, payload))
        {
            throw new DomainException(
                "unsupported_media_type",
                $"The file contents do not match the declared content type '{contentType}'.");
        }

        buffer.Position = 0;

        var tenantId = TenantId;
        var storagePath = await _storage
            .SaveAsync(tenantId, "products", productId, fileName, buffer, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var hasPrimary = await Db.ProductImages
                .AnyAsync(i => i.ProductId == productId && i.IsPrimary, cancellationToken)
                .ConfigureAwait(false);

            var nextSortOrder = await Db.ProductImages
                .Where(i => i.ProductId == productId)
                .Select(i => (int?)i.SortOrder)
                .MaxAsync(cancellationToken)
                .ConfigureAwait(false) ?? -1;

            var now = Clock.UtcNow;
            var image = new ProductImage
            {
                TenantId = tenantId,
                ProductId = productId,
                StoragePath = storagePath,
                AltText = altText,
                SortOrder = nextSortOrder + 1,
                IsPrimary = !hasPrimary,
                CreatedAt = now,
                UpdatedAt = now,
            };

            Db.ProductImages.Add(image);
            await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return ToDto(image);
        }
        catch
        {
            await _storage.DeleteAsync(tenantId, storagePath, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ProductImageDto> UpdateAsync(
        Guid productId,
        Guid imageId,
        UpdateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var image = await FindImageAsync(productId, imageId, cancellationToken).ConfigureAwait(false);

        if (request.AltText is not null)
        {
            image.AltText = request.AltText;
        }

        if (request.SortOrder.HasValue)
        {
            image.SortOrder = request.SortOrder.Value;
        }

        image.UpdatedAt = Clock.UtcNow;
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(image);
    }

    public async Task DeleteAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default)
    {
        var image = await FindImageAsync(productId, imageId, cancellationToken).ConfigureAwait(false);
        var wasPrimary = image.IsPrimary;

        Db.ProductImages.Remove(image);
        await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Deleting the primary image must not leave the product without one.
        if (wasPrimary)
        {
            var replacement = await Db.ProductImages
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (replacement is not null)
            {
                replacement.IsPrimary = true;
                replacement.UpdatedAt = Clock.UtcNow;
                await Db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await _storage.DeleteAsync(TenantId, image.StoragePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Makes <paramref name="imageId"/> the product's only primary image. The demotion of the
    /// previous primary and the promotion happen in one transaction, so the partial unique
    /// index is satisfied at every commit boundary.
    /// </summary>
    public async Task<ProductImageDto> SetPrimaryAsync(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var target = await FindImageAsync(productId, imageId, cancellationToken).ConfigureAwait(false);

        if (target.IsPrimary)
        {
            return ToDto(target);
        }

        await using var transaction = await Db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = Clock.UtcNow;

        // Demote first: the unique index allows at most one primary row per product.
        await Db.ProductImages
            .Where(i => i.ProductId == productId && i.IsPrimary)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.IsPrimary, false)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        await Db.ProductImages
            .Where(i => i.Id == imageId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.IsPrimary, true)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        target.IsPrimary = true;
        target.UpdatedAt = now;
        return ToDto(target);
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        MemoryStream destination,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var chunk = new byte[16 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new DomainException(
                    "file_too_large",
                    $"The uploaded file exceeds the {maxBytes / (1024 * 1024)} MiB limit.");
            }

            destination.Write(chunk, 0, read);
        }
    }

    private static bool MatchesMagicBytes(string contentType, ReadOnlySpan<byte> bytes)
    {
        if (string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        }

        if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        }

        if (string.Equals(contentType, "image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 6 &&
                bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38 &&
                (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61;
        }

        if (string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
        }

        return false;
    }

    private async Task<ProductImage> FindImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        await EnsureProductExistsAsync(productId, cancellationToken).ConfigureAwait(false);

        return await Db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(
                "product_image_not_found",
                $"Image '{imageId}' was not found on product '{productId}'.");
    }

    private async Task EnsureProductExistsAsync(Guid productId, CancellationToken cancellationToken)
    {
        var exists = await Db.Products
            .AnyAsync(p => p.Id == productId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");
        }
    }

    private ProductImageDto ToDto(ProductImage i) =>
        new(i.Id, i.ProductId, i.StoragePath, _storage.GetPublicUrl(i.StoragePath), i.AltText, i.SortOrder, i.IsPrimary);
}
