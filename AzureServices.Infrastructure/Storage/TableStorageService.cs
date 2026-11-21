using Azure.Data.Tables;
using AzureServices.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.Storage;

public class TableStorageService : ITableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly ILogger<TableStorageService> _logger;

    public TableStorageService(TableServiceClient tableServiceClient, ILogger<TableStorageService> logger)
    {
        _tableServiceClient = tableServiceClient;
        _logger = logger;
    }

    public async Task UpsertEntityAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default) where T : class, ITableEntity
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);

        _logger.LogInformation("Upserting entity with PartitionKey='{PK}', RowKey='{RK}' into table '{TableName}'",
            entity.PartitionKey, entity.RowKey, tableName);

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default) where T : class, ITableEntity
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        try
        {
            var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<T>> QueryEntitiesAsync<T>(string tableName, string? filter = null, int? maxPerPage = null, CancellationToken cancellationToken = default) where T : class, ITableEntity
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);

        var results = new List<T>();
        var query = tableClient.QueryAsync<T>(filter: filter, maxPerPage: maxPerPage, cancellationToken: cancellationToken);

        await foreach (var entity in query)
        {
            results.Add(entity);
        }

        return results;
    }

    public async Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        _logger.LogInformation("Deleting entity PK='{PK}', RK='{RK}' from table '{TableName}'", partitionKey, rowKey, tableName);
        await tableClient.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: cancellationToken);
    }
}
