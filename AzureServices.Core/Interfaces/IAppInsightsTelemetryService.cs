namespace AzureServices.Core.Interfaces;

public interface IAppInsightsTelemetryService
{
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null, IDictionary<string, double>? metrics = null);
    void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null);
    void TrackException(Exception exception, IDictionary<string, string>? properties = null, IDictionary<string, double>? metrics = null);
    void TrackDependency(string dependencyTypeName, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success);
}
