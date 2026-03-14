namespace Analytics.API.Dtos;

public record EquipmentHealthDto(
    string   EquipmentId,
    string   EquipmentName,
    double   HealthScore,
    string   Severity,
    string   Trend,
    int?     EstimatedDaysToMaintenance,
    string[] TriggeringMetrics,
    DateTime ComputedAt);

public record MaintenanceScheduleDto(
    string   EquipmentId,
    string   EquipmentName,
    double   HealthScore,
    string   Severity,
    int      EstimatedDaysToMaintenance,
    string[] TriggeringMetrics);

public record AnalyticsDashboardDto(
    IEnumerable<EquipmentHealthDto> HealthScores,
    int    Anomalies24h,
    int    CriticalAnomalies24h,
    int    EquipmentNeedingMaintenance,
    double AvgFleetHealth);
