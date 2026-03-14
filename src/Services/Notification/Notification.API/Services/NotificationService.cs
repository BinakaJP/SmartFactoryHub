using BuildingBlocks.Common.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Notification.API.Data;
using Notification.API.Dtos;
using Notification.API.Hubs;
using Notification.API.Models;

namespace Notification.API.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _db;
    private readonly IHubContext<FactoryNotificationsHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        NotificationDbContext db,
        IHubContext<FactoryNotificationsHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task HandleAlertTriggeredAsync(AlertTriggeredEvent evt)
    {
        var log = new NotificationLog
        {
            NotificationType = "Alert",
            EquipmentId = evt.EquipmentId,
            EquipmentName = evt.EquipmentName,
            Message = evt.Message,
            Severity = evt.Severity,
            Channels = "SignalR"
        };

        _db.Notifications.Add(log);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Notification created for alert {AlertId} — Equipment: {EquipmentId}, Severity: {Severity}",
            evt.AlertId, evt.EquipmentId, evt.Severity);

        var dto = ToDto(log);

        // Push to all connected clients
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", dto);

        // Also push to the severity group (e.g. "Critical" group)
        await _hubContext.Clients.Group(evt.Severity).SendAsync("ReceiveAlert", dto);
    }

    public async Task HandleEquipmentStatusChangedAsync(EquipmentStatusChangedEvent evt)
    {
        // Only notify on significant status changes
        var isSignificant = evt.NewStatus is "Down" or "Maintenance";
        var severity = evt.NewStatus == "Down" ? "Critical" : "Info";

        var log = new NotificationLog
        {
            NotificationType = "EquipmentStatus",
            EquipmentId = evt.EquipmentId,
            EquipmentName = evt.EquipmentName,
            Message = $"{evt.EquipmentName} status changed: {evt.PreviousStatus} → {evt.NewStatus}",
            Severity = severity,
            Channels = "SignalR"
        };

        _db.Notifications.Add(log);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Equipment status change notification — {EquipmentId}: {Previous} → {New}",
            evt.EquipmentId, evt.PreviousStatus, evt.NewStatus);

        var dto = ToDto(log);
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", dto);

        if (isSignificant)
            await _hubContext.Clients.Group("EquipmentDown").SendAsync("ReceiveEquipmentAlert", dto);
    }

    public async Task HandleAnomalyDetectedAsync(AnomalyDetectedEvent evt)
    {
        var log = new NotificationLog
        {
            NotificationType = "Anomaly",
            EquipmentId      = evt.EquipmentId,
            EquipmentName    = evt.EquipmentName,
            Message          = $"{evt.EquipmentName}: {evt.MetricType} anomaly detected " +
                               $"({evt.Severity}) — value {evt.Value:F2}, expected ~{evt.ExpectedValue:F2} " +
                               $"({evt.DeviationPercent:F1}% deviation, method: {evt.Method})",
            Severity         = evt.Severity,
            Channels         = "SignalR"
        };

        _db.Notifications.Add(log);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Anomaly notification — Equipment: {EquipmentId}, Metric: {Metric}, Severity: {Severity}",
            evt.EquipmentId, evt.MetricType, evt.Severity);

        var dto = ToDto(log);
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", dto);
        await _hubContext.Clients.Group(evt.Severity).SendAsync("ReceiveAnomaly", dto);
    }

    public async Task HandleMaintenancePredictedAsync(MaintenancePredictedEvent evt)
    {
        var log = new NotificationLog
        {
            NotificationType = "Maintenance",
            EquipmentId      = evt.EquipmentId,
            EquipmentName    = evt.EquipmentName,
            Message          = $"{evt.EquipmentName}: maintenance predicted in ~{evt.EstimatedDaysToMaintenance} day(s) " +
                               $"(health: {evt.HealthScore:F1}%, severity: {evt.Severity}). " +
                               $"Action: {evt.RecommendedAction}",
            Severity         = evt.Severity,
            Channels         = "SignalR"
        };

        _db.Notifications.Add(log);
        await _db.SaveChangesAsync();

        _logger.LogWarning(
            "Maintenance notification — Equipment: {EquipmentId}, Health: {Score:F1}%, Days: {Days}, Severity: {Severity}",
            evt.EquipmentId, evt.HealthScore, evt.EstimatedDaysToMaintenance, evt.Severity);

        var dto = ToDto(log);
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", dto);
        await _hubContext.Clients.Group("Maintenance").SendAsync("ReceiveMaintenance", dto);
    }

    public async Task<IEnumerable<NotificationDto>> GetRecentAsync(int count = 50)
    {
        var notifications = await _db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Min(count, 200))
            .ToListAsync();

        return notifications.Select(ToDto);
    }

    public async Task<NotificationSummaryDto> GetSummaryAsync()
    {
        var all = await _db.Notifications.ToListAsync();
        return new NotificationSummaryDto(
            Total: all.Count,
            Unread: all.Count(n => !n.IsRead),
            Alerts: all.Count(n => n.NotificationType == "Alert"),
            EquipmentStatusChanges: all.Count(n => n.NotificationType == "EquipmentStatus")
        );
    }

    public async Task<bool> MarkAsReadAsync(Guid id)
    {
        var notification = await _db.Notifications.FindAsync(id);
        if (notification is null) return false;

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllAsReadAsync()
    {
        await _db.Notifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    private static NotificationDto ToDto(NotificationLog n) => new(
        n.Id, n.NotificationType, n.EquipmentId, n.EquipmentName,
        n.Message, n.Severity, n.Channels, n.IsRead, n.CreatedAt
    );
}
