using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using AzureServices.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/tables")]
public class TableStorageController : ControllerBase
{
    private readonly ITableStorageService _tableStorageService;
    private readonly string _defaultTableName;

    public TableStorageController(
        ITableStorageService tableStorageService,
        IOptions<AzureSettings> azureSettings)
    {
        _tableStorageService = tableStorageService;
        _defaultTableName = azureSettings.Value.Storage.TableName;
    }

    /// <summary>
    /// Upserts an order audit entry into Azure Table Storage.
    /// Demonstrates high-throughput NoSQL key-value storage with PartitionKey and RowKey.
    /// </summary>
    [HttpPost("entities")]
    public async Task<ActionResult<ApiResponse<OrderAuditTableEntity>>> UpsertEntity(
        [FromBody] OrderAuditTableEntity entity,
        [FromQuery] string? tableName,
        CancellationToken cancellationToken)
    {
        string targetTable = tableName ?? _defaultTableName;

        if (string.IsNullOrWhiteSpace(entity.PartitionKey))
        {
            entity.PartitionKey = "Customer-Global";
        }
        if (string.IsNullOrWhiteSpace(entity.RowKey))
        {
            entity.RowKey = Guid.NewGuid().ToString("N");
        }

        await _tableStorageService.UpsertEntityAsync(targetTable, entity, cancellationToken);
        return Ok(ApiResponse<OrderAuditTableEntity>.Ok(entity, $"Entity upserted into Azure Table '{targetTable}'."));
    }

    /// <summary>
    /// Retrieves a single entity by its exact PartitionKey and RowKey (O(1) point-read).
    /// </summary>
    [HttpGet("entities/{partitionKey}/{rowKey}")]
    public async Task<ActionResult<ApiResponse<OrderAuditTableEntity>>> GetEntity(
        string partitionKey,
        string rowKey,
        [FromQuery] string? tableName,
        CancellationToken cancellationToken)
    {
        string targetTable = tableName ?? _defaultTableName;
        var entity = await _tableStorageService.GetEntityAsync<OrderAuditTableEntity>(targetTable, partitionKey, rowKey, cancellationToken);

        if (entity == null)
        {
            return NotFound(ApiResponse<OrderAuditTableEntity>.Fail($"Entity with PK='{partitionKey}', RK='{rowKey}' not found in '{targetTable}'."));
        }

        return Ok(ApiResponse<OrderAuditTableEntity>.Ok(entity, "Entity retrieved successfully."));
    }

    /// <summary>
    /// Queries entities using OData filters across Azure Table Storage partitions.
    /// </summary>
    [HttpGet("entities")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrderAuditTableEntity>>>> QueryEntities(
        [FromQuery] string? partitionKey,
        [FromQuery] string? tableName,
        [FromQuery] int maxPerPage = 20,
        CancellationToken cancellationToken = default)
    {
        string targetTable = tableName ?? _defaultTableName;
        string? filter = !string.IsNullOrWhiteSpace(partitionKey)
            ? $"PartitionKey eq '{partitionKey}'"
            : null;

        var entities = await _tableStorageService.QueryEntitiesAsync<OrderAuditTableEntity>(
            targetTable,
            filter,
            maxPerPage,
            cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<OrderAuditTableEntity>>.Ok(entities, $"Query returned {entities.Count} entity(ies) from '{targetTable}'."));
    }

    /// <summary>
    /// Deletes an entity from Azure Table Storage.
    /// </summary>
    [HttpDelete("entities/{partitionKey}/{rowKey}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteEntity(
        string partitionKey,
        string rowKey,
        [FromQuery] string? tableName,
        CancellationToken cancellationToken)
    {
        string targetTable = tableName ?? _defaultTableName;
        await _tableStorageService.DeleteEntityAsync(targetTable, partitionKey, rowKey, cancellationToken);
        return Ok(ApiResponse<string>.Ok($"{partitionKey}/{rowKey}", $"Entity deleted from '{targetTable}'."));
    }
}
