using AzureServices.Core.Models;

namespace AzureServices.Core.Interfaces;

public interface IQueueStorageService
{
    Task SendMessageAsync(string queueName, string messageText, TimeSpan? timeToLive = null, TimeSpan? visibilityTimeout = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueueMessageItem>> ReceiveMessagesAsync(string queueName, int maxMessages = 10, TimeSpan? visibilityTimeout = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueueMessageItem>> PeekMessagesAsync(string queueName, int maxMessages = 10, CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(string queueName, string messageId, string popReceipt, CancellationToken cancellationToken = default);
    Task<int> GetApproximateMessageCountAsync(string queueName, CancellationToken cancellationToken = default);
    Task ClearMessagesAsync(string queueName, CancellationToken cancellationToken = default);
}
