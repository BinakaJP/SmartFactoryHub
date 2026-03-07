namespace Alert.API.Dtos;

public record AlertDto(
    Guid Id,
    string EquipmentId,
    string EquipmentName,
    string MetricType,
    double TriggerValue,
    double ThresholdValue,
    string Severity,
    string Status,
    string? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    string? ResolutionNote,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AcknowledgeAlertDto(string AcknowledgedBy);

public record ResolveAlertDto(string? ResolutionNote);

public record AlertSummaryDto(
    int Total,
    int Open,
    int Acknowledged,
    int Resolved,
    int Warning,
    int Critical
);
