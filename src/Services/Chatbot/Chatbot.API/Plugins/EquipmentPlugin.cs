using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Chatbot.API.Plugins;

public sealed class EquipmentPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EquipmentPlugin> _logger;

    public EquipmentPlugin(IHttpClientFactory httpClientFactory, ILogger<EquipmentPlugin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [KernelFunction, Description("Get a list of all factory equipment, optionally filtered by status (Running, Idle, Maintenance, Fault, Offline) or location.")]
    public async Task<string> GetEquipmentList(
        [Description("Optional status filter: Running, Idle, Maintenance, Fault, Offline")] string? status = null,
        [Description("Optional location filter")] string? location = null)
    {
        var query = BuildQuery(("status", status), ("location", location));
        return await CallAsync($"/api/Equipment{query}");
    }

    [KernelFunction, Description("Get full details for a single piece of equipment by its code (e.g. CNC-001, ROB-001, CONV-001).")]
    public async Task<string> GetEquipmentByCode(
        [Description("The equipment code, e.g. CNC-001")] string code)
    {
        return await CallAsync($"/api/Equipment/by-code/{Uri.EscapeDataString(code)}");
    }

    [KernelFunction, Description("Get a count of equipment grouped by status (Running, Idle, Maintenance, Fault, Offline) across the whole factory.")]
    public async Task<string> GetEquipmentStatusSummary()
    {
        return await CallAsync("/api/Equipment/status/summary");
    }

    [KernelFunction, Description("Update the status of a piece of equipment. Requires Engineer or Admin role. Status values: Running, Idle, Maintenance, Fault, Offline.")]
    public async Task<string> UpdateEquipmentStatus(
        [Description("The equipment GUID id")] string id,
        [Description("New status: Running, Idle, Maintenance, Fault, Offline")] string status,
        [Description("Reason for the status change")] string reason)
    {
        var client = _httpClientFactory.CreateClient("EquipmentApi");
        try
        {
            var body = System.Text.Json.JsonSerializer.Serialize(new { status, reason });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/api/Equipment/{Uri.EscapeDataString(id)}/status", content);
            var responseText = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? $"Status updated successfully: {responseText}"
                : $"Failed to update status (HTTP {(int)response.StatusCode}): {responseText}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UpdateEquipmentStatus failed for {Id}", id);
            return $"Error updating equipment status: {ex.Message}";
        }
    }

    private async Task<string> CallAsync(string path)
    {
        var client = _httpClientFactory.CreateClient("EquipmentApi");
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
            _logger.LogWarning(ex, "EquipmentPlugin call to {Path} failed", path);
            return $"Equipment service unavailable: {ex.Message}";
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
