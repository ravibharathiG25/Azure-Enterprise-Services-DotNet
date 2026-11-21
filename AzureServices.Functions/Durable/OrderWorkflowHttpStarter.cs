using System.Net;
using System.Text.Json;
using AzureServices.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace AzureServices.Functions.Durable;

public class OrderWorkflowHttpStarter
{
    private readonly ILogger<OrderWorkflowHttpStarter> _logger;

    public OrderWorkflowHttpStarter(ILogger<OrderWorkflowHttpStarter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts a new instance of the OrderWorkflowOrchestrator.
    /// Returns 202 Accepted with status query URLs (CheckStatusUri, TerminateUri, PurgeUri, SendEventUri).
    /// </summary>
    [Function("StartOrderWorkflow")]
    public async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/OrderWorkflow")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        _logger.LogInformation("HTTP request received to trigger OrderWorkflowOrchestrator.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        Order order;
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            order = JsonSerializer.Deserialize<Order>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new Order();
        }
        else
        {
            order = new Order
            {
                Id = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                CustomerId = "CUST-DURABLE-100",
                CustomerName = "John Doe",
                Amount = 1500m
            };
        }

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(OrderWorkflowOrchestrator),
            order);

        _logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    /// <summary>
    /// Raises the 'ManagerApproval' external event for a running high-value order orchestration.
    /// Demonstrates human-in-the-loop workflows in Durable Functions.
    /// </summary>
    [Function("ApproveOrderWorkflow")]
    public async Task<HttpResponseData> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/{instanceId}/approve")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        _logger.LogInformation("Raising 'ManagerApproval' event for Instance '{InstanceId}'.", instanceId);

        await client.RaiseEventAsync(instanceId, "ManagerApproval", true);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            InstanceId = instanceId,
            Event = "ManagerApproval",
            Decision = "Approved",
            Timestamp = DateTime.UtcNow
        });
        return response;
    }
}
