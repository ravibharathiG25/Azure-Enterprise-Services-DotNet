using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.Storage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<string> UploadBlobAsync(
        string containerName,
        string blobName,
        Stream content,
        string contentType,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = metadata
        };

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        _logger.LogInformation("Uploading blob '{BlobName}' to container '{ContainerName}' with ContentType '{ContentType}'", blobName, containerName, contentType);
        await blobClient.UploadAsync(content, options, cancellationToken);

        return blobClient.Uri.ToString();
    }

    public async Task<(Stream Content, string ContentType, IDictionary<string, string> Metadata)> DownloadBlobAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        _logger.LogInformation("Downloading blob '{BlobName}' from container '{ContainerName}'", blobName, containerName);
        BlobDownloadResult result = await blobClient.DownloadContentAsync(cancellationToken);

        var metadata = (IDictionary<string, string>)result.Details.Metadata;
        var contentType = result.Details.ContentType;
        return (result.Content.ToStream(), contentType, metadata);
    }

    public async Task<IReadOnlyList<StorageFileItem>> ListBlobsAsync(
        string containerName,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        if (!await containerClient.ExistsAsync(cancellationToken))
        {
            return Array.Empty<StorageFileItem>();
        }

        var results = new List<StorageFileItem>();
        await foreach (BlobItem blob in containerClient.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, cancellationToken))
        {
            results.Add(new StorageFileItem
            {
                Name = blob.Name,
                Uri = containerClient.GetBlobClient(blob.Name).Uri.ToString(),
                ContentLength = blob.Properties.ContentLength,
                ContentType = blob.Properties.ContentType,
                AccessTier = blob.Properties.AccessTier?.ToString(),
                LastModified = blob.Properties.LastModified,
                Metadata = blob.Metadata
            });
        }

        return results;
    }

    public async Task<bool> DeleteBlobAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        _logger.LogInformation("Deleting blob '{BlobName}' from container '{ContainerName}'", blobName, containerName);
        return await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public string GenerateSasUri(string containerName, string blobName, TimeSpan validity, bool canWrite = false)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            _logger.LogWarning("BlobClient cannot generate SAS URI directly without account key or delegation. Returning standard URI.");
            return blobClient.Uri.ToString();
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
        };

        var permissions = BlobSasPermissions.Read;
        if (canWrite)
        {
            permissions |= BlobSasPermissions.Write | BlobSasPermissions.Create;
        }
        sasBuilder.SetPermissions(permissions);

        Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
        return sasUri.ToString();
    }

    public async Task SetAccessTierAsync(
        string containerName,
        string blobName,
        string tier,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        AccessTier accessTier = tier.ToLowerInvariant() switch
        {
            "cool" => AccessTier.Cool,
            "cold" => AccessTier.Cold,
            "archive" => AccessTier.Archive,
            _ => AccessTier.Hot
        };

        _logger.LogInformation("Setting access tier for blob '{BlobName}' to '{Tier}'", blobName, accessTier);
        await blobClient.SetAccessTierAsync(accessTier, cancellationToken: cancellationToken);
    }
}
