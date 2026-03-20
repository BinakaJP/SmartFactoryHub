using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Chatbot.API.Plugins;

public sealed class MetricsPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MetricsPlugin> _logger;

    public MetricsPlugin(IHttpClientFactory httpClientFactory, ILogger<MetricsPlugin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [KernelFunction, Description("Get the most recent metric readings for a piece of equipment. Optionally filter by metric type (Temperature, Vibration, OEE, YieldRate, PowerConsumption, Throughput).")]
    public async Task<string> GetLatestMetrics(
        [Description("The equipment ID (GUID)")] string equipmentId,
        [Description("Optional metric type: Temperature, Vibration, OEE, YieldRate, PowerConsumption, Throughput")] string? metricType = null,
        [Description("Number of readings to return (default 10, max 100)")] int count = 10)
    {
        var query = BuildQuery(("metricType", metricType), ("count", count.ToString()));
        return await CallAsync($"/api/Metrics/equipment/{Uri.EscapeDataString(equipmentId)}/latest{query}");
    }

    [KernelFunction, Description("Get aggregated metric statistics (min, max, average) for a piece of equipment over a time range. Use fromHours=8 for the last 8 hours, toHours=0 for now.")]
    public async Task<string> GetMetricAggregation(
        [Description("The equipment ID (GUID)")] string equipmentId,
        [Description("Metric type: Temperature, Vibration, OEE, YieldRate, PowerConsumption, Throughput")] string metricType,
        [Description("How many hours ago to start (e.g. 8 = last 8 hours)")] int fromHours = 8,
        [Description("How many hours ago to end (0 = now)")] int toHours = 0)
    {
        var query = BuildQuery(("fromHours", fromHours.ToString()), ("toHours", toHours.ToString()));
        return await CallAsync($"/api/Metrics/equipment/{Uri.EscapeDataString(equipmentId)}/{Uri.EscapeDataString(metricType)}/aggregate{query}");
    }

    [KernelFunction, Description("Get a factory-wide dashboard summary showing overall KPIs: OEE, throughput, active alerts count, equipment health overview.")]
    public async Task<string> GetDashboardSummary()
    {
        return await CallAsync("/api/Metrics/dashboard/summary");
    }

    private async Task<string> CallAsync(string path)
    {
        var client = _httpClientFactory.CreateClient("MetricsApi");
        try
        {
            var response = await client.GetAsync(path);
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? content
                : $"Service returned HTTP {(int)response.StatusCode}: {content}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MetricsPlugin call to {Path} failed", path);
            return $"Metrics service unavailable: {ex.Message}";
        }
    }

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? $"?{qs}" : string.Empty;
    }
}
