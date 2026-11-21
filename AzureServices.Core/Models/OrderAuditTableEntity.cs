using Azure;
using Azure.Data.Tables;

namespace AzureServices.Core.Models;

/// <summary>
/// Table entity for high-speed audit logging and order tracking in Azure Table Storage.
/// PartitionKey: Logical grouping (e.g., CustomerId or YearMonth) for distributed scaling.
/// RowKey: Unique identifier within the partition (OrderId).
/// </summary>
public class OrderAuditTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // e.g. CustomerId
    public string RowKey { get; set; } = string.Empty;       // e.g. OrderId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ExecutedBy { get; set; } = string.Empty;
    public string? Details { get; set; }
}
