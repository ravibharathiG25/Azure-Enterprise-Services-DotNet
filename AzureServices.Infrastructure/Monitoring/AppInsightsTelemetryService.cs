using System.Globalization;
using AzureServices.Core.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.Monitoring;

public class AppInsightsTelemetryService : IAppInsightsTelemetryService
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly ILogger<AppInsightsTelemetryService> _logger;

    public AppInsightsTelemetryService(TelemetryClient? telemetryClient, ILogger<AppInsightsTelemetryService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null, IDictionary<string, double>? metrics = null)
    {
        _logger.LogInformation("Application Insights Event: {EventName}", eventName);
        if (_telemetryClient == null) return;

        var telemetry = new EventTelemetry(eventName);
        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                telemetry.Properties[kvp.Key] = kvp.Value;
            }
        }
        if (metrics != null)
        {
            foreach (var kvp in metrics)
            {
                telemetry.Properties[$"metric_{kvp.Key}"] = kvp.Value.ToString(CultureInfo.InvariantCulture);
                _telemetryClient.TrackMetric(new MetricTelemetry(kvp.Key, kvp.Value));
            }
        }

        _telemetryClient.TrackEvent(telemetry);
    }

    public void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null)
    {
        _logger.LogInformation("Application Insights Metric: {MetricName} = {Value}", metricName, value);
        if (_telemetryClient == null) return;

        var telemetry = new MetricTelemetry(metricName, value);
        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                telemetry.Properties[kvp.Key] = kvp.Value;
            }
        }

        _telemetryClient.TrackMetric(telemetry);
    }

    public void TrackException(Exception exception, IDictionary<string, string>? properties = null, IDictionary<string, double>? metrics = null)
    {
        _logger.LogError(exception, "Application Insights Exception Tracked: {Message}", exception.Message);
        if (_telemetryClient == null) return;

        var telemetry = new ExceptionTelemetry(exception);
        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                telemetry.Properties[kvp.Key] = kvp.Value;
            }
        }
        if (metrics != null)
        {
            foreach (var kvp in metrics)
            {
                telemetry.Properties[$"metric_{kvp.Key}"] = kvp.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        _telemetryClient.TrackException(telemetry);
    }

    public void TrackDependency(string dependencyTypeName, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success)
    {
        _logger.LogInformation("Application Insights Dependency: {TypeName} - {Name} (Success: {Success}, Duration: {Duration}ms)",
            dependencyTypeName, dependencyName, success, duration.TotalMilliseconds);
        if (_telemetryClient == null) return;

        var telemetry = new DependencyTelemetry(
            dependencyTypeName: dependencyTypeName,
            target: null,
            dependencyName: dependencyName,
            data: data,
            startTime: startTime,
            duration: duration,
            resultCode: success ? "200" : "500",
            success: success);

        _telemetryClient.TrackDependency(telemetry);
    }
}
