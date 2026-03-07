using Alert.API.Dtos;
using BuildingBlocks.Common.Events;

namespace Alert.API.Services;

public interface IAlertService
{
    Task<AlertDto> CreateFromEventAsync(MetricThresholdBreachedEvent evt);
    Task<IEnumerable<AlertDto>> GetAllAsync(string? status = null, string? severity = null, string? equipmentId = null);
    Task<AlertDto?> GetByIdAsync(Guid id);
    Task<AlertDto?> AcknowledgeAsync(Guid id, string acknowledgedBy);
    Task<AlertDto?> ResolveAsync(Guid id, string? resolutionNote);
    Task<AlertSummaryDto> GetSummaryAsync();
}
