using Analytics.API.Dtos;
using Analytics.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IAnalyticsService analytics, ILogger<AnalyticsController> logger)
    {
        _analytics = analytics;
        _logger    = logger;
    }

    // ── Anomalies ──────────────────────────────────────────────────────────────

    /// <summary>
    /// List anomalies with optional filters.
    /// </summary>
    /// <param name="equipmentId">Filter by equipment ID.</param>
    /// <param name="severity">Filter by severity: Suspicious | Anomalous | Critical.</param>
    /// <param name="hours">Look-back window in hours (default 24).</param>
    /// <param name="count">Maximum records to return (default 100, max 500).</param>
    [HttpGet("anomalies")]
    [ProducesResponseType(typeof(IEnumerable<AnomalyDto>), 200)]
    public async Task<IActionResult> GetAnomalies(
        [FromQuery] string? equipmentId = null,
        [FromQuery] string? severity    = null,
        [FromQuery] int     hours       = 24,
        [FromQuery] int     count       = 100)
    {
        count = Math.Clamp(count, 1, 500);
        hours = Math.Clamp(hours, 1, 720); // max 30 days
        var anomalies = await _analytics.GetAnomaliesAsync(equipmentId, severity, hours, count);
        return Ok(anomalies);
    }

    /// <summary>Get a single anomaly record by ID.</summary>
    [HttpGet("anomalies/{id:guid}")]
    [ProducesResponseType(typeof(AnomalyDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAnomalyById(Guid id)
    {
        var anomaly = await _analytics.GetAnomalyByIdAsync(id);
        return anomaly is null ? NotFound() : Ok(anomaly);
    }

    /// <summary>Acknowledge an anomaly (mark as reviewed).</summary>
    [HttpPatch("anomalies/{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(AnomalyDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AcknowledgeAnomaly(Guid id)
    {
        var anomaly = await _analytics.AcknowledgeAnomalyAsync(id);
        return anomaly is null ? NotFound() : Ok(anomaly);
    }

    // ── Health Scores ──────────────────────────────────────────────────────────

    /// <summary>
    /// Get current health scores for all tracked equipment.
    /// Health is computed from Z-Score normalcy weighted across all metric types.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(IEnumerable<EquipmentHealthDto>), 200)]
    public async Task<IActionResult> GetHealthScores()
    {
        var scores = await _analytics.GetHealthScoresAsync();
        return Ok(scores);
    }

    /// <summary>Get health score for a specific piece of equipment.</summary>
    [HttpGet("health/{equipmentId}")]
    [ProducesResponseType(typeof(EquipmentHealthDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetHealthScore(string equipmentId)
    {
        var health = await _analytics.GetHealthScoreAsync(equipmentId);
        return health is null ? NotFound() : Ok(health);
    }

    // ── Maintenance ────────────────────────────────────────────────────────────

    /// <summary>
    /// Get maintenance schedule ordered by urgency (soonest first).
    /// Only equipment with a Remaining Useful Life estimate is included.
    /// </summary>
    [HttpGet("maintenance/schedule")]
    [ProducesResponseType(typeof(IEnumerable<MaintenanceScheduleDto>), 200)]
    public async Task<IActionResult> GetMaintenanceSchedule()
    {
        var schedule = await _analytics.GetMaintenanceScheduleAsync();
        return Ok(schedule);
    }

    // ── Dashboard ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Analytics dashboard: fleet health overview, anomaly counts, and maintenance needs.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AnalyticsDashboardDto), 200)]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await _analytics.GetDashboardAsync();
        return Ok(dashboard);
    }
}
