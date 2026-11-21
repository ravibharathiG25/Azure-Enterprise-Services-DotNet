using AzureServices.Core.Models;

namespace AzureServices.Core.Interfaces;

public interface IFileShareStorageService
{
    Task CreateShareIfNotExistsAsync(string shareName, CancellationToken cancellationToken = default);
    Task UploadFileAsync(string shareName, string directoryPath, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string shareName, string directoryPath, string fileName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageFileItem>> ListFilesAndDirectoriesAsync(string shareName, string? directoryPath = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string shareName, string directoryPath, string fileName, CancellationToken cancellationToken = default);
}
