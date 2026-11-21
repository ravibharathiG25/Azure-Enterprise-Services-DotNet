using System.Net;
using System.Text.Json;
using AzureServices.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AzureServices.Functions.Triggers;

public class OrderHttpTrigger
{
    private readonly ILogger<OrderHttpTrigger> _logger;

    public OrderHttpTrigger(ILogger<OrderHttpTrigger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Serverless HTTP trigger function for instant order status inquiries or submissions.
    /// Demonstrates Azure Functions v4 isolated worker model.
    /// </summary>
    [Function("OrderHttpTrigger")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "orders/status/{orderId?}")] HttpRequestData req,
        string? orderId)
    {
        _logger.LogInformation("C# HTTP trigger function processed an order request for OrderId: {OrderId}", orderId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        var statusData = new
        {
            OrderId = orderId ?? "DEMO-1001",
            Status = "Completed",
            Host = "Azure Functions Isolated Worker (.NET 8)",
            ExecutionTimeUtc = DateTime.UtcNow,
            ServerlessFeatures = new[]
            {
                "Sub-second auto-scaling from 0 to 200+ instances",
                "Consumption billing (pay per execution and memory-seconds)",
                "Built-in bindings for Azure Storage, Service Bus, Cosmos DB, and Event Hubs"
            }
        };

        await response.WriteStringAsync(JsonSerializer.Serialize(statusData));
        return response;
    }
}
