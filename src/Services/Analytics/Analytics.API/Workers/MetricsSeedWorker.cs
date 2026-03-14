using System.Net.Http.Json;
using Analytics.API.Core;

namespace Analytics.API.Workers;

/// <summary>
/// Startup worker that pre-populates the in-memory analytics engine with recent
/// metric history from Metrics.API so that anomaly detection has a warm rolling
/// window from the first live event, rather than needing to collect 15+ samples
/// from scratch after each service restart.
///
/// Runs once on startup; errors are logged as warnings (the service continues
/// operating normally — it just won't have warm windows until live data arrives).
/// </summary>
public class MetricsSeedWorker : IHostedService
{
    // Local DTO — mirrors MetricQueryResult in Metrics.API (no project reference needed)
    private sealed record MetricPoint(string EquipmentId, string MetricType, double Value, DateTime Timestamp);

    private readonly AnalyticsEngine    _engine;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration     _config;
    private readonly ILogger<MetricsSeedWorker> _logger;

    public MetricsSeedWorker(
        AnalyticsEngine engine,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<MetricsSeedWorker> logger)
    {
        _engine     = engine;
        _httpFactory = httpFactory;
        _config     = config;
        _logger     = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var metricsBaseUrl = _config.GetValue<string>("ServiceUrls:MetricsApi");
        if (string.IsNullOrEmpty(metricsBaseUrl))
        {
            _logger.LogWarning("MetricsSeedWorker: ServiceUrls:MetricsApi not configured — skipping seed.");
            return;
        }

        var equipmentIds = _config
            .GetSection("Analytics:SeedEquipmentIds")
            .Get<string[]>() ?? Array.Empty<string>();

        if (equipmentIds.Length == 0)
        {
            _logger.LogInformation("MetricsSeedWorker: No SeedEquipmentIds configured — skipping seed.");
            return;
        }

        _logger.LogInformation(
            "MetricsSeedWorker: Seeding rolling windows for {Count} equipment IDs from {Url}",
            equipmentIds.Length, metricsBaseUrl);

        var http = _httpFactory.CreateClient("MetricsApi");
        http.BaseAddress = new Uri(metricsBaseUrl.TrimEnd('/') + "/");

        int seeded = 0;
        foreach (var equipmentId in equipmentIds)
        {
            if (cancellationToken.IsCancellationRequested) break;
            seeded += await SeedEquipmentAsync(http, equipmentId, cancellationToken);
        }

        _logger.LogInformation(
            "MetricsSeedWorker: Seed complete — {Count} metric/equipment combinations initialized.",
            seeded);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<int> SeedEquipmentAsync(
        HttpClient http, string equipmentId, CancellationToken ct)
    {
        try
        {
            // GET /api/Metrics/equipment/{id}/latest?count=200
            var url = $"api/Metrics/equipment/{Uri.EscapeDataString(equipmentId)}/latest?count=200";
            var points = await http.GetFromJsonAsync<List<MetricPoint>>(url, ct);

            if (points is null || points.Count == 0)
            {
                _logger.LogDebug("MetricsSeedWorker: No history returned for {EquipmentId}", equipmentId);
                return 0;
            }

            // Group by MetricType and seed each window (oldest-first)
            var byMetric = points
                .GroupBy(p => p.MetricType)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Timestamp).Select(p => p.Value).ToList());

            foreach (var (metricType, values) in byMetric)
                _engine.SeedMetric(equipmentId, equipmentId, metricType, values);

            _logger.LogDebug(
                "MetricsSeedWorker: Seeded {Count} metric types for {EquipmentId} ({Points} points total)",
                byMetric.Count, equipmentId, points.Count);

            return byMetric.Count;
        }
        catch (Exception ex)
        {
            // Don't let a single equipment failure abort seeding for others
            _logger.LogWarning(ex,
                "MetricsSeedWorker: Failed to seed history for equipment {EquipmentId} — service will warm up from live data.",
                equipmentId);
            return 0;
        }
    }
}
