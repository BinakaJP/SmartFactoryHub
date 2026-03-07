using Microsoft.AspNetCore.SignalR;

namespace Notification.API.Hubs;

/// <summary>
/// SignalR hub for real-time factory notifications.
/// Angular clients connect to /hubs/factory and listen for:
///   - ReceiveNotification(notification) — pushed on every new notification
///   - ReceiveAlert(notification)        — pushed only for alert-type notifications
/// </summary>
public class FactoryNotificationsHub : Hub
{
    private readonly ILogger<FactoryNotificationsHub> _logger;

    public FactoryNotificationsHub(ILogger<FactoryNotificationsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to FactoryNotificationsHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from FactoryNotificationsHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Allows clients to join a named group (e.g. by severity or equipmentId).</summary>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} joined group {Group}", Context.ConnectionId, groupName);
    }

    /// <summary>Allows clients to leave a named group.</summary>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
