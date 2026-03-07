using Alert.API.Data;
using Alert.API.Dtos;
using Alert.API.Models;
using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Alert.API.Services;

public class AlertService : IAlertService
{
    private readonly AlertDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AlertService> _logger;

    public AlertService(AlertDbContext db, IEventBus eventBus, ILogger<AlertService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<AlertDto> CreateFromEventAsync(MetricThresholdBreachedEvent evt)
    {
        var severity = evt.Severity == "Critical" ? AlertSeverity.Critical : AlertSeverity.Warning;

        var alert = new Alert.API.Models.Alert
        {
            EquipmentId = evt.EquipmentId,
            EquipmentName = evt.EquipmentName,
            MetricType = evt.MetricType,
            TriggerValue = evt.Value,
            ThresholdValue = evt.ThresholdValue,
            Severity = severity,
            Status = AlertStatus.Open
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Alert {AlertId} created — Equipment: {EquipmentId}, Metric: {MetricType}, Value: {Value}, Severity: {Severity}",
            alert.Id, alert.EquipmentId, alert.MetricType, alert.TriggerValue, alert.Severity);

        try
        {
            await _eventBus.PublishAsync(new AlertTriggeredEvent
            {
                AlertId = alert.Id.ToString(),
                EquipmentId = alert.EquipmentId,
                EquipmentName = alert.EquipmentName,
                Message = $"{alert.Severity} alert on {alert.EquipmentName}: {alert.MetricType} = {alert.TriggerValue:F2} (threshold: {alert.ThresholdValue:F2})",
                Severity = alert.Severity.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AlertTriggeredEvent for alert {AlertId}", alert.Id);
        }

        return ToDto(alert);
    }

    public async Task<IEnumerable<AlertDto>> GetAllAsync(string? status = null, string? severity = null, string? equipmentId = null)
    {
        var query = _db.Alerts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AlertStatus>(status, true, out var statusEnum))
            query = query.Where(a => a.Status == statusEnum);

        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<AlertSeverity>(severity, true, out var severityEnum))
            query = query.Where(a => a.Severity == severityEnum);

        if (!string.IsNullOrWhiteSpace(equipmentId))
            query = query.Where(a => a.EquipmentId == equipmentId);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync();

        return alerts.Select(ToDto);
    }

    public async Task<AlertDto?> GetByIdAsync(Guid id)
    {
        var alert = await _db.Alerts.FindAsync(id);
        return alert is null ? null : ToDto(alert);
    }

    public async Task<AlertDto?> AcknowledgeAsync(Guid id, string acknowledgedBy)
    {
        var alert = await _db.Alerts.FindAsync(id);
        if (alert is null || alert.Status != AlertStatus.Open)
            return null;

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedBy = acknowledgedBy;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alert.Id, acknowledgedBy);
        return ToDto(alert);
    }

    public async Task<AlertDto?> ResolveAsync(Guid id, string? resolutionNote)
    {
        var alert = await _db.Alerts.FindAsync(id);
        if (alert is null || alert.Status == AlertStatus.Resolved)
            return null;

        alert.Status = AlertStatus.Resolved;
        alert.ResolutionNote = resolutionNote;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Alert {AlertId} resolved", alert.Id);
        return ToDto(alert);
    }

    public async Task<AlertSummaryDto> GetSummaryAsync()
    {
        var alerts = await _db.Alerts.ToListAsync();
        return new AlertSummaryDto(
            Total: alerts.Count,
            Open: alerts.Count(a => a.Status == AlertStatus.Open),
            Acknowledged: alerts.Count(a => a.Status == AlertStatus.Acknowledged),
            Resolved: alerts.Count(a => a.Status == AlertStatus.Resolved),
            Warning: alerts.Count(a => a.Severity == AlertSeverity.Warning),
            Critical: alerts.Count(a => a.Severity == AlertSeverity.Critical)
        );
    }

    private static AlertDto ToDto(Alert.API.Models.Alert a) => new(
        a.Id, a.EquipmentId, a.EquipmentName, a.MetricType,
        a.TriggerValue, a.ThresholdValue,
        a.Severity.ToString(), a.Status.ToString(),
        a.AcknowledgedBy, a.AcknowledgedAt,
        a.ResolutionNote, a.ResolvedAt,
        a.CreatedAt, a.UpdatedAt
    );
}
