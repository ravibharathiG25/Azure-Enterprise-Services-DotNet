using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using AzureServices.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/queues")]
public class QueueStorageController : ControllerBase
{
    private readonly IQueueStorageService _queueStorageService;
    private readonly string _defaultQueueName;

    public QueueStorageController(
        IQueueStorageService queueStorageService,
        IOptions<AzureSettings> azureSettings)
    {
        _queueStorageService = queueStorageService;
        _defaultQueueName = azureSettings.Value.Storage.QueueName;
    }

    /// <summary>
    /// Enqueues a message into Azure Queue Storage with optional TTL and visibility delay.
    /// </summary>
    [HttpPost("messages")]
    public async Task<ActionResult<ApiResponse<string>>> EnqueueMessage(
        [FromBody] EnqueueMessageRequest request,
        CancellationToken cancellationToken)
    {
        string targetQueue = request.QueueName ?? _defaultQueueName;
        TimeSpan? visibilityTimeout = request.VisibilityDelaySeconds.HasValue
            ? TimeSpan.FromSeconds(request.VisibilityDelaySeconds.Value)
            : null;

        await _queueStorageService.SendMessageAsync(
            targetQueue,
            request.MessageText,
            visibilityTimeout: visibilityTimeout,
            cancellationToken: cancellationToken);

        return Ok(ApiResponse<string>.Ok(request.MessageText, $"Message successfully enqueued into Azure Storage Queue '{targetQueue}'."));
    }

    /// <summary>
    /// Receives and locks messages from Azure Queue Storage (Visibility Timeout pattern).
    /// </summary>
    [HttpGet("messages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QueueMessageItem>>>> ReceiveMessages(
        [FromQuery] string? queueName,
        [FromQuery] int maxMessages = 5,
        [FromQuery] int visibilitySeconds = 30,
        CancellationToken cancellationToken = default)
    {
        string targetQueue = queueName ?? _defaultQueueName;
        var messages = await _queueStorageService.ReceiveMessagesAsync(
            targetQueue,
            maxMessages,
            TimeSpan.FromSeconds(visibilitySeconds),
            cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<QueueMessageItem>>.Ok(messages, $"Received {messages.Count} message(s) from '{targetQueue}'."));
    }

    /// <summary>
    /// Peeks messages without altering their visibility or dequeue count.
    /// </summary>
    [HttpGet("peek")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QueueMessageItem>>>> PeekMessages(
        [FromQuery] string? queueName,
        [FromQuery] int maxMessages = 5,
        CancellationToken cancellationToken = default)
    {
        string targetQueue = queueName ?? _defaultQueueName;
        var messages = await _queueStorageService.PeekMessagesAsync(targetQueue, maxMessages, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<QueueMessageItem>>.Ok(messages, $"Peeked {messages.Count} message(s) from '{targetQueue}'."));
    }

    /// <summary>
    /// Deletes (acknowledges) a processed message using its PopReceipt.
    /// </summary>
    [HttpDelete("messages")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteMessage(
        [FromQuery] string messageId,
        [FromQuery] string popReceipt,
        [FromQuery] string? queueName,
        CancellationToken cancellationToken)
    {
        string targetQueue = queueName ?? _defaultQueueName;
        await _queueStorageService.DeleteMessageAsync(targetQueue, messageId, popReceipt, cancellationToken);
        return Ok(ApiResponse<string>.Ok(messageId, $"Message '{messageId}' acknowledged and deleted from '{targetQueue}'."));
    }

    /// <summary>
    /// Gets the approximate number of messages currently sitting in the queue.
    /// </summary>
    [HttpGet("count")]
    public async Task<ActionResult<ApiResponse<int>>> GetMessageCount(
        [FromQuery] string? queueName,
        CancellationToken cancellationToken)
    {
        string targetQueue = queueName ?? _defaultQueueName;
        int count = await _queueStorageService.GetApproximateMessageCountAsync(targetQueue, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count, $"Approximate messages in '{targetQueue}': {count}"));
    }
}

public class EnqueueMessageRequest
{
    public string MessageText { get; set; } = "Order: #1001 ready for processing.";
    public string? QueueName { get; set; }
    public int? VisibilityDelaySeconds { get; set; }
}
