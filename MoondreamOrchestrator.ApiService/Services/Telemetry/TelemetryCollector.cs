namespace MoondreamOrchestrator.ApiService.Services.Telemetry;

/// <summary>
/// Telemetry event representing a processing stage
/// </summary>
public record TelemetryEvent
{
    public string EventName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? JobId { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public Dictionary<string, double> Metrics { get; init; } = new();
}

/// <summary>
/// Interface for collecting telemetry events
/// </summary>
public interface ITelemetryCollector
{
    void TrackEvent(TelemetryEvent telemetryEvent);
    void TrackMetric(string name, double value, Dictionary<string, string>? dimensions = null);
    void Flush();
}

/// <summary>
/// File-based telemetry collector that writes JSON lines to a log file
/// </summary>
public class FileTelemetryCollector : ITelemetryCollector, IDisposable
{
    private readonly string _filePath;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private readonly ILogger<FileTelemetryCollector> _logger;

    public FileTelemetryCollector(string filePath, ILogger<FileTelemetryCollector> logger)
    {
        _filePath = filePath;
        _logger = logger;
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(_filePath, append: true) { AutoFlush = true };
    }

    public void TrackEvent(TelemetryEvent telemetryEvent)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(telemetryEvent);
            
            lock (_lock)
            {
                _writer.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing telemetry event to file");
        }
    }

    public void TrackMetric(string name, double value, Dictionary<string, string>? dimensions = null)
    {
        var evt = new TelemetryEvent
        {
            EventName = "Metric",
            Properties = new Dictionary<string, object> { ["MetricName"] = name },
            Metrics = new Dictionary<string, double> { [name] = value }
        };

        if (dimensions != null)
        {
            foreach (var kvp in dimensions)
            {
                evt.Properties[kvp.Key] = kvp.Value;
            }
        }

        TrackEvent(evt);
    }

    public void Flush()
    {
        lock (_lock)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        Flush();
        _writer.Dispose();
    }
}

/// <summary>
/// No-op telemetry collector for when telemetry is disabled
/// </summary>
public class NullTelemetryCollector : ITelemetryCollector
{
    public void TrackEvent(TelemetryEvent telemetryEvent) { }
    public void TrackMetric(string name, double value, Dictionary<string, string>? dimensions = null) { }
    public void Flush() { }
}
