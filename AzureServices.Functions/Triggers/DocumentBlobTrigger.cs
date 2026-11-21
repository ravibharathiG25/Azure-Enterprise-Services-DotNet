using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureServices.Functions.Triggers;

public class DocumentBlobTrigger
{
    private readonly ILogger<DocumentBlobTrigger> _logger;

    public DocumentBlobTrigger(ILogger<DocumentBlobTrigger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Serverless Blob trigger that activates when a new file or invoice is committed to Azure Blob Storage.
    /// </summary>
    [Function("DocumentBlobTrigger")]
    public async Task Run(
        [BlobTrigger("order-documents/{name}", Connection = "AzureWebJobsStorage")] Stream stream,
        string name,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Azure Blob Trigger fired for blob: '{BlobName}', Size: {Length} bytes", name, stream.Length);

        // Process or analyze file stream (e.g., OCR, virus scan, archival)
        await Task.CompletedTask;

        _logger.LogInformation("Blob '{BlobName}' processing finished.", name);
    }
}
