using Microsoft.AspNetCore.Mvc;
using Notification.API.Dtos;
using Notification.API.Services;

namespace Notification.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Get recent notifications (most recent first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationDto>), 200)]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 50)
    {
        var notifications = await _notificationService.GetRecentAsync(count);
        return Ok(notifications);
    }

    /// <summary>Summary counts of total, unread, and by type.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(NotificationSummaryDto), 200)]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _notificationService.GetSummaryAsync();
        return Ok(summary);
    }

    /// <summary>Mark a single notification as read.</summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var found = await _notificationService.MarkAsReadAsync(id);
        return found ? NoContent() : NotFound();
    }

    /// <summary>Mark all notifications as read.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkAllAsReadAsync();
        return NoContent();
    }
}
