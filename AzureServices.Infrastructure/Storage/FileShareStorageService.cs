using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.Storage;

public class FileShareStorageService : IFileShareStorageService
{
    private readonly ShareServiceClient _shareServiceClient;
    private readonly ILogger<FileShareStorageService> _logger;

    public FileShareStorageService(ShareServiceClient shareServiceClient, ILogger<FileShareStorageService> logger)
    {
        _shareServiceClient = shareServiceClient;
        _logger = logger;
    }

    public async Task CreateShareIfNotExistsAsync(string shareName, CancellationToken cancellationToken = default)
    {
        var shareClient = _shareServiceClient.GetShareClient(shareName);
        await shareClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task UploadFileAsync(
        string shareName,
        string directoryPath,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var shareClient = _shareServiceClient.GetShareClient(shareName);
        await shareClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        ShareDirectoryClient dirClient = shareClient.GetRootDirectoryClient();
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            var parts = directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                dirClient = dirClient.GetSubdirectoryClient(part);
                await dirClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
        }

        var fileClient = dirClient.GetFileClient(fileName);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        long length = content.Length;
        _logger.LogInformation("Uploading file '{FileName}' ({Length} bytes) to File Share '{ShareName}/{DirectoryPath}'",
            fileName, length, shareName, directoryPath);

        await fileClient.CreateAsync(length, cancellationToken: cancellationToken);
        await fileClient.UploadRangeAsync(new Azure.HttpRange(0, length), content, cancellationToken: cancellationToken);
    }

    public async Task<Stream> DownloadFileAsync(
        string shareName,
        string directoryPath,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var shareClient = _shareServiceClient.GetShareClient(shareName);
        ShareDirectoryClient dirClient = shareClient.GetRootDirectoryClient();
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            var parts = directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                dirClient = dirClient.GetSubdirectoryClient(part);
            }
        }

        var fileClient = dirClient.GetFileClient(fileName);
        _logger.LogInformation("Downloading file '{FileName}' from File Share '{ShareName}/{DirectoryPath}'",
            fileName, shareName, directoryPath);

        ShareFileDownloadInfo download = await fileClient.DownloadAsync(cancellationToken: cancellationToken);
        var memoryStream = new MemoryStream();
        await download.Content.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<IReadOnlyList<StorageFileItem>> ListFilesAndDirectoriesAsync(
        string shareName,
        string? directoryPath = null,
        CancellationToken cancellationToken = default)
    {
        var shareClient = _shareServiceClient.GetShareClient(shareName);
        if (!await shareClient.ExistsAsync(cancellationToken))
        {
            return Array.Empty<StorageFileItem>();
        }

        ShareDirectoryClient dirClient = shareClient.GetRootDirectoryClient();
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            var parts = directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                dirClient = dirClient.GetSubdirectoryClient(part);
            }
        }

        var results = new List<StorageFileItem>();
        await foreach (ShareFileItem item in dirClient.GetFilesAndDirectoriesAsync(cancellationToken: cancellationToken))
        {
            results.Add(new StorageFileItem
            {
                Name = item.Name,
                Uri = $"{shareClient.Uri}/{directoryPath?.Trim('/')}/{item.Name}".TrimEnd('/'),
                ContentLength = item.FileSize,
                ContentType = item.IsDirectory ? "Directory" : "File"
            });
        }

        return results;
    }

    public async Task<bool> DeleteFileAsync(
        string shareName,
        string directoryPath,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var shareClient = _shareServiceClient.GetShareClient(shareName);
        ShareDirectoryClient dirClient = shareClient.GetRootDirectoryClient();
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            var parts = directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                dirClient = dirClient.GetSubdirectoryClient(part);
            }
        }

        var fileClient = dirClient.GetFileClient(fileName);
        _logger.LogInformation("Deleting file '{FileName}' from share '{ShareName}'", fileName, shareName);
        return await fileClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
