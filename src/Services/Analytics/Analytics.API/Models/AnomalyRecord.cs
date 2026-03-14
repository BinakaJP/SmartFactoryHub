using System.ComponentModel.DataAnnotations;

namespace Analytics.API.Models;

/// <summary>
/// Persisted record of a detected metric anomaly.
/// Written when Z-Score, EWMA, or Rate-of-Change detection triggers on an incoming metric.
/// </summary>
public class AnomalyRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string EquipmentId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string EquipmentName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string MetricType { get; set; } = string.Empty;

    /// <summary>The observed value that triggered the anomaly.</summary>
    public double Value { get; set; }

    /// <summary>The rolling mean (or EWMA target) at the time of detection.</summary>
    public double ExpectedValue { get; set; }

    /// <summary>Z-Score at time of detection. Null when detected by EWMA or Rate-of-Change only.</summary>
    public double? ZScore { get; set; }

    /// <summary>Percentage deviation from expected value.</summary>
    public double DeviationPercent { get; set; }

    /// <summary>Which algorithm detected the anomaly: ZScore | EWMA | RateOfChange.</summary>
    [Required, MaxLength(30)]
    public string Method { get; set; } = string.Empty;

    /// <summary>Suspicious | Anomalous | Critical.</summary>
    [Required, MaxLength(30)]
    public string Severity { get; set; } = string.Empty;

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public bool IsAcknowledged { get; set; }

    public DateTime? AcknowledgedAt { get; set; }
}
