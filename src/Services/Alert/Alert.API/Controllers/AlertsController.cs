using Alert.API.Dtos;
using Alert.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alert.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertService alertService, ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>Get all alerts with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AlertDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? equipmentId = null)
    {
        var alerts = await _alertService.GetAllAsync(status, severity, equipmentId);
        return Ok(alerts);
    }

    /// <summary>Get a single alert by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AlertDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var alert = await _alertService.GetByIdAsync(id);
        return alert is null ? NotFound() : Ok(alert);
    }

    /// <summary>Summary counts by status and severity.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AlertSummaryDto), 200)]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _alertService.GetSummaryAsync();
        return Ok(summary);
    }

    /// <summary>Acknowledge an open alert.</summary>
    [HttpPatch("{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(AlertDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeAlertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AcknowledgedBy))
            return BadRequest("AcknowledgedBy is required.");

        var alert = await _alertService.AcknowledgeAsync(id, dto.AcknowledgedBy);
        if (alert is null)
            return Conflict("Alert not found or is not in Open status.");

        return Ok(alert);
    }

    /// <summary>Resolve an alert (Open or Acknowledged).</summary>
    [HttpPatch("{id:guid}/resolve")]
    [ProducesResponseType(typeof(AlertDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveAlertDto dto)
    {
        var alert = await _alertService.ResolveAsync(id, dto.ResolutionNote);
        if (alert is null)
            return Conflict("Alert not found or is already resolved.");

        return Ok(alert);
    }
}
