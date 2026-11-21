namespace AzureServices.Core.Models;

public class ServiceBusOrderMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string EventType { get; set; } = "OrderCreated"; // OrderCreated, OrderProcessed, OrderCompleted, OrderFailed
    public string Priority { get; set; } = "Normal";       // Normal, High, Critical (used for topic SQL filtering)
    public string Region { get; set; } = "eastus";
    public Order Order { get; set; } = new();
    public DateTime EnqueuedTimeUtc { get; set; } = DateTime.UtcNow;
}
