namespace Notification.API.Dtos;

public record NotificationDto(
    Guid Id,
    string NotificationType,
    string EquipmentId,
    string EquipmentName,
    string Message,
    string Severity,
    string Channels,
    bool IsRead,
    DateTime CreatedAt
);

public record NotificationSummaryDto(
    int Total,
    int Unread,
    int Alerts,
    int EquipmentStatusChanges
);
