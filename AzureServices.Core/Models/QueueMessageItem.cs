namespace AzureServices.Core.Models;

public class QueueMessageItem
{
    public string MessageId { get; set; } = string.Empty;
    public string PopReceipt { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public long DequeueCount { get; set; }
    public DateTimeOffset? InsertedOn { get; set; }
    public DateTimeOffset? ExpiresOn { get; set; }
    public DateTimeOffset? NextVisibleOn { get; set; }
}
