namespace AzureServices.Core.Interfaces;

public interface IServiceBusTopicService
{
    Task PublishAsync<T>(string topicName, T message, string eventType, string priority, string? correlationId = null, IDictionary<string, object>? additionalProperties = null, CancellationToken cancellationToken = default);
}
