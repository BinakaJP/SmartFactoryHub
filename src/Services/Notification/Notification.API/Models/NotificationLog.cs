namespace Notification.API.Models;

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>"Alert" | "EquipmentStatus"</summary>
    public string NotificationType { get; set; } = string.Empty;

    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>"Warning" | "Critical" | "Info"</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>"SignalR" | "Email" | "SignalR,Email"</summary>
    public string Channels { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
