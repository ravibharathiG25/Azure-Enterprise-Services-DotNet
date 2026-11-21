using AzureServices.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace AzureServices.Functions.Durable;

public class OrderWorkflowResult
{
    public string InstanceId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string FinalStatus { get; set; } = string.Empty;
    public List<string> ExecutionSteps { get; set; } = new();
    public bool RequiresHumanApproval { get; set; }
    public bool Approved { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}

public static class OrderWorkflowOrchestrator
{
    /// <summary>
    /// Durable Functions Orchestrator:
    /// Orchestrates a multi-step, stateful, deterministic order processing pipeline:
    /// 1. Validate Order
    /// 2. Charge Payment
    /// 3. Upload Receipt to Blob Storage
    /// 4. Save Audit Record to Table Storage
    /// 5. Human-in-the-loop: Wait for external event (Manager Approval if > $1,000)
    /// 6. Publish Completion to Service Bus Topic
    /// </summary>
    [Function(nameof(OrderWorkflowOrchestrator))]
    public static async Task<OrderWorkflowResult> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(OrderWorkflowOrchestrator));
        Order order = context.GetInput<Order>() ?? new Order { Id = "DEFAULT-ORD" };

        var steps = new List<string>();
        logger.LogInformation("Starting Durable Orchestration for Order #{OrderId} (Amount: {Amount})", order.Id, order.Amount);

        // Step 1: Validate Order Activity
        bool isValid = await context.CallActivityAsync<bool>(nameof(ValidateOrderActivity), order);
        steps.Add($"Step 1: Order Validation Result: {isValid}");
        if (!isValid)
        {
            return new OrderWorkflowResult
            {
                InstanceId = context.InstanceId,
                OrderId = order.Id,
                FinalStatus = "ValidationFailed",
                ExecutionSteps = steps,
                CompletedAtUtc = context.CurrentUtcDateTime
            };
        }

        // Step 2: Payment Activity
        string paymentTxId = await context.CallActivityAsync<string>(nameof(ProcessPaymentActivity), order);
        steps.Add($"Step 2: Payment Processed. TxId: {paymentTxId}");

        // Step 3: Receipt Generation in Blob Storage
        string receiptUrl = await context.CallActivityAsync<string>(nameof(GenerateReceiptBlobActivity), order);
        steps.Add($"Step 3: Receipt Uploaded to Azure Blob: {receiptUrl}");

        // Step 4: Audit Record in Table Storage
        await context.CallActivityAsync(nameof(RecordTableAuditActivity), order);
        steps.Add("Step 4: Audit Record saved to Azure Table Storage");

        // Step 5: Human-in-the-loop / External Event wait (if high-value order > $1000)
        bool approved = true;
        bool requiredApproval = order.Amount > 1000;

        if (requiredApproval)
        {
            steps.Add("Step 5: High-value order (> $1,000). Waiting for 'ManagerApproval' external event (timeout: 5 minutes)...");

            using var cts = new CancellationTokenSource();
            var approvalEventTask = context.WaitForExternalEvent<bool>("ManagerApproval", cts.Token);
            var timeoutTask = context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(5), cts.Token);

            var winner = await Task.WhenAny(approvalEventTask, timeoutTask);
            if (winner == approvalEventTask)
            {
                cts.Cancel(); // Cancel timer
                approved = await approvalEventTask;
                steps.Add($"Step 5 Result: Manager Approval received = {approved}");
            }
            else
            {
                approved = false;
                steps.Add("Step 5 Result: Approval timed out after 5 minutes. Auto-rejected.");
            }
        }

        if (approved)
        {
            // Step 6: Publish Completion to Azure Service Bus Topic
            await context.CallActivityAsync(nameof(PublishTopicNotificationActivity), order);
            steps.Add("Step 6: Completion Event published to Azure Service Bus Topic");
        }

        return new OrderWorkflowResult
        {
            InstanceId = context.InstanceId,
            OrderId = order.Id,
            FinalStatus = approved ? "Completed" : "RejectedOrTimedOut",
            ExecutionSteps = steps,
            RequiresHumanApproval = requiredApproval,
            Approved = approved,
            CompletedAtUtc = context.CurrentUtcDateTime
        };
    }

    // --- ACTIVITY FUNCTIONS ---

    [Function(nameof(ValidateOrderActivity))]
    public static bool ValidateOrderActivity(
        [ActivityTrigger] Order order,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(ValidateOrderActivity));
        logger.LogInformation("Activity [ValidateOrder]: Validating Order #{OrderId}", order.Id);
        return order.Amount > 0 && !string.IsNullOrWhiteSpace(order.CustomerName);
    }

    [Function(nameof(ProcessPaymentActivity))]
    public static string ProcessPaymentActivity(
        [ActivityTrigger] Order order,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(ProcessPaymentActivity));
        logger.LogInformation("Activity [ProcessPayment]: Charging {Amount} {Currency} for Order #{OrderId}",
            order.Amount, order.Currency, order.Id);
        return $"TX_{Guid.NewGuid():N}"[..16];
    }

    [Function(nameof(GenerateReceiptBlobActivity))]
    public static string GenerateReceiptBlobActivity(
        [ActivityTrigger] Order order,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(GenerateReceiptBlobActivity));
        logger.LogInformation("Activity [GenerateReceiptBlob]: Simulating receipt blob upload for Order #{OrderId}", order.Id);
        return $"https://stazureservicesdemo.blob.core.windows.net/order-documents/receipts/receipt_{order.Id}.pdf";
    }

    [Function(nameof(RecordTableAuditActivity))]
    public static void RecordTableAuditActivity(
        [ActivityTrigger] Order order,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(RecordTableAuditActivity));
        logger.LogInformation("Activity [RecordTableAudit]: Logging audit entry to Azure Table Storage for PK='{Customer}', RK='{OrderId}'",
            order.CustomerId, order.Id);
    }

    [Function(nameof(PublishTopicNotificationActivity))]
    public static void PublishTopicNotificationActivity(
        [ActivityTrigger] Order order,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(PublishTopicNotificationActivity));
        logger.LogInformation("Activity [PublishTopicNotification]: Published to Topic 'orders-topic' for Order #{OrderId}", order.Id);
    }
}
