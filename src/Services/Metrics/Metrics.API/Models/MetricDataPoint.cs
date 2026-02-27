using System.ComponentModel.DataAnnotations;

namespace Metrics.API.Models;

public class MetricDataPoint
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string EquipmentId { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string MetricType { get; set; } = string.Empty;

    public double Value { get; set; }

    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AlertThreshold
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string EquipmentId { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string MetricType { get; set; } = string.Empty;

    public double WarningValue { get; set; }
    public double CriticalValue { get; set; }

    public ThresholdDirection Direction { get; set; } = ThresholdDirection.Above;

    public bool IsActive { get; set; } = true;
}

public enum ThresholdDirection
{
    Above,
    Below
}

public static class MetricTypes
{
    public const string OEE = "OEE";
    public const string Throughput = "Throughput";
    public const string Temperature = "Temperature";
    public const string YieldRate = "YieldRate";
    public const string PowerConsumption = "PowerConsumption";
    public const string Vibration = "Vibration";
    public const string Pressure = "Pressure";
    public const string Humidity = "Humidity";
}
