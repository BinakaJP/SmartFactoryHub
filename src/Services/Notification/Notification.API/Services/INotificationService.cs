using BuildingBlocks.Common.Events;
using Notification.API.Dtos;

namespace Notification.API.Services;

public interface INotificationService
{
    Task HandleAlertTriggeredAsync(AlertTriggeredEvent evt);
    Task HandleEquipmentStatusChangedAsync(EquipmentStatusChangedEvent evt);
    Task<IEnumerable<NotificationDto>> GetRecentAsync(int count = 50);
    Task<NotificationSummaryDto> GetSummaryAsync();
    Task<bool> MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync();
}
