using Equipment.API.DTOs;
using Equipment.API.Models;

namespace Equipment.API.Services;

/// <summary>
/// Service interface for equipment business logic.
/// Follows the Repository/Service pattern for clean separation of concerns.
/// </summary>
public interface IEquipmentService
{
    Task<IEnumerable<EquipmentDto>> GetAllAsync(string? status = null, string? location = null, string? line = null);
    Task<EquipmentDto?> GetByIdAsync(Guid id);
    Task<EquipmentDto?> GetByCodeAsync(string equipmentCode);
    Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto);
    Task<EquipmentDto?> UpdateAsync(Guid id, UpdateEquipmentDto dto);
    Task<EquipmentDto?> UpdateStatusAsync(Guid id, EquipmentStatus status);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<EquipmentDto>> GetByStatusAsync(EquipmentStatus status);
    Task<object> GetStatusSummaryAsync();
}
