using AzureServices.Core.Models;

namespace AzureServices.Core.Interfaces;

public interface IServiceBusQueueService
{
    Task SendMessageAsync<T>(string queueName, T payload, string? correlationId = null, TimeSpan? delay = null, IDictionary<string, object>? customProperties = null, CancellationToken cancellationToken = default);
    Task SendBatchMessagesAsync<T>(string queueName, IEnumerable<T> payloads, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceBusOrderMessage>> PeekDeadLetterMessagesAsync(string queueName, int maxMessages = 10, CancellationToken cancellationToken = default);
}
