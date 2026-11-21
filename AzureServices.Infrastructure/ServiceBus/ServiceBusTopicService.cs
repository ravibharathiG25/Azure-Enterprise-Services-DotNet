using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AzureServices.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.ServiceBus;

public class ServiceBusTopicService : IServiceBusTopicService
{
    private readonly ServiceBusClient? _serviceBusClient;
    private readonly ILogger<ServiceBusTopicService> _logger;

    public ServiceBusTopicService(ServiceBusClient? serviceBusClient, ILogger<ServiceBusTopicService> logger)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    public async Task PublishAsync<T>(
        string topicName,
        T message,
        string eventType,
        string priority,
        string? correlationId = null,
        IDictionary<string, object>? additionalProperties = null,
        CancellationToken cancellationToken = default)
    {
        if (_serviceBusClient == null)
        {
            _logger.LogWarning("ServiceBusClient not initialized. Simulating topic publish to '{TopicName}'", topicName);
            return;
        }

        await using ServiceBusSender sender = _serviceBusClient.CreateSender(topicName);

        string json = JsonSerializer.Serialize(message);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            Subject = eventType
        };

        // Application properties used for SQL filter subscription routing
        sbMessage.ApplicationProperties["EventType"] = eventType;
        sbMessage.ApplicationProperties["Priority"] = priority;
        sbMessage.ApplicationProperties["PublishedAtUtc"] = DateTime.UtcNow.ToString("O");

        if (additionalProperties != null)
        {
            foreach (var kvp in additionalProperties)
            {
                sbMessage.ApplicationProperties[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogInformation("Publishing to Topic '{TopicName}' [EventType='{EventType}', Priority='{Priority}']",
            topicName, eventType, priority);

        await sender.SendMessageAsync(sbMessage, cancellationToken);
    }
}
