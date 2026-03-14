using Analytics.API.Dtos;
using BuildingBlocks.Common.Events;

namespace Analytics.API.Services;

public interface IAnalyticsService
{
    /// <summary>Process an incoming metric — run detection and persist any anomaly found.</summary>
    Task ProcessMetricAsync(MetricRecordedEvent evt);

    Task<IEnumerable<AnomalyDto>>         GetAnomaliesAsync(string? equipmentId, string? severity, int hours, int count);
    Task<AnomalyDto?>                     GetAnomalyByIdAsync(Guid id);
    Task<AnomalyDto?>                     AcknowledgeAnomalyAsync(Guid id);
    Task<IEnumerable<EquipmentHealthDto>> GetHealthScoresAsync();
    Task<EquipmentHealthDto?>             GetHealthScoreAsync(string equipmentId);
    Task<IEnumerable<MaintenanceScheduleDto>> GetMaintenanceScheduleAsync();
    Task<AnalyticsDashboardDto>           GetDashboardAsync();
}
