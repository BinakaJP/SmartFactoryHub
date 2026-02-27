using Equipment.API.DTOs;
using Equipment.API.Models;
using Equipment.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Equipment.API.Controllers;

/// <summary>
/// REST API controller for managing factory equipment.
/// Demonstrates RESTful API design with proper HTTP status codes,
/// filtering, and Swagger documentation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentService _equipmentService;
    private readonly ILogger<EquipmentController> _logger;

    public EquipmentController(IEquipmentService equipmentService, ILogger<EquipmentController> logger)
    {
        _equipmentService = equipmentService;
        _logger = logger;
    }

    /// <summary>
    /// Get all equipment with optional filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EquipmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EquipmentDto>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? location = null,
        [FromQuery] string? line = null)
    {
        var equipment = await _equipmentService.GetAllAsync(status, location, line);
        return Ok(equipment);
    }

    /// <summary>
    /// Get equipment by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentDto>> GetById(Guid id)
    {
        var equipment = await _equipmentService.GetByIdAsync(id);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    /// <summary>
    /// Get equipment by equipment code.
    /// </summary>
    [HttpGet("code/{equipmentCode}")]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentDto>> GetByCode(string equipmentCode)
    {
        var equipment = await _equipmentService.GetByCodeAsync(equipmentCode);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    /// <summary>
    /// Create new equipment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EquipmentDto>> Create([FromBody] CreateEquipmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var equipment = await _equipmentService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = equipment.Id }, equipment);
    }

    /// <summary>
    /// Update existing equipment.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentDto>> Update(Guid id, [FromBody] UpdateEquipmentDto dto)
    {
        var equipment = await _equipmentService.UpdateAsync(id, dto);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    /// <summary>
    /// Update equipment status (e.g., Running → Down).
    /// Publishes EquipmentStatusChangedEvent to RabbitMQ.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentDto>> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var equipment = await _equipmentService.UpdateStatusAsync(id, dto.Status);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    /// <summary>
    /// Soft-delete equipment (marks as decommissioned).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _equipmentService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }

    /// <summary>
    /// Get equipment status summary (counts by status).
    /// Used by the Angular dashboard for the status overview widget.
    /// </summary>
    [HttpGet("summary/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetStatusSummary()
    {
        var summary = await _equipmentService.GetStatusSummaryAsync();
        return Ok(summary);
    }

    /// <summary>
    /// Health check endpoint for Kubernetes liveness probe.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "Equipment.API", Timestamp = DateTime.UtcNow });
}
