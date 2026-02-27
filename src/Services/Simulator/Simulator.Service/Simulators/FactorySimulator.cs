using System.Text;
using System.Text.Json;
using Simulator.Service.Configuration;

namespace Simulator.Service.Simulators;

/// <summary>
/// Generates realistic factory metrics data and posts it to the Metrics API.
/// Simulates patterns like: shift changes, gradual degradation, random failures,
/// and maintenance events — creating realistic dashboard data.
/// </summary>
public class FactorySimulator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FactorySimulator> _logger;
    private readonly Random _random = new();
    private readonly Dictionary<string, double> _currentValues = new();
    private int _tickCount;

    public FactorySimulator(HttpClient httpClient, ILogger<FactorySimulator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SimulateTickAsync(CancellationToken cancellationToken)
    {
        _tickCount++;
        var metrics = GenerateMetrics();

        try
        {
            var payload = new { Metrics = metrics };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/Metrics/ingest/batch", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Tick {Tick}: Sent {Count} metrics", _tickCount, metrics.Count);
            }
            else
            {
                _logger.LogWarning("Tick {Tick}: Metrics API returned {StatusCode}", _tickCount, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tick {Tick}: Could not reach Metrics API", _tickCount);
        }
    }

    private List<object> GenerateMetrics()
    {
        var equipmentConfigs = GetDefaultEquipment();
        var metrics = new List<object>();

        foreach (var eq in equipmentConfigs)
        {
            // OEE (Overall Equipment Effectiveness) — 65% to 98%
            metrics.Add(CreateMetric(eq.Id, "OEE", GenerateValue(eq.Id, "OEE", 85, 8, isPercentage: true), "%"));

            // Throughput — varies by equipment
            metrics.Add(CreateMetric(eq.Id, "Throughput", GenerateValue(eq.Id, "Throughput", eq.BaseThroughput, eq.BaseThroughput * 0.15), "units/hr"));

            // Temperature — gradual drift with occasional spikes
            metrics.Add(CreateMetric(eq.Id, "Temperature", GenerateValue(eq.Id, "Temperature", eq.BaseTemp, 20, hasDrift: true), "°C"));

            // Yield Rate — 88% to 99.5%
            metrics.Add(CreateMetric(eq.Id, "YieldRate", GenerateValue(eq.Id, "YieldRate", 95, 4, isPercentage: true), "%"));

            // Power Consumption — correlates with throughput
            var throughputKey = $"{eq.Id}_Throughput";
            var throughputFactor = _currentValues.GetValueOrDefault(throughputKey, eq.BaseThroughput) / eq.BaseThroughput;
            metrics.Add(CreateMetric(eq.Id, "PowerConsumption", GenerateValue(eq.Id, "PowerConsumption", eq.BasePower * throughputFactor, eq.BasePower * 0.1), "kWh"));

            // Vibration — increases with wear
            metrics.Add(CreateMetric(eq.Id, "Vibration", GenerateValue(eq.Id, "Vibration", 2.5, 1.0, hasDrift: true), "mm/s"));
        }

        return metrics;
    }

    private double GenerateValue(string equipmentId, string metricType, double baseValue, double variance, bool isPercentage = false, bool hasDrift = false)
    {
        var key = $"{equipmentId}_{metricType}";

        // Get or initialize current value
        if (!_currentValues.TryGetValue(key, out var currentValue))
        {
            currentValue = baseValue;
            _currentValues[key] = currentValue;
        }

        // Simulate shift change effects (every 480 ticks ≈ 8 hours at 1 tick/min)
        var shiftFactor = 1.0;
        if (_tickCount % 480 < 30) // First 30 minutes of shift: ramp up
        {
            shiftFactor = 0.85 + ((_tickCount % 480) / 30.0) * 0.15;
        }

        // Apply gradual drift (simulates equipment degradation)
        var drift = 0.0;
        if (hasDrift)
        {
            drift = Math.Sin(_tickCount * 0.01) * variance * 0.3;
        }

        // Random variation
        var noise = (_random.NextDouble() - 0.5) * 2 * variance * 0.3;

        // Occasional spike (1% chance)
        var spike = 0.0;
        if (_random.NextDouble() < 0.01)
        {
            spike = variance * (_random.NextDouble() > 0.5 ? 1.5 : -1.0);
            _logger.LogInformation("Spike on {Equipment}/{Metric}: {SpikeValue:F1}", equipmentId, metricType, spike);
        }

        var newValue = baseValue * shiftFactor + noise + drift + spike;

        // Clamp percentages
        if (isPercentage)
        {
            newValue = Math.Clamp(newValue, 0, 100);
        }

        // Smooth transition (weighted average with previous)
        newValue = currentValue * 0.3 + newValue * 0.7;
        _currentValues[key] = newValue;

        return Math.Round(newValue, 2);
    }

    private static object CreateMetric(string equipmentId, string metricType, double value, string unit)
    {
        return new
        {
            EquipmentId = equipmentId,
            MetricType = metricType,
            Value = value,
            Unit = unit,
            Timestamp = DateTime.UtcNow
        };
    }

    private static List<EquipmentConfig> GetDefaultEquipment()
    {
        return new List<EquipmentConfig>
        {
            new("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "CNC-001", 350, 250, 150),
            new("b2c3d4e5-f6a7-8901-bcde-f12345678901", "CONV-001", 180, 500, 80),
            new("c3d4e5f6-a7b8-9012-cdef-123456789012", "TEMP-001", 320, 100, 50),
            new("d4e5f6a7-b8c9-0123-defa-234567890123", "ROB-001", 200, 180, 120),
            new("e5f6a7b8-c9d0-1234-efab-345678901234", "QC-001", 150, 400, 90),
        };
    }

    private record EquipmentConfig(string Id, string Name, double BaseTemp, double BaseThroughput, double BasePower);
}
