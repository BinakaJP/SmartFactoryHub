namespace Analytics.API.Dtos;

public record AnomalyDto(
    Guid     Id,
    string   EquipmentId,
    string   EquipmentName,
    string   MetricType,
    double   Value,
    double   ExpectedValue,
    double   DeviationPercent,
    double?  ZScore,
    string   Method,
    string   Severity,
    DateTime DetectedAt,
    bool     IsAcknowledged);

public record AnomalyAcknowledgeDto(string AcknowledgedBy);
