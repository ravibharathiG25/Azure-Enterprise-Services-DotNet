using System.Diagnostics;
using System.Text;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using AzureServices.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IBlobStorageService _blobService;
    private readonly ITableStorageService _tableService;
    private readonly IQueueStorageService _queueService;
    private readonly IServiceBusQueueService _serviceBusQueueService;
    private readonly IServiceBusTopicService _serviceBusTopicService;
    private readonly IAppInsightsTelemetryService _telemetryService;
    private readonly AzureSettings _azureSettings;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IBlobStorageService blobService,
        ITableStorageService tableService,
        IQueueStorageService queueService,
        IServiceBusQueueService serviceBusQueueService,
        IServiceBusTopicService serviceBusTopicService,
        IAppInsightsTelemetryService telemetryService,
        IOptions<AzureSettings> azureSettings,
        ILogger<OrdersController> logger)
    {
        _blobService = blobService;
        _tableService = tableService;
        _queueService = queueService;
        _serviceBusQueueService = serviceBusQueueService;
        _serviceBusTopicService = serviceBusTopicService;
        _telemetryService = telemetryService;
        _azureSettings = azureSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes an end-to-end order integrating:
    /// 1. Azure Blob Storage: Stores generated invoice receipt
    /// 2. Azure Table Storage: Logs audit record
    /// 3. Azure Service Bus Queue: Point-to-point decoupling for fulfillment
    /// 4. Azure Service Bus Topic: Event broadcast for subscribers
    /// 5. Azure Queue Storage: Simple notification queue
    /// 6. Azure Application Insights: Custom metrics & distributed tracing
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Order>>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        string orderId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string correlationId = HttpContext.TraceIdentifier;

        _logger.LogInformation("Creating order '{OrderId}' for customer '{CustomerName}' (Amount: {Amount})",
            orderId, request.CustomerName, request.Amount);

        var order = new Order
        {
            Id = orderId,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Amount = request.Amount,
            Currency = request.Currency ?? "USD",
            Priority = request.Priority ?? (request.Amount > 1000 ? "High" : "Normal"),
            Status = "Processing",
            CreatedAtUtc = DateTime.UtcNow
        };

        // 1. Azure Blob Storage: Generate invoice and upload
        string invoiceContent = $"INVOICE #{order.Id}\nCustomer: {order.CustomerName} ({order.CustomerEmail})\nAmount: {order.Amount} {order.Currency}\nDate: {order.CreatedAtUtc:u}\nStatus: {order.Status}";
        string blobFileName = $"invoices/invoice_{order.Id}.txt";

        using (var invoiceStream = new MemoryStream(Encoding.UTF8.GetBytes(invoiceContent)))
        {
            order.ReceiptBlobUrl = await _blobService.UploadBlobAsync(
                _azureSettings.Storage.BlobContainerName,
                blobFileName,
                invoiceStream,
                "text/plain; charset=utf-8",
                new Dictionary<string, string>
                {
                    { "OrderId", order.Id },
                    { "CustomerId", order.CustomerId }
                },
                cancellationToken);
        }

        // 2. Azure Table Storage: Write audit entry
        var auditEntity = new OrderAuditTableEntity
        {
            PartitionKey = order.CustomerId,
            RowKey = order.Id,
            OrderId = order.Id,
            CustomerName = order.CustomerName,
            Amount = order.Amount,
            Status = order.Status,
            Action = "OrderCreated",
            ExecutedBy = User.Identity?.Name ?? "API-Client",
            Details = $"Receipt stored at {order.ReceiptBlobUrl}"
        };
        await _tableService.UpsertEntityAsync(_azureSettings.Storage.TableName, auditEntity, cancellationToken);

        // 3. Azure Service Bus Queue: Dispatch for fulfillment
        var sbMessage = new ServiceBusOrderMessage
        {
            CorrelationId = correlationId,
            EventType = "OrderCreated",
            Priority = order.Priority,
            Order = order
        };
        await _serviceBusQueueService.SendMessageAsync(
            _azureSettings.ServiceBus.QueueName,
            sbMessage,
            correlationId,
            null,
            new Dictionary<string, object>
            {
                { "Priority", order.Priority },
                { "Amount", (double)order.Amount }
            },
            cancellationToken);

        // 4. Azure Service Bus Topic: Publish event for reporting / analytics subscribers
        await _serviceBusTopicService.PublishAsync(
            _azureSettings.ServiceBus.TopicName,
            sbMessage,
            "OrderCreated",
            order.Priority,
            correlationId,
            null,
            cancellationToken);

        // 5. Azure Queue Storage: Quick notification task
        await _queueService.SendMessageAsync(
            _azureSettings.Storage.QueueName,
            $"Send confirmation email to {order.CustomerEmail} for Order #{order.Id}",
            cancellationToken: cancellationToken);

        stopwatch.Stop();

        // 6. Azure Application Insights: Telemetry & Metrics
        _telemetryService.TrackEvent("OrderPlaced", new Dictionary<string, string>
        {
            { "OrderId", order.Id },
            { "CustomerId", order.CustomerId },
            { "Priority", order.Priority }
        }, new Dictionary<string, double>
        {
            { "Amount", (double)order.Amount },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        });

        _telemetryService.TrackMetric("OrderAmountUSD", (double)order.Amount);
        _telemetryService.TrackMetric("OrderCreationLatencyMs", stopwatch.ElapsedMilliseconds);

        return CreatedAtAction(nameof(GetOrderAudit), new { customerId = order.CustomerId, orderId = order.Id },
            ApiResponse<Order>.Ok(order, "Order created and processed across all integrated Azure services.", correlationId));
    }

    /// <summary>
    /// Retrieves order audit details directly from Azure Table Storage.
    /// </summary>
    [HttpGet("{customerId}/{orderId}")]
    public async Task<ActionResult<ApiResponse<OrderAuditTableEntity>>> GetOrderAudit(
        string customerId,
        string orderId,
        CancellationToken cancellationToken)
    {
        var entity = await _tableService.GetEntityAsync<OrderAuditTableEntity>(
            _azureSettings.Storage.TableName,
            customerId,
            orderId,
            cancellationToken);

        if (entity == null)
        {
            return NotFound(ApiResponse<OrderAuditTableEntity>.Fail($"Order audit for '{orderId}' not found."));
        }

        return Ok(ApiResponse<OrderAuditTableEntity>.Ok(entity, "Order audit loaded from Table Storage."));
    }
}

public class CreateOrderRequest
{
    public string CustomerId { get; set; } = "CUST-1001";
    public string CustomerName { get; set; } = "Jane Doe";
    public string CustomerEmail { get; set; } = "jane.doe@example.com";
    public decimal Amount { get; set; } = 1250.00m;
    public string? Currency { get; set; } = "USD";
    public string? Priority { get; set; } // Normal, High, Critical
}
