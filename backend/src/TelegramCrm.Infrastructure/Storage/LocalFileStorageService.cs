using Microsoft.Extensions.Options;
using TelegramCrm.Application.Abstractions;

namespace TelegramCrm.Infrastructure.Storage;

/// <summary>Local-volume implementation of <see cref="IFileStorageService"/> for the MVP.</summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    public LocalFileStorageService(IOptions<FileStorageOptions> options)
    {
        _rootPath = options.Value.RootPath;
        _publicBaseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
    }

    public async Task<string> SaveAsync(
        Guid tenantId,
        string entity,
        Guid entityId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        var relative = BuildRelativePath(tenantId, entity, entityId, safeName);
        var absolute = Path.Combine(_rootPath, relative.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using var target = File.Create(absolute);
        await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);

        return relative;
    }

    public Task DeleteAsync(Guid tenantId, string storagePath, CancellationToken cancellationToken = default)
    {
        // Refuse any path that is not inside this tenant's namespace.
        if (!storagePath.StartsWith($"{tenantId}/", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Storage path does not belong to the tenant.");
        }

        var absolute = Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string storagePath) => $"{_publicBaseUrl}/{storagePath}";

    private static string BuildRelativePath(Guid tenantId, string entity, Guid entityId, string fileName) =>
        $"{tenantId}/{entity}/{entityId}/{fileName}";
}

/// <summary>Local storage configuration.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; set; } = "storage";

    public string PublicBaseUrl { get; set; } = "/files";
}
