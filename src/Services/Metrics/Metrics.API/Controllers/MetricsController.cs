using Metrics.API.DTOs;
using Metrics.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Metrics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;

    public MetricsController(IMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    /// <summary>Ingest a single metric data point.</summary>
    [HttpPost("ingest")]
    [ProducesResponseType(typeof(MetricQueryResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<MetricQueryResult>> Ingest([FromBody] IngestMetricDto dto)
    {
        var result = await _metricsService.IngestAsync(dto);
        return Created("", result);
    }

    /// <summary>Ingest a batch of metric data points.</summary>
    [HttpPost("ingest/batch")]
    [ProducesResponseType(typeof(IEnumerable<MetricQueryResult>), StatusCodes.Status201Created)]
    public async Task<ActionResult<IEnumerable<MetricQueryResult>>> IngestBatch([FromBody] BatchIngestDto batch)
    {
        var results = await _metricsService.IngestBatchAsync(batch);
        return Created("", results);
    }

    /// <summary>Get latest metrics for an equipment.</summary>
    [HttpGet("equipment/{equipmentId}/latest")]
    public async Task<ActionResult<IEnumerable<MetricQueryResult>>> GetLatest(
        string equipmentId, [FromQuery] string? metricType = null, [FromQuery] int count = 100)
    {
        var results = await _metricsService.GetLatestAsync(equipmentId, metricType, count);
        return Ok(results);
    }

    /// <summary>Get metrics for a time range.</summary>
    [HttpGet("equipment/{equipmentId}/{metricType}")]
    public async Task<ActionResult<IEnumerable<MetricQueryResult>>> GetByTimeRange(
        string equipmentId, string metricType, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var results = await _metricsService.GetByTimeRangeAsync(equipmentId, metricType, from, to);
        return Ok(results);
    }

    /// <summary>Get aggregated metrics (avg, min, max) for a time range.</summary>
    [HttpGet("equipment/{equipmentId}/{metricType}/aggregate")]
    public async Task<ActionResult<MetricAggregation>> GetAggregation(
        string equipmentId, string metricType, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var result = await _metricsService.GetAggregationAsync(equipmentId, metricType, from, to);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get dashboard summary with KPIs.</summary>
    [HttpGet("dashboard/summary")]
    public async Task<ActionResult<DashboardSummary>> GetDashboardSummary()
    {
        var summary = await _metricsService.GetDashboardSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "Metrics.API", Timestamp = DateTime.UtcNow });
}
