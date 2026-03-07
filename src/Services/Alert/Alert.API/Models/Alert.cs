namespace Alert.API.Models;

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public double TriggerValue { get; set; }
    public double ThresholdValue { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Open;
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum AlertSeverity { Warning, Critical }
public enum AlertStatus { Open, Acknowledged, Resolved }
