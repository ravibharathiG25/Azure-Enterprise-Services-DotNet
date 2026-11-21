using Azure.Data.Tables;

namespace AzureServices.Core.Interfaces;

public interface ITableStorageService
{
    Task UpsertEntityAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default) where T : class, ITableEntity;
    Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default) where T : class, ITableEntity;
    Task<IReadOnlyList<T>> QueryEntitiesAsync<T>(string tableName, string? filter = null, int? maxPerPage = null, CancellationToken cancellationToken = default) where T : class, ITableEntity;
    Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default);
}
