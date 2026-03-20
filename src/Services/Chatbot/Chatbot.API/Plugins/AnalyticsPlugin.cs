using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Chatbot.API.Plugins;

public sealed class AnalyticsPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnalyticsPlugin> _logger;

    public AnalyticsPlugin(IHttpClientFactory httpClientFactory, ILogger<AnalyticsPlugin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [KernelFunction, Description("Get health score (0-100) and remaining useful life (RUL) prediction for one or all pieces of equipment. Lower score = closer to needing maintenance.")]
    public async Task<string> GetEquipmentHealth(
        [Description("Optional equipment ID (GUID) to get health for a specific machine. Leave empty for all equipment.")] string? equipmentId = null)
    {
        var path = equipmentId is not null
            ? $"/api/Analytics/health/{Uri.EscapeDataString(equipmentId)}"
            : "/api/Analytics/health";
        return await CallAsync(path);
    }

    [KernelFunction, Description("Get recent anomaly detections, optionally filtered by equipment ID or time window. Anomalies are statistically unusual metric values detected by Z-Score, EWMA, or Rate-of-Change algorithms.")]
    public async Task<string> GetAnomalies(
        [Description("Optional equipment ID (GUID) to filter anomalies")] string? equipmentId = null,
        [Description("How many hours back to look (default 24)")] int hours = 24)
    {
        var query = BuildQuery(("equipmentId", equipmentId), ("hours", hours.ToString()));
        return await CallAsync($"/api/Analytics/anomalies{query}");
    }

    [KernelFunction, Description("Get the maintenance schedule — all equipment ordered by urgency (those needing maintenance soonest first), with estimated days to maintenance and health scores.")]
    public async Task<string> GetMaintenanceSchedule()
    {
        return await CallAsync("/api/Analytics/maintenance-schedule");
    }

    private async Task<string> CallAsync(string path)
    {
        var client = _httpClientFactory.CreateClient("AnalyticsApi");
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
            _logger.LogWarning(ex, "AnalyticsPlugin call to {Path} failed", path);
            return $"Analytics service unavailable: {ex.Message}";
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
