namespace AzureServices.Core.Models;

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Priority { get; set; } = "Normal"; // Normal, High, Critical
    public string Status { get; set; } = "Pending";   // Pending, Validated, Paid, Completed, Failed
    public string? ReceiptBlobUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
