using AzureServices.Core.Models;

namespace AzureServices.Core.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadBlobAsync(string containerName, string blobName, Stream content, string contentType, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
    Task<(Stream Content, string ContentType, IDictionary<string, string> Metadata)> DownloadBlobAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageFileItem>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteBlobAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    string GenerateSasUri(string containerName, string blobName, TimeSpan validity, bool canWrite = false);
    Task SetAccessTierAsync(string containerName, string blobName, string tier, CancellationToken cancellationToken = default);
}
