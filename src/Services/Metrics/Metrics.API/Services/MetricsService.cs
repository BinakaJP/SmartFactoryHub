using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Metrics.API.Data;
using Metrics.API.DTOs;
using Metrics.API.Instrumentation;
using Metrics.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Metrics.API.Services;

public class MetricsService : IMetricsService
{
    private readonly MetricsDbContext _context;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(MetricsDbContext context, IEventBus eventBus, ILogger<MetricsService> logger)
    {
        _context = context;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<MetricQueryResult> IngestAsync(IngestMetricDto dto)
    {
        var dataPoint = new MetricDataPoint
        {
            EquipmentId = dto.EquipmentId,
            MetricType = dto.MetricType,
            Value = dto.Value,
            Unit = dto.Unit,
            Timestamp = dto.Timestamp ?? DateTime.UtcNow
        };

        _context.MetricDataPoints.Add(dataPoint);
        await _context.SaveChangesAsync();

        // Increment custom Prometheus counters
        FactoryMetrics.MetricsIngestTotal.WithLabels(dto.EquipmentId, dto.MetricType).Inc();
        FactoryMetrics.DbWritesTotal.Inc();

        // Update gauge with latest observed value (powers the Grafana equipment metrics dashboard)
        var equipmentName = FactoryMetrics.KnownEquipmentNames.TryGetValue(dto.EquipmentId, out var knownName)
            ? knownName
            : dto.EquipmentId[..8];
        FactoryMetrics.EquipmentMetricValue
            .WithLabels(dto.EquipmentId, equipmentName, dto.MetricType, dto.Unit ?? string.Empty)
            .Set(dto.Value);

        // Check thresholds and publish events if breached
        await CheckThresholdsAsync(dataPoint);

        // Publish metric recorded event for real-time updates
        try
        {
            await _eventBus.PublishAsync(new MetricRecordedEvent
            {
                EquipmentId = dto.EquipmentId,
                MetricType = dto.MetricType,
                Value = dto.Value,
                Unit = dto.Unit
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish MetricRecordedEvent");
        }

        return MapToResult(dataPoint);
    }

    public async Task<IEnumerable<MetricQueryResult>> IngestBatchAsync(BatchIngestDto batch)
    {
        var results = new List<MetricQueryResult>();
        foreach (var dto in batch.Metrics)
        {
            var result = await IngestAsync(dto);
            results.Add(result);
        }

        // Increment batch counter once per batch request
        FactoryMetrics.MetricsIngestBatchTotal.Inc();

        return results;
    }

    public async Task<IEnumerable<MetricQueryResult>> GetLatestAsync(string equipmentId, string? metricType = null, int count = 100)
    {
        var query = _context.MetricDataPoints
            .Where(m => m.EquipmentId == equipmentId);

        if (!string.IsNullOrEmpty(metricType))
            query = query.Where(m => m.MetricType == metricType);

        var data = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync();

        return data.Select(MapToResult);
    }

    public async Task<IEnumerable<MetricQueryResult>> GetByTimeRangeAsync(string equipmentId, string metricType, DateTime from, DateTime to)
    {
        var data = await _context.MetricDataPoints
            .Where(m => m.EquipmentId == equipmentId && m.MetricType == metricType && m.Timestamp >= from && m.Timestamp <= to)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return data.Select(MapToResult);
    }

    public async Task<MetricAggregation?> GetAggregationAsync(string equipmentId, string metricType, DateTime from, DateTime to)
    {
        var data = await _context.MetricDataPoints
            .Where(m => m.EquipmentId == equipmentId && m.MetricType == metricType && m.Timestamp >= from && m.Timestamp <= to)
            .ToListAsync();

        if (!data.Any()) return null;

        return new MetricAggregation
        {
            EquipmentId = equipmentId,
            MetricType = metricType,
            Average = Math.Round(data.Average(d => d.Value), 2),
            Min = data.Min(d => d.Value),
            Max = data.Max(d => d.Value),
            Count = data.Count,
            PeriodStart = from,
            PeriodEnd = to
        };
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentMetrics = await _context.MetricDataPoints
            .Where(m => m.Timestamp >= oneHourAgo)
            .ToListAsync();

        var oeeMetrics = recentMetrics.Where(m => m.MetricType == MetricTypes.OEE).ToList();
        var yieldMetrics = recentMetrics.Where(m => m.MetricType == MetricTypes.YieldRate).ToList();
        var throughputMetrics = recentMetrics.Where(m => m.MetricType == MetricTypes.Throughput).ToList();

        var activeThresholds = await _context.AlertThresholds.CountAsync(t => t.IsActive);

        return new DashboardSummary
        {
            AverageOEE = oeeMetrics.Any() ? Math.Round(oeeMetrics.Average(m => m.Value), 1) : 0,
            AverageYield = yieldMetrics.Any() ? Math.Round(yieldMetrics.Average(m => m.Value), 1) : 0,
            TotalThroughput = throughputMetrics.Any() ? Math.Round(throughputMetrics.Sum(m => m.Value), 0) : 0,
            ActiveAlertThresholds = activeThresholds,
            TotalDataPoints = await _context.MetricDataPoints.CountAsync(),
            LastUpdated = DateTime.UtcNow
        };
    }

    private async Task CheckThresholdsAsync(MetricDataPoint dataPoint)
    {
        var threshold = await _context.AlertThresholds
            .FirstOrDefaultAsync(t =>
                t.EquipmentId == dataPoint.EquipmentId &&
                t.MetricType == dataPoint.MetricType &&
                t.IsActive);

        if (threshold is null) return;

        bool breached = threshold.Direction == ThresholdDirection.Above
            ? dataPoint.Value >= threshold.WarningValue
            : dataPoint.Value <= threshold.WarningValue;

        if (!breached) return;

        bool isCritical = threshold.Direction == ThresholdDirection.Above
            ? dataPoint.Value >= threshold.CriticalValue
            : dataPoint.Value <= threshold.CriticalValue;

        var severity = isCritical ? "Critical" : "Warning";

        // Increment threshold breach counter
        FactoryMetrics.ThresholdBreachesTotal.WithLabels(dataPoint.EquipmentId, dataPoint.MetricType, severity).Inc();

        try
        {
            await _eventBus.PublishAsync(new MetricThresholdBreachedEvent
            {
                EquipmentId = dataPoint.EquipmentId,
                EquipmentName = dataPoint.EquipmentId,
                MetricType = dataPoint.MetricType,
                Value = dataPoint.Value,
                ThresholdValue = isCritical ? threshold.CriticalValue : threshold.WarningValue,
                Severity = severity
            });

            _logger.LogWarning("{Severity} threshold breached for {Equipment}/{Metric}: {Value} (threshold: {Threshold})",
                severity, dataPoint.EquipmentId, dataPoint.MetricType, dataPoint.Value,
                isCritical ? threshold.CriticalValue : threshold.WarningValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish threshold breach event");
        }
    }

    private static MetricQueryResult MapToResult(MetricDataPoint dp) => new()
    {
        EquipmentId = dp.EquipmentId,
        MetricType = dp.MetricType,
        Value = dp.Value,
        Unit = dp.Unit,
        Timestamp = dp.Timestamp
    };
}
