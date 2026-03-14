using Analytics.API.Core;
using Analytics.API.Data;
using Analytics.API.Dtos;
using Analytics.API.Models;
using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Analytics.API.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AnalyticsDbContext  _db;
    private readonly AnalyticsEngine     _engine;
    private readonly IEventBus           _eventBus;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        AnalyticsDbContext db,
        AnalyticsEngine engine,
        IEventBus eventBus,
        ILogger<AnalyticsService> logger)
    {
        _db       = db;
        _engine   = engine;
        _eventBus = eventBus;
        _logger   = logger;
    }

    // ── Process incoming metric ───────────────────────────────────────────────

    public async Task ProcessMetricAsync(MetricRecordedEvent evt)
    {
        // 1. Run detection algorithms
        var result = _engine.RecordAndAnalyze(
            evt.EquipmentId, evt.EquipmentId, evt.MetricType, evt.Value);

        // 2. Persist and publish if anomaly detected (Suspicious+ level)
        if (result.Severity > AnomalySeverity.None && result.HasSufficientHistory)
        {
            double deviationPct = result.ExpectedValue > 1e-9
                ? Math.Abs(evt.Value - result.ExpectedValue) / result.ExpectedValue * 100.0
                : 0;

            var record = new AnomalyRecord
            {
                EquipmentId     = evt.EquipmentId,
                EquipmentName   = evt.EquipmentId,  // name resolved from engine if available
                MetricType      = evt.MetricType,
                Value           = evt.Value,
                ExpectedValue   = result.ExpectedValue,
                ZScore          = result.ZScore != 0 ? result.ZScore : null,
                DeviationPercent = Math.Round(deviationPct, 2),
                Method          = result.TriggeringMethod ?? "ZScore",
                Severity        = result.Severity.ToString()
            };

            _db.Anomalies.Add(record);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Anomaly detected — Equipment: {Equipment}, Metric: {Metric}, Value: {Value:F2}, " +
                "Expected: {Expected:F2}, Z-Score: {Z:F2}, Method: {Method}, Severity: {Severity}",
                evt.EquipmentId, evt.MetricType, evt.Value,
                result.ExpectedValue, result.ZScore, record.Method, record.Severity);

            // Only publish event for Anomalous/Critical (not Suspicious — too noisy)
            if (result.Severity >= AnomalySeverity.Anomalous)
            {
                try
                {
                    await _eventBus.PublishAsync(new AnomalyDetectedEvent
                    {
                        EquipmentId      = evt.EquipmentId,
                        EquipmentName    = evt.EquipmentId,
                        MetricType       = evt.MetricType,
                        Value            = evt.Value,
                        ExpectedValue    = result.ExpectedValue,
                        DeviationPercent = Math.Round(deviationPct, 2),
                        Method           = record.Method,
                        Severity         = record.Severity
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish AnomalyDetectedEvent for {Equipment}", evt.EquipmentId);
                }
            }
        }

        // 3. Refresh health score and publish maintenance prediction if warranted
        var health = _engine.ComputeHealth(evt.EquipmentId);
        if (health is { ShouldPublishMaintenanceEvent: true })
        {
            string action = health.Severity switch
            {
                "Urgent"  => "Schedule emergency maintenance immediately",
                "Warning" => $"Schedule preventive maintenance within {health.EstimatedDaysToMaintenance} days",
                _         => "Monitor closely and plan maintenance soon"
            };

            try
            {
                await _eventBus.PublishAsync(new MaintenancePredictedEvent
                {
                    EquipmentId                  = health.EquipmentId,
                    EquipmentCode                = health.EquipmentId,
                    EquipmentName                = health.EquipmentName,
                    HealthScore                  = health.HealthScore,
                    EstimatedDaysToMaintenance   = health.EstimatedDaysToMaintenance ?? 0,
                    TriggeringMetrics            = health.TriggeringMetrics,
                    Severity                     = health.Severity,
                    RecommendedAction            = action
                });

                _logger.LogWarning(
                    "Maintenance predicted — Equipment: {Equipment}, Health: {Score:F1}, " +
                    "Severity: {Severity}, Days: {Days}, Metrics: [{Metrics}]",
                    health.EquipmentId, health.HealthScore, health.Severity,
                    health.EstimatedDaysToMaintenance,
                    string.Join(", ", health.TriggeringMetrics));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish MaintenancePredictedEvent for {Equipment}", health.EquipmentId);
            }
        }
    }

    // ── Query methods ─────────────────────────────────────────────────────────

    public async Task<IEnumerable<AnomalyDto>> GetAnomaliesAsync(
        string? equipmentId, string? severity, int hours, int count)
    {
        var since = DateTime.UtcNow.AddHours(-hours);
        var query = _db.Anomalies.Where(a => a.DetectedAt >= since);

        if (!string.IsNullOrEmpty(equipmentId))
            query = query.Where(a => a.EquipmentId == equipmentId);

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(a => a.Severity == severity);

        var records = await query
            .OrderByDescending(a => a.DetectedAt)
            .Take(count)
            .ToListAsync();

        return records.Select(ToDto);
    }

    public async Task<AnomalyDto?> GetAnomalyByIdAsync(Guid id)
    {
        var record = await _db.Anomalies.FindAsync(id);
        return record is null ? null : ToDto(record);
    }

    public async Task<AnomalyDto?> AcknowledgeAnomalyAsync(Guid id)
    {
        var record = await _db.Anomalies.FindAsync(id);
        if (record is null) return null;

        record.IsAcknowledged = true;
        record.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(record);
    }

    public Task<IEnumerable<EquipmentHealthDto>> GetHealthScoresAsync()
    {
        var scores = _engine.GetAllHealth().Select(ToHealthDto);
        return Task.FromResult(scores);
    }

    public Task<EquipmentHealthDto?> GetHealthScoreAsync(string equipmentId)
    {
        var health = _engine.ComputeHealth(equipmentId);
        return Task.FromResult(health is null ? null : ToHealthDto(health));
    }

    public Task<IEnumerable<MaintenanceScheduleDto>> GetMaintenanceScheduleAsync()
    {
        var schedule = _engine.GetAllHealth()
            .Where(h => h.EstimatedDaysToMaintenance.HasValue)
            .OrderBy(h => h.EstimatedDaysToMaintenance)
            .Select(h => new MaintenanceScheduleDto(
                h.EquipmentId,
                h.EquipmentName,
                h.HealthScore,
                h.Severity,
                h.EstimatedDaysToMaintenance!.Value,
                h.TriggeringMetrics));

        return Task.FromResult(schedule);
    }

    public async Task<AnalyticsDashboardDto> GetDashboardAsync()
    {
        var since24h = DateTime.UtcNow.AddHours(-24);

        var anomalies24h = await _db.Anomalies
            .Where(a => a.DetectedAt >= since24h)
            .CountAsync();

        var critical24h = await _db.Anomalies
            .Where(a => a.DetectedAt >= since24h && a.Severity == "Critical")
            .CountAsync();

        var healthScores = _engine.GetAllHealth().ToList();
        double avgHealth = healthScores.Count > 0 ? healthScores.Average(h => h.HealthScore) : 100;
        int needMaintenance = healthScores.Count(h =>
            h.EstimatedDaysToMaintenance.HasValue && h.EstimatedDaysToMaintenance <= 14);

        return new AnalyticsDashboardDto(
            HealthScores:                 healthScores.Select(ToHealthDto),
            Anomalies24h:                 anomalies24h,
            CriticalAnomalies24h:         critical24h,
            EquipmentNeedingMaintenance:  needMaintenance,
            AvgFleetHealth:               Math.Round(avgHealth, 1));
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static AnomalyDto ToDto(AnomalyRecord r) => new(
        r.Id, r.EquipmentId, r.EquipmentName, r.MetricType,
        r.Value, r.ExpectedValue, r.DeviationPercent, r.ZScore,
        r.Method, r.Severity, r.DetectedAt, r.IsAcknowledged);

    private static EquipmentHealthDto ToHealthDto(EquipmentHealth h) => new(
        h.EquipmentId, h.EquipmentName, h.HealthScore, h.Severity,
        h.Trend, h.EstimatedDaysToMaintenance, h.TriggeringMetrics, h.ComputedAt);
}
