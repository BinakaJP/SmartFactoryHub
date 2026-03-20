using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Chatbot.API.Plugins;

public sealed class AlertPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AlertPlugin> _logger;

    public AlertPlugin(IHttpClientFactory httpClientFactory, ILogger<AlertPlugin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [KernelFunction, Description("Get active alerts, optionally filtered by severity (Info, Warning, Critical) or equipment ID. Returns open and acknowledged alerts.")]
    public async Task<string> GetActiveAlerts(
        [Description("Optional severity filter: Info, Warning, Critical")] string? severity = null,
        [Description("Optional equipment ID (GUID) to filter alerts for one piece of equipment")] string? equipmentId = null)
    {
        var query = BuildQuery(("severity", severity), ("equipmentId", equipmentId), ("status", "Open"));
        return await CallAsync($"/api/Alerts{query}");
    }

    [KernelFunction, Description("Get a count summary of alerts grouped by status (Open, Acknowledged, Resolved) and severity (Info, Warning, Critical).")]
    public async Task<string> GetAlertSummary()
    {
        return await CallAsync("/api/Alerts/summary");
    }

    [KernelFunction, Description("Acknowledge an open alert by its ID. Records who acknowledged it. Requires Engineer or Admin role.")]
    public async Task<string> AcknowledgeAlert(
        [Description("The alert GUID id")] string alertId,
        [Description("The name or email of the person acknowledging the alert")] string acknowledgedBy)
    {
        var client = _httpClientFactory.CreateClient("AlertApi");
        try
        {
            var body = System.Text.Json.JsonSerializer.Serialize(new { acknowledgedBy });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/api/Alerts/{Uri.EscapeDataString(alertId)}/acknowledge", content);
            var responseText = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? $"Alert {alertId} acknowledged by {acknowledgedBy}: {responseText}"
                : $"Failed to acknowledge alert (HTTP {(int)response.StatusCode}): {responseText}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AcknowledgeAlert failed for {AlertId}", alertId);
            return $"Alert service unavailable: {ex.Message}";
        }
    }

    private async Task<string> CallAsync(string path)
    {
        var client = _httpClientFactory.CreateClient("AlertApi");
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
            _logger.LogWarning(ex, "AlertPlugin call to {Path} failed", path);
            return $"Alert service unavailable: {ex.Message}";
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
