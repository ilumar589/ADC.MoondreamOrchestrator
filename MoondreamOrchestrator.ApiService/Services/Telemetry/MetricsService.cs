using System.Collections.Concurrent;
using System.Text;

namespace MoondreamOrchestrator.ApiService.Services.Telemetry;

/// <summary>
/// Simple metrics service with Prometheus-style metrics
/// </summary>
public class MetricsService
{
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> _histograms = new();
    private readonly ConcurrentDictionary<string, double> _gauges = new();
    private readonly object _lock = new();

    public void IncrementCounter(string name, long value = 1, Dictionary<string, string>? labels = null)
    {
        var key = BuildKey(name, labels);
        _counters.AddOrUpdate(key, value, (_, current) => current + value);
    }

    public void RecordHistogram(string name, double value, Dictionary<string, string>? labels = null)
    {
        var key = BuildKey(name, labels);
        var bag = _histograms.GetOrAdd(key, _ => new ConcurrentBag<double>());
        bag.Add(value);
    }

    public void SetGauge(string name, double value, Dictionary<string, string>? labels = null)
    {
        var key = BuildKey(name, labels);
        _gauges[key] = value;
    }

    public string GetPrometheusMetrics()
    {
        var sb = new StringBuilder();

        // Counters
        foreach (var kvp in _counters)
        {
            sb.AppendLine($"{kvp.Key} {kvp.Value}");
        }

        // Gauges
        foreach (var kvp in _gauges)
        {
            sb.AppendLine($"{kvp.Key} {kvp.Value}");
        }

        // Histograms (simplified - just count, sum, and some percentiles)
        foreach (var kvp in _histograms)
        {
            var values = kvp.Value.OrderBy(v => v).ToArray();
            if (values.Length == 0) continue;

            var count = values.Length;
            var sum = values.Sum();
            
            sb.AppendLine($"{kvp.Key}_count {count}");
            sb.AppendLine($"{kvp.Key}_sum {sum}");
            
            if (count > 0)
            {
                var p50Index = (int)(count * 0.5);
                var p95Index = (int)(count * 0.95);
                var p99Index = (int)(count * 0.99);
                
                sb.AppendLine($"{kvp.Key}{{quantile=\"0.5\"}} {values[Math.Min(p50Index, count - 1)]}");
                sb.AppendLine($"{kvp.Key}{{quantile=\"0.95\"}} {values[Math.Min(p95Index, count - 1)]}");
                sb.AppendLine($"{kvp.Key}{{quantile=\"0.99\"}} {values[Math.Min(p99Index, count - 1)]}");
            }
        }

        return sb.ToString();
    }

    private static string BuildKey(string name, Dictionary<string, string>? labels)
    {
        if (labels == null || labels.Count == 0)
        {
            return name;
        }

        var labelStr = string.Join(",", labels.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\""));
        return $"{name}{{{labelStr}}}";
    }
}
