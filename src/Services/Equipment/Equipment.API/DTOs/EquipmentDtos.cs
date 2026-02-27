using System.ComponentModel.DataAnnotations;
using Equipment.API.Models;

namespace Equipment.API.DTOs;

/// <summary>
/// DTO returned when querying equipment.
/// </summary>
public record EquipmentDto
{
    public Guid Id { get; init; }
    public string EquipmentCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string PlantArea { get; init; } = string.Empty;
    public string ProductionLine { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string ModelNumber { get; init; } = string.Empty;
    public DateTime InstalledDate { get; init; }
    public DateTime? LastMaintenanceDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO for creating new equipment.
/// </summary>
public record CreateEquipmentDto
{
    [Required, MaxLength(50)]
    public string EquipmentCode { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    [Required]
    public EquipmentType Type { get; init; }

    [Required, MaxLength(100)]
    public string Location { get; init; } = string.Empty;

    [MaxLength(100)]
    public string PlantArea { get; init; } = string.Empty;

    [MaxLength(100)]
    public string ProductionLine { get; init; } = string.Empty;

    [MaxLength(100)]
    public string Manufacturer { get; init; } = string.Empty;

    [MaxLength(100)]
    public string ModelNumber { get; init; } = string.Empty;

    public DateTime InstalledDate { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for updating existing equipment.
/// </summary>
public record UpdateEquipmentDto
{
    [MaxLength(200)]
    public string? Name { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }

    public EquipmentStatus? Status { get; init; }

    [MaxLength(100)]
    public string? Location { get; init; }

    [MaxLength(100)]
    public string? PlantArea { get; init; }

    [MaxLength(100)]
    public string? ProductionLine { get; init; }

    public bool? IsActive { get; init; }
}

/// <summary>
/// DTO for updating equipment status specifically (used by Simulator).
/// </summary>
public record UpdateStatusDto
{
    [Required]
    public EquipmentStatus Status { get; init; }
}
