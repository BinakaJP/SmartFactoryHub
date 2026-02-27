namespace BuildingBlocks.Common.Events;

/// <summary>
/// Base class for all integration events published across microservices via RabbitMQ.
/// </summary>
public abstract class IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}

/// <summary>
/// Published when a new metric data point is recorded that breaches a threshold.
/// Consumed by Alert.API to create alerts.
/// </summary>
public class MetricThresholdBreachedEvent : IntegrationEvent
{
    public required string EquipmentId { get; init; }
    public required string EquipmentName { get; init; }
    public required string MetricType { get; init; }
    public required double Value { get; init; }
    public required double ThresholdValue { get; init; }
    public required string Severity { get; init; } // "Warning" | "Critical"
}

/// <summary>
/// Published when equipment status changes (e.g., Running → Down).
/// Consumed by Notification.API and Metrics.API.
/// </summary>
public class EquipmentStatusChangedEvent : IntegrationEvent
{
    public required string EquipmentId { get; init; }
    public required string EquipmentName { get; init; }
    public required string PreviousStatus { get; init; }
    public required string NewStatus { get; init; }
}

/// <summary>
/// Published when an alert is triggered.
/// Consumed by Notification.API to send email/push notifications.
/// </summary>
public class AlertTriggeredEvent : IntegrationEvent
{
    public required string AlertId { get; init; }
    public required string EquipmentId { get; init; }
    public required string EquipmentName { get; init; }
    public required string Message { get; init; }
    public required string Severity { get; init; }
}

/// <summary>
/// Published when a metric data point is recorded.
/// Used for real-time dashboard updates.
/// </summary>
public class MetricRecordedEvent : IntegrationEvent
{
    public required string EquipmentId { get; init; }
    public required string MetricType { get; init; }
    public required double Value { get; init; }
    public required string Unit { get; init; }
}
