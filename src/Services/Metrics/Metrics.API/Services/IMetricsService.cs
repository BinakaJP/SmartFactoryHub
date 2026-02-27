using Metrics.API.DTOs;

namespace Metrics.API.Services;

public interface IMetricsService
{
    Task<MetricQueryResult> IngestAsync(IngestMetricDto dto);
    Task<IEnumerable<MetricQueryResult>> IngestBatchAsync(BatchIngestDto batch);
    Task<IEnumerable<MetricQueryResult>> GetLatestAsync(string equipmentId, string? metricType = null, int count = 100);
    Task<IEnumerable<MetricQueryResult>> GetByTimeRangeAsync(string equipmentId, string metricType, DateTime from, DateTime to);
    Task<MetricAggregation?> GetAggregationAsync(string equipmentId, string metricType, DateTime from, DateTime to);
    Task<DashboardSummary> GetDashboardSummaryAsync();
}
