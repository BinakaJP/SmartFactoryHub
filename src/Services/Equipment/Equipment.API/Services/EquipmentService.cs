using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Equipment.API.Data;
using Equipment.API.DTOs;
using Equipment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Equipment.API.Services;

/// <summary>
/// Implementation of IEquipmentService.
/// Handles CRUD operations and publishes integration events via RabbitMQ
/// when equipment status changes (demonstrating inter-service communication).
/// </summary>
public class EquipmentService : IEquipmentService
{
    private readonly EquipmentDbContext _context;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EquipmentService> _logger;

    public EquipmentService(EquipmentDbContext context, IEventBus eventBus, ILogger<EquipmentService> logger)
    {
        _context = context;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<IEnumerable<EquipmentDto>> GetAllAsync(string? status = null, string? location = null, string? line = null)
    {
        var query = _context.Equipment.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<EquipmentStatus>(status, true, out var statusEnum))
            query = query.Where(e => e.Status == statusEnum);

        if (!string.IsNullOrEmpty(location))
            query = query.Where(e => e.Location.Contains(location));

        if (!string.IsNullOrEmpty(line))
            query = query.Where(e => e.ProductionLine == line);

        var equipment = await query.OrderBy(e => e.EquipmentCode).ToListAsync();
        return equipment.Select(MapToDto);
    }

    public async Task<EquipmentDto?> GetByIdAsync(Guid id)
    {
        var equipment = await _context.Equipment.FindAsync(id);
        return equipment is null ? null : MapToDto(equipment);
    }

    public async Task<EquipmentDto?> GetByCodeAsync(string equipmentCode)
    {
        var equipment = await _context.Equipment.FirstOrDefaultAsync(e => e.EquipmentCode == equipmentCode);
        return equipment is null ? null : MapToDto(equipment);
    }

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto)
    {
        var entity = new EquipmentEntity
        {
            EquipmentCode = dto.EquipmentCode,
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            Status = EquipmentStatus.Idle,
            Location = dto.Location,
            PlantArea = dto.PlantArea,
            ProductionLine = dto.ProductionLine,
            Manufacturer = dto.Manufacturer,
            ModelNumber = dto.ModelNumber,
            InstalledDate = dto.InstalledDate
        };

        _context.Equipment.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created equipment {Code} with ID {Id}", entity.EquipmentCode, entity.Id);
        return MapToDto(entity);
    }

    public async Task<EquipmentDto?> UpdateAsync(Guid id, UpdateEquipmentDto dto)
    {
        var entity = await _context.Equipment.FindAsync(id);
        if (entity is null) return null;

        var previousStatus = entity.Status;

        if (dto.Name is not null) entity.Name = dto.Name;
        if (dto.Description is not null) entity.Description = dto.Description;
        if (dto.Status.HasValue) entity.Status = dto.Status.Value;
        if (dto.Location is not null) entity.Location = dto.Location;
        if (dto.PlantArea is not null) entity.PlantArea = dto.PlantArea;
        if (dto.ProductionLine is not null) entity.ProductionLine = dto.ProductionLine;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Publish event if status changed (inter-service communication via RabbitMQ)
        if (dto.Status.HasValue && dto.Status.Value != previousStatus)
        {
            await PublishStatusChangeEvent(entity, previousStatus);
        }

        return MapToDto(entity);
    }

    public async Task<EquipmentDto?> UpdateStatusAsync(Guid id, EquipmentStatus status)
    {
        var entity = await _context.Equipment.FindAsync(id);
        if (entity is null) return null;

        var previousStatus = entity.Status;
        entity.Status = status;
        entity.UpdatedAt = DateTime.UtcNow;

        if (status == EquipmentStatus.Maintenance)
            entity.LastMaintenanceDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (status != previousStatus)
        {
            await PublishStatusChangeEvent(entity, previousStatus);
        }

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.Equipment.FindAsync(id);
        if (entity is null) return false;

        // Soft delete
        entity.IsActive = false;
        entity.Status = EquipmentStatus.Decommissioned;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<EquipmentDto>> GetByStatusAsync(EquipmentStatus status)
    {
        var equipment = await _context.Equipment
            .Where(e => e.Status == status && e.IsActive)
            .OrderBy(e => e.EquipmentCode)
            .ToListAsync();

        return equipment.Select(MapToDto);
    }

    public async Task<object> GetStatusSummaryAsync()
    {
        var summary = await _context.Equipment
            .Where(e => e.IsActive)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        var total = summary.Sum(s => s.Count);

        return new
        {
            Total = total,
            ByStatus = summary,
            RunningPercentage = total > 0
                ? Math.Round(summary.FirstOrDefault(s => s.Status == "Running")?.Count * 100.0 / total ?? 0, 1)
                : 0
        };
    }

    private async Task PublishStatusChangeEvent(EquipmentEntity entity, EquipmentStatus previousStatus)
    {
        try
        {
            var @event = new EquipmentStatusChangedEvent
            {
                EquipmentId = entity.Id.ToString(),
                EquipmentName = entity.Name,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = entity.Status.ToString()
            };

            await _eventBus.PublishAsync(@event);
            _logger.LogInformation("Published status change for {Code}: {From} → {To}",
                entity.EquipmentCode, previousStatus, entity.Status);
        }
        catch (Exception ex)
        {
            // Don't fail the API call if event publishing fails
            _logger.LogWarning(ex, "Failed to publish status change event for {Code}", entity.EquipmentCode);
        }
    }

    private static EquipmentDto MapToDto(EquipmentEntity entity) => new()
    {
        Id = entity.Id,
        EquipmentCode = entity.EquipmentCode,
        Name = entity.Name,
        Description = entity.Description,
        Type = entity.Type.ToString(),
        Status = entity.Status.ToString(),
        Location = entity.Location,
        PlantArea = entity.PlantArea,
        ProductionLine = entity.ProductionLine,
        Manufacturer = entity.Manufacturer,
        ModelNumber = entity.ModelNumber,
        InstalledDate = entity.InstalledDate,
        LastMaintenanceDate = entity.LastMaintenanceDate,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        IsActive = entity.IsActive
    };
}
