using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using AzureServices.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/servicebus")]
public class ServiceBusController : ControllerBase
{
    private readonly IServiceBusQueueService _queueService;
    private readonly IServiceBusTopicService _topicService;
    private readonly ServiceBusOptions _sbOptions;

    public ServiceBusController(
        IServiceBusQueueService queueService,
        IServiceBusTopicService topicService,
        IOptions<AzureSettings> azureSettings)
    {
        _queueService = queueService;
        _topicService = topicService;
        _sbOptions = azureSettings.Value.ServiceBus;
    }

    /// <summary>
    /// Sends a point-to-point decoupled message to Azure Service Bus Queue with correlation ID and retry policy.
    /// </summary>
    [HttpPost("queue/send")]
    public async Task<ActionResult<ApiResponse<ServiceBusOrderMessage>>> SendToQueue(
        [FromBody] Order order,
        [FromQuery] string? queueName,
        [FromQuery] int? delaySeconds,
        CancellationToken cancellationToken)
    {
        string targetQueue = queueName ?? _sbOptions.QueueName;
        var message = new ServiceBusOrderMessage
        {
            EventType = "OrderQueued",
            Priority = order.Priority,
            Order = order
        };

        TimeSpan? delay = delaySeconds.HasValue ? TimeSpan.FromSeconds(delaySeconds.Value) : null;

        await _queueService.SendMessageAsync(
            targetQueue,
            message,
            message.CorrelationId,
            delay,
            new Dictionary<string, object>
            {
                { "Priority", order.Priority },
                { "CustomerId", order.CustomerId }
            },
            cancellationToken);

        return Ok(ApiResponse<ServiceBusOrderMessage>.Ok(message, $"Order sent to Azure Service Bus Queue '{targetQueue}'."));
    }

    /// <summary>
    /// Sends a batch of messages to Azure Service Bus Queue efficiently in a single round-trip.
    /// </summary>
    [HttpPost("queue/send-batch")]
    public async Task<ActionResult<ApiResponse<int>>> SendBatchToQueue(
        [FromBody] List<Order> orders,
        [FromQuery] string? queueName,
        CancellationToken cancellationToken)
    {
        string targetQueue = queueName ?? _sbOptions.QueueName;
        var messages = orders.Select(o => new ServiceBusOrderMessage
        {
            EventType = "OrderBatchQueued",
            Priority = o.Priority,
            Order = o
        }).ToList();

        await _queueService.SendBatchMessagesAsync(targetQueue, messages, cancellationToken);
        return Ok(ApiResponse<int>.Ok(messages.Count, $"Batch of {messages.Count} messages sent to '{targetQueue}'."));
    }

    /// <summary>
    /// Inspects the Dead-Letter Queue (DLQ) for failed, expired, or poison messages.
    /// </summary>
    [HttpGet("queue/deadletter")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServiceBusOrderMessage>>>> PeekDeadLetter(
        [FromQuery] string? queueName,
        [FromQuery] int maxMessages = 10,
        CancellationToken cancellationToken = default)
    {
        string targetQueue = queueName ?? _sbOptions.QueueName;
        var dlqMessages = await _queueService.PeekDeadLetterMessagesAsync(targetQueue, maxMessages, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ServiceBusOrderMessage>>.Ok(dlqMessages, $"Peeked {dlqMessages.Count} message(s) from Dead-Letter Queue of '{targetQueue}'."));
    }

    /// <summary>
    /// Publishes an event to Azure Service Bus Topic for 1-to-many publish-subscribe.
    /// Subscribers use SQL Filters (e.g. Priority = 'High') to filter relevant messages.
    /// </summary>
    [HttpPost("topic/publish")]
    public async Task<ActionResult<ApiResponse<ServiceBusOrderMessage>>> PublishToTopic(
        [FromBody] Order order,
        [FromQuery] string? topicName,
        CancellationToken cancellationToken)
    {
        string targetTopic = topicName ?? _sbOptions.TopicName;
        var message = new ServiceBusOrderMessage
        {
            EventType = "OrderSubmittedEvent",
            Priority = order.Priority,
            Order = order
        };

        await _topicService.PublishAsync(
            targetTopic,
            message,
            message.EventType,
            message.Priority,
            message.CorrelationId,
            new Dictionary<string, object>
            {
                { "Region", "eastus" },
                { "Amount", (double)order.Amount }
            },
            cancellationToken);

        return Ok(ApiResponse<ServiceBusOrderMessage>.Ok(message, $"Event published to Azure Service Bus Topic '{targetTopic}' (Priority: {order.Priority})."));
    }
}
