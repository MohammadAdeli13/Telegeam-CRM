namespace TelegramCrm.Application.Abstractions;

/// <summary>
/// Tenant-namespaced file storage. Every path is produced as
/// <c>/{tenantId}/{entity}/{id}</c> so one tenant can never address another's object
/// (contract v1.2 §6.7).
/// </summary>
public interface IFileStorageService
{
    Task<string> SaveAsync(
        Guid tenantId,
        string entity,
        Guid entityId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, string storagePath, CancellationToken cancellationToken = default);

    string GetPublicUrl(string storagePath);
}
