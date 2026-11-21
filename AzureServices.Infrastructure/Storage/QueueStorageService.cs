using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.Storage;

public class QueueStorageService : IQueueStorageService
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly ILogger<QueueStorageService> _logger;

    public QueueStorageService(QueueServiceClient queueServiceClient, ILogger<QueueStorageService> logger)
    {
        _queueServiceClient = queueServiceClient;
        _logger = logger;
    }

    public async Task SendMessageAsync(
        string queueName,
        string messageText,
        TimeSpan? timeToLive = null,
        TimeSpan? visibilityTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("Enqueuing message to Azure Storage Queue '{QueueName}'", queueName);
        await queueClient.SendMessageAsync(
            messageText,
            visibilityTimeout: visibilityTimeout ?? TimeSpan.Zero,
            timeToLive: timeToLive ?? TimeSpan.FromDays(7),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<QueueMessageItem>> ReceiveMessagesAsync(
        string queueName,
        int maxMessages = 10,
        TimeSpan? visibilityTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        if (!await queueClient.ExistsAsync(cancellationToken))
        {
            return Array.Empty<QueueMessageItem>();
        }

        QueueMessage[] messages = await queueClient.ReceiveMessagesAsync(
            maxMessages: maxMessages,
            visibilityTimeout: visibilityTimeout ?? TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        return messages.Select(m => new QueueMessageItem
        {
            MessageId = m.MessageId,
            PopReceipt = m.PopReceipt,
            Body = m.Body.ToString(),
            DequeueCount = m.DequeueCount,
            InsertedOn = m.InsertedOn,
            ExpiresOn = m.ExpiresOn,
            NextVisibleOn = m.NextVisibleOn
        }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessageItem>> PeekMessagesAsync(
        string queueName,
        int maxMessages = 10,
        CancellationToken cancellationToken = default)
    {
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        if (!await queueClient.ExistsAsync(cancellationToken))
        {
            return Array.Empty<QueueMessageItem>();
        }

        PeekedMessage[] messages = await queueClient.PeekMessagesAsync(maxMessages, cancellationToken);

        return messages.Select(m => new QueueMessageItem
        {
            MessageId = m.MessageId,
            Body = m.Body.ToString(),
            DequeueCount = m.DequeueCount,
            InsertedOn = m.InsertedOn,
            ExpiresOn = m.ExpiresOn
        }).ToList();
    }

    public async Task DeleteMessageAsync(
        string queueName,
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default)
    {
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        _logger.LogInformation("Deleting message '{MessageId}' from queue '{QueueName}'", messageId, queueName);
        await queueClient.DeleteMessageAsync(messageId, popReceipt, cancellationToken);
    }

    public async Task<int> GetApproximateMessageCountAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        if (!await queueClient.ExistsAsync(cancellationToken))
        {
            return 0;
        }

        QueueProperties properties = await queueClient.GetPropertiesAsync(cancellationToken);
        return properties.ApproximateMessagesCount;
    }

    public async Task ClearMessagesAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        if (await queueClient.ExistsAsync(cancellationToken))
        {
            _logger.LogInformation("Clearing all messages from queue '{QueueName}'", queueName);
            await queueClient.ClearMessagesAsync(cancellationToken);
        }
    }
}
