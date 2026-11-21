using System.Diagnostics;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/monitoring")]
public class MonitoringController : ControllerBase
{
    private readonly IAppInsightsTelemetryService _telemetryService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IAppInsightsTelemetryService telemetryService,
        ILogger<MonitoringController> logger)
    {
        _telemetryService = telemetryService;
        _logger = logger;
    }

    /// <summary>
    /// Tracks a custom business event in Azure Application Insights.
    /// </summary>
    [HttpPost("track-event")]
    public ActionResult<ApiResponse<string>> TrackEvent([FromBody] CustomEventRequest request)
    {
        _logger.LogInformation("Tracking custom event: {EventName}", request.EventName);

        _telemetryService.TrackEvent(
            request.EventName,
            request.Properties ?? new Dictionary<string, string> { { "Source", "MonitoringController" } },
            request.Metrics);

        return Ok(ApiResponse<string>.Ok(request.EventName, "Custom event tracked in Application Insights."));
    }

    /// <summary>
    /// Emits a custom metric to Azure Application Insights.
    /// Useful for monitoring custom SLIs, transaction rates, queue depths, or order values.
    /// </summary>
    [HttpPost("track-metric")]
    public ActionResult<ApiResponse<double>> TrackMetric([FromBody] CustomMetricRequest request)
    {
        _logger.LogInformation("Tracking custom metric: {MetricName} = {Value}", request.MetricName, request.Value);

        _telemetryService.TrackMetric(request.MetricName, request.Value, request.Properties);

        return Ok(ApiResponse<double>.Ok(request.Value, $"Metric '{request.MetricName}' recorded."));
    }

    /// <summary>
    /// Intentionally throws an exception to demonstrate Application Insights Exception Analysis, stack trace capture, and error rate telemetry.
    /// </summary>
    [HttpGet("simulate-exception")]
    public ActionResult<ApiResponse<string>> SimulateException([FromQuery] string? message)
    {
        _logger.LogWarning("Simulating exception for Application Insights demonstration.");
        throw new InvalidOperationException(message ?? "Simulated failure for Application Insights Exception Telemetry analysis.");
    }

    /// <summary>
    /// Simulates an outbound external service or database dependency call and records its timing and status in Application Insights.
    /// </summary>
    [HttpGet("simulate-dependency")]
    public async Task<ActionResult<ApiResponse<object>>> SimulateDependency([FromQuery] int latencyMs = 250, [FromQuery] bool succeed = true)
    {
        var sw = Stopwatch.StartNew();
        DateTimeOffset startTime = DateTimeOffset.UtcNow;

        // Simulate network or database work
        await Task.Delay(Math.Clamp(latencyMs, 10, 5000));
        sw.Stop();

        _telemetryService.TrackDependency(
            dependencyTypeName: "HTTP / PaymentGateway",
            dependencyName: "POST https://api.payments.stripe.com/v1/charges",
            data: "charge_request_token_mock",
            startTime: startTime,
            duration: sw.Elapsed,
            success: succeed);

        var result = new
        {
            Dependency = "External Payment Gateway",
            Target = "api.payments.stripe.com",
            DurationMs = sw.ElapsedMilliseconds,
            Success = succeed,
            Note = "Inspect Application Insights -> Application Map and Performance blade to see dependency dependency durations and failure breakdown."
        };

        return Ok(ApiResponse<object>.Ok(result, "Dependency call recorded in Application Insights."));
    }
}

public class CustomEventRequest
{
    public string EventName { get; set; } = "UserCheckoutCompleted";
    public Dictionary<string, string>? Properties { get; set; }
    public Dictionary<string, double>? Metrics { get; set; }
}

public class CustomMetricRequest
{
    public string MetricName { get; set; } = "OrderProcessingTimeMs";
    public double Value { get; set; } = 142.5;
    public Dictionary<string, string>? Properties { get; set; }
}
