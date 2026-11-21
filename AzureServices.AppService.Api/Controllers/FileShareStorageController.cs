using System.Text;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using AzureServices.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/fileshares")]
public class FileShareStorageController : ControllerBase
{
    private readonly IFileShareStorageService _fileShareService;
    private readonly string _defaultShareName;

    public FileShareStorageController(
        IFileShareStorageService fileShareService,
        IOptions<AzureSettings> azureSettings)
    {
        _fileShareService = fileShareService;
        _defaultShareName = azureSettings.Value.Storage.FileShareName;
    }

    /// <summary>
    /// Uploads a file or document to Azure Files (SMB / Cloud File Share).
    /// Used for shared application directories, configuration files, and container volume mounts.
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<string>>> UploadFile(
        IFormFile file,
        [FromQuery] string? directoryPath,
        [FromQuery] string? shareName,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<string>.Fail("File cannot be empty."));
        }

        string targetShare = shareName ?? _defaultShareName;
        string targetDir = directoryPath ?? "uploads";

        using var stream = file.OpenReadStream();
        await _fileShareService.UploadFileAsync(targetShare, targetDir, file.FileName, stream, cancellationToken);

        return Ok(ApiResponse<string>.Ok($"{targetShare}/{targetDir}/{file.FileName}", "File uploaded to Azure File Share."));
    }

    /// <summary>
    /// Creates and uploads a sample log or config file directly into Azure Files.
    /// </summary>
    [HttpPost("upload-text")]
    public async Task<ActionResult<ApiResponse<string>>> UploadTextFile(
        [FromBody] UploadTextShareRequest request,
        CancellationToken cancellationToken)
    {
        string targetShare = request.ShareName ?? _defaultShareName;
        string targetDir = request.DirectoryPath ?? "configs";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Content));
        await _fileShareService.UploadFileAsync(targetShare, targetDir, request.FileName, stream, cancellationToken);

        return Ok(ApiResponse<string>.Ok($"{targetShare}/{targetDir}/{request.FileName}", "File written to Azure File Share."));
    }

    /// <summary>
    /// Lists files and subdirectories inside an Azure File Share directory.
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StorageFileItem>>>> ListFiles(
        [FromQuery] string? directoryPath,
        [FromQuery] string? shareName,
        CancellationToken cancellationToken)
    {
        string targetShare = shareName ?? _defaultShareName;
        var items = await _fileShareService.ListFilesAndDirectoriesAsync(targetShare, directoryPath, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StorageFileItem>>.Ok(items, $"Found {items.Count} items in Azure File Share '{targetShare}'."));
    }

    /// <summary>
    /// Downloads a file from Azure File Share.
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> DownloadFile(
        [FromQuery] string fileName,
        [FromQuery] string? directoryPath,
        [FromQuery] string? shareName,
        CancellationToken cancellationToken)
    {
        string targetShare = shareName ?? _defaultShareName;
        string targetDir = directoryPath ?? "uploads";

        var stream = await _fileShareService.DownloadFileAsync(targetShare, targetDir, fileName, cancellationToken);
        return File(stream, "application/octet-stream", fileName);
    }

    /// <summary>
    /// Deletes a file from Azure File Share.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteFile(
        [FromQuery] string fileName,
        [FromQuery] string? directoryPath,
        [FromQuery] string? shareName,
        CancellationToken cancellationToken)
    {
        string targetShare = shareName ?? _defaultShareName;
        string targetDir = directoryPath ?? "uploads";

        bool deleted = await _fileShareService.DeleteFileAsync(targetShare, targetDir, fileName, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(deleted, deleted ? "File deleted." : "File not found."));
    }
}

public class UploadTextShareRequest
{
    public string FileName { get; set; } = "appsettings-shared.json";
    public string DirectoryPath { get; set; } = "configs";
    public string Content { get; set; } = "{\n  \"SharedFeatureFlag\": true\n}";
    public string? ShareName { get; set; }
}
