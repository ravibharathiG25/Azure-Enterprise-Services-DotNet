using System.Text;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using AzureServices.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/blobs")]
public class BlobStorageController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly string _defaultContainer;

    public BlobStorageController(
        IBlobStorageService blobStorageService,
        IOptions<AzureSettings> azureSettings)
    {
        _blobStorageService = blobStorageService;
        _defaultContainer = azureSettings.Value.Storage.BlobContainerName;
    }

    /// <summary>
    /// Uploads an arbitrary file or document to Azure Blob Storage with metadata.
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<string>>> UploadFile(
        IFormFile file,
        [FromQuery] string? containerName,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<string>.Fail("File is required and cannot be empty."));
        }

        string targetContainer = containerName ?? _defaultContainer;
        string blobName = $"{Guid.NewGuid():N}_{file.FileName}";

        var metadata = new Dictionary<string, string>
        {
            { "OriginalFileName", file.FileName },
            { "UploadedAtUtc", DateTime.UtcNow.ToString("O") },
            { "UploadedBy", User.Identity?.Name ?? "Anonymous" }
        };

        using var stream = file.OpenReadStream();
        string blobUri = await _blobStorageService.UploadBlobAsync(
            targetContainer,
            blobName,
            stream,
            file.ContentType,
            metadata,
            cancellationToken);

        return Ok(ApiResponse<string>.Ok(blobUri, $"File uploaded successfully to Azure Blob Storage as '{blobName}'."));
    }

    /// <summary>
    /// Creates and uploads a sample text document directly to Azure Blob Storage.
    /// </summary>
    [HttpPost("upload-text")]
    public async Task<ActionResult<ApiResponse<string>>> UploadText(
        [FromBody] UploadTextRequest request,
        CancellationToken cancellationToken)
    {
        string targetContainer = request.ContainerName ?? _defaultContainer;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Content));

        var metadata = new Dictionary<string, string>
        {
            { "Author", User.Identity?.Name ?? "Anonymous" },
            { "CreatedUtc", DateTime.UtcNow.ToString("O") }
        };

        string blobUri = await _blobStorageService.UploadBlobAsync(
            targetContainer,
            request.BlobName,
            stream,
            "text/plain; charset=utf-8",
            metadata,
            cancellationToken);

        return Ok(ApiResponse<string>.Ok(blobUri, $"Blob '{request.BlobName}' written successfully."));
    }

    /// <summary>
    /// Downloads a blob from Azure Blob Storage.
    /// </summary>
    [HttpGet("download/{blobName}")]
    public async Task<IActionResult> DownloadBlob(
        string blobName,
        [FromQuery] string? containerName,
        CancellationToken cancellationToken)
    {
        string targetContainer = containerName ?? _defaultContainer;
        var (stream, contentType, _) = await _blobStorageService.DownloadBlobAsync(targetContainer, blobName, cancellationToken);
        return File(stream, contentType, blobName);
    }

    /// <summary>
    /// Lists all blobs and metadata in the Azure Blob Storage container.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StorageFileItem>>>> ListBlobs(
        [FromQuery] string? containerName,
        [FromQuery] string? prefix,
        CancellationToken cancellationToken)
    {
        string targetContainer = containerName ?? _defaultContainer;
        var blobs = await _blobStorageService.ListBlobsAsync(targetContainer, prefix, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StorageFileItem>>.Ok(blobs, $"Found {blobs.Count} blob(s) in container '{targetContainer}'."));
    }

    /// <summary>
    /// Generates a time-limited Shared Access Signature (SAS) token for delegated secure access without exposing keys.
    /// </summary>
    [HttpGet("sas/{blobName}")]
    public ActionResult<ApiResponse<string>> GenerateSasUrl(
        string blobName,
        [FromQuery] string? containerName,
        [FromQuery] int validityMinutes = 30)
    {
        string targetContainer = containerName ?? _defaultContainer;
        string sasUri = _blobStorageService.GenerateSasUri(targetContainer, blobName, TimeSpan.FromMinutes(validityMinutes));
        return Ok(ApiResponse<string>.Ok(sasUri, $"SAS URI generated. Valid for {validityMinutes} minutes."));
    }

    /// <summary>
    /// Changes the Azure Blob Storage Access Tier (Hot, Cool, Cold, Archive) for cost optimization.
    /// </summary>
    [HttpPut("tier/{blobName}")]
    public async Task<ActionResult<ApiResponse<string>>> ChangeTier(
        string blobName,
        [FromQuery] string tier,
        [FromQuery] string? containerName,
        CancellationToken cancellationToken)
    {
        string targetContainer = containerName ?? _defaultContainer;
        await _blobStorageService.SetAccessTierAsync(targetContainer, blobName, tier, cancellationToken);
        return Ok(ApiResponse<string>.Ok(tier, $"Blob '{blobName}' access tier modified to '{tier}'."));
    }

    /// <summary>
    /// Deletes a blob from Azure Blob Storage.
    /// </summary>
    [HttpDelete("{blobName}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteBlob(
        string blobName,
        [FromQuery] string? containerName,
        CancellationToken cancellationToken)
    {
        string targetContainer = containerName ?? _defaultContainer;
        bool deleted = await _blobStorageService.DeleteBlobAsync(targetContainer, blobName, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(deleted, deleted ? $"Blob '{blobName}' deleted." : "Blob did not exist."));
    }
}

public class UploadTextRequest
{
    public string BlobName { get; set; } = "sample-document.txt";
    public string Content { get; set; } = "Azure Blob Storage sample content demonstrating unstructured object storage.";
    public string? ContainerName { get; set; }
}
