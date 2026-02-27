using System.ComponentModel.DataAnnotations;

namespace Metrics.API.DTOs;

public record IngestMetricDto
{
    [Required, MaxLength(50)]
    public string EquipmentId { get; init; } = string.Empty;

    [Required, MaxLength(50)]
    public string MetricType { get; init; } = string.Empty;

    [Required]
    public double Value { get; init; }

    [MaxLength(20)]
    public string Unit { get; init; } = string.Empty;

    public DateTime? Timestamp { get; init; }
}

public record BatchIngestDto
{
    [Required]
    public List<IngestMetricDto> Metrics { get; init; } = new();
}

public record MetricQueryResult
{
    public string EquipmentId { get; init; } = string.Empty;
    public string MetricType { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record MetricAggregation
{
    public string EquipmentId { get; init; } = string.Empty;
    public string MetricType { get; init; } = string.Empty;
    public double Average { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public int Count { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
}

public record DashboardSummary
{
    public double AverageOEE { get; init; }
    public double AverageYield { get; init; }
    public double TotalThroughput { get; init; }
    public int ActiveAlertThresholds { get; init; }
    public int TotalDataPoints { get; init; }
    public DateTime LastUpdated { get; init; }
}
