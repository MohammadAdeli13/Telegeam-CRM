using TelegramCrm.Domain.Exceptions;

namespace TelegramCrm.Application.Common;

/// <summary>
/// Shared magic-byte and size validator for all tenant image uploads (product images,
/// shop logo, and default product image).
/// </summary>
public static class ImageUploadValidator
{
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
        };

    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static async Task<MemoryStream> ValidateAndBufferAsync(
        string? contentType,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
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

        var buffer = new MemoryStream();
        try
        {
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
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static string GetExtensionForContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin",
        };

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
}
