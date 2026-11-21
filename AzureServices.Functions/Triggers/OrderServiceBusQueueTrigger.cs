using System.Text.Json;
using AzureServices.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureServices.Functions.Triggers;

public class OrderServiceBusQueueTrigger
{
    private readonly ILogger<OrderServiceBusQueueTrigger> _logger;

    public OrderServiceBusQueueTrigger(ILogger<OrderServiceBusQueueTrigger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Event-driven serverless trigger for Azure Service Bus Queue.
    /// Scales automatically with queue depth (0 to multiple worker instances).
    /// </summary>
    [Function("OrderServiceBusQueueTrigger")]
    public async Task Run(
        [ServiceBusTrigger("orders-queue", Connection = "ServiceBusConnectionString")] string messageBody,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Service Bus Queue Trigger received message: {MessageBody}", messageBody);

        try
        {
            var message = JsonSerializer.Deserialize<ServiceBusOrderMessage>(messageBody);
            if (message != null)
            {
                _logger.LogInformation("Processing Service Bus Order #{OrderId} (Customer: '{Customer}', Priority: '{Priority}', Amount: {Amount})",
                    message.Order.Id,
                    message.Order.CustomerName,
                    message.Priority,
                    message.Order.Amount);

                // Simulate processing
                await Task.Delay(100);

                _logger.LogInformation("Order #{OrderId} processed successfully in serverless worker.", message.Order.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message from Service Bus queue. Function will retry or dead-letter based on host.json policy.");
            throw; // Re-throwing ensures Azure Service Bus delivery count increments and triggers DLQ on maxDeliveryCount exceeded
        }
    }
}
