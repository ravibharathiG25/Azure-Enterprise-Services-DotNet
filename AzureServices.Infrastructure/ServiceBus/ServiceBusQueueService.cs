using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.ServiceBus;

public class ServiceBusQueueService : IServiceBusQueueService
{
    private readonly ServiceBusClient? _serviceBusClient;
    private readonly ILogger<ServiceBusQueueService> _logger;

    public ServiceBusQueueService(ServiceBusClient? serviceBusClient, ILogger<ServiceBusQueueService> logger)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    public async Task SendMessageAsync<T>(
        string queueName,
        T payload,
        string? correlationId = null,
        TimeSpan? delay = null,
        IDictionary<string, object>? customProperties = null,
        CancellationToken cancellationToken = default)
    {
        if (_serviceBusClient == null)
        {
            _logger.LogWarning("ServiceBusClient not initialized. Simulating send to queue '{QueueName}'", queueName);
            return;
        }

        await using ServiceBusSender sender = _serviceBusClient.CreateSender(queueName);

        string json = JsonSerializer.Serialize(payload);
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
        };

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            message.ScheduledEnqueueTime = DateTimeOffset.UtcNow.Add(delay.Value);
        }

        if (customProperties != null)
        {
            foreach (var kvp in customProperties)
            {
                message.ApplicationProperties[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogInformation("Sending message '{MessageId}' (CorrelationId: '{CorrelationId}') to Service Bus Queue '{QueueName}'",
            message.MessageId, message.CorrelationId, queueName);

        await sender.SendMessageAsync(message, cancellationToken);
    }

    public async Task SendBatchMessagesAsync<T>(
        string queueName,
        IEnumerable<T> payloads,
        CancellationToken cancellationToken = default)
    {
        if (_serviceBusClient == null)
        {
            _logger.LogWarning("ServiceBusClient not initialized. Simulating batch send to queue '{QueueName}'", queueName);
            return;
        }

        await using ServiceBusSender sender = _serviceBusClient.CreateSender(queueName);
        using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync(cancellationToken);

        foreach (var payload in payloads)
        {
            string json = JsonSerializer.Serialize(payload);
            var message = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString("N")
            };

            if (!messageBatch.TryAddMessage(message))
            {
                // Batch is full; send current batch and create new one
                await sender.SendMessagesAsync(messageBatch, cancellationToken);
                using var nextBatch = await sender.CreateMessageBatchAsync(cancellationToken);
                if (!nextBatch.TryAddMessage(message))
                {
                    throw new InvalidOperationException("Single message too large for Azure Service Bus batch limit.");
                }
            }
        }

        if (messageBatch.Count > 0)
        {
            _logger.LogInformation("Sending batch of {Count} messages to Service Bus Queue '{QueueName}'", messageBatch.Count, queueName);
            await sender.SendMessagesAsync(messageBatch, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ServiceBusOrderMessage>> PeekDeadLetterMessagesAsync(
        string queueName,
        int maxMessages = 10,
        CancellationToken cancellationToken = default)
    {
        if (_serviceBusClient == null)
        {
            return Array.Empty<ServiceBusOrderMessage>();
        }

        var options = new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        await using ServiceBusReceiver receiver = _serviceBusClient.CreateReceiver(queueName, options);
        var peeked = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: cancellationToken);

        var results = new List<ServiceBusOrderMessage>();
        foreach (var msg in peeked)
        {
            try
            {
                var orderMsg = JsonSerializer.Deserialize<ServiceBusOrderMessage>(msg.Body.ToString());
                if (orderMsg != null)
                {
                    results.Add(orderMsg);
                }
            }
            catch
            {
                // unparseable DLQ item fallback
            }
        }

        return results;
    }
}
