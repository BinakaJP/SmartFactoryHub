using System.ComponentModel.DataAnnotations;

namespace Equipment.API.Models;

/// <summary>
/// Represents a piece of factory equipment (machine, sensor, production line).
/// </summary>
public class EquipmentEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string EquipmentCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public EquipmentType Type { get; set; }

    [Required]
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Idle;

    [Required, MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(100)]
    public string PlantArea { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ProductionLine { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Manufacturer { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ModelNumber { get; set; } = string.Empty;

    public DateTime InstalledDate { get; set; }

    public DateTime? LastMaintenanceDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}

public enum EquipmentStatus
{
    Running,
    Idle,
    Down,
    Maintenance,
    Decommissioned
}

public enum EquipmentType
{
    ProductionMachine,
    ConveyorBelt,
    Sensor,
    Robot,
    QualityInspection,
    Packaging,
    HVAC,
    PowerSystem
}
