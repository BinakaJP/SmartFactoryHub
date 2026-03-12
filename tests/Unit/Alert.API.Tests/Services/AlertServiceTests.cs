using Alert.API.Data;
using Alert.API.Models;
using Alert.API.Services;
using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Alert.API.Tests.Services;

public class AlertServiceTests : IDisposable
{
    private readonly AlertDbContext _db;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly AlertService _sut;

    public AlertServiceTests()
    {
        var options = new DbContextOptionsBuilder<AlertDbContext>()
            .UseInMemoryDatabase($"AlertTest_{Guid.NewGuid()}")
            .Options;
        _db = new AlertDbContext(options);
        _db.Database.EnsureCreated();

        _eventBusMock = new Mock<IEventBus>();
        _eventBusMock
            .Setup(e => e.PublishAsync(It.IsAny<AlertTriggeredEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new AlertService(_db, _eventBusMock.Object, NullLogger<AlertService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MetricThresholdBreachedEvent BuildEvent(string severity = "Warning") =>
        new()
        {
            EquipmentId   = "eq-001",
            EquipmentName = "CNC Machine",
            MetricType    = "Temperature",
            Value         = 380,
            ThresholdValue = 350,
            Severity      = severity
        };

    // ── CreateFromEventAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateFromEventAsync_WarningEvent_CreatesAlertWithWarningSeverity()
    {
        var evt = BuildEvent("Warning");

        var dto = await _sut.CreateFromEventAsync(evt);

        dto.Severity.Should().Be("Warning");
        dto.Status.Should().Be("Open");
        dto.EquipmentId.Should().Be("eq-001");
        dto.MetricType.Should().Be("Temperature");
        dto.TriggerValue.Should().Be(380);
        dto.ThresholdValue.Should().Be(350);
    }

    [Fact]
    public async Task CreateFromEventAsync_CriticalEvent_CreatesAlertWithCriticalSeverity()
    {
        var evt = BuildEvent("Critical");

        var dto = await _sut.CreateFromEventAsync(evt);

        dto.Severity.Should().Be("Critical");
        dto.Status.Should().Be("Open");
    }

    [Fact]
    public async Task CreateFromEventAsync_PersistsAlertToDatabase()
    {
        var evt = BuildEvent("Warning");

        var dto = await _sut.CreateFromEventAsync(evt);

        var stored = await _db.Alerts.FindAsync(dto.Id);
        stored.Should().NotBeNull();
        stored!.EquipmentId.Should().Be("eq-001");
    }

    [Fact]
    public async Task CreateFromEventAsync_PublishesAlertTriggeredEvent()
    {
        var evt = BuildEvent("Warning");

        await _sut.CreateFromEventAsync(evt);

        _eventBusMock.Verify(
            e => e.PublishAsync(It.IsAny<AlertTriggeredEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateFromEventAsync_UnknownSeverityString_MapsToWarning()
    {
        // Any severity value other than "Critical" falls back to Warning
        var evt = BuildEvent("Info");

        var dto = await _sut.CreateFromEventAsync(evt);

        dto.Severity.Should().Be("Warning");
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_NoFilters_ReturnsAllAlerts()
    {
        await _sut.CreateFromEventAsync(BuildEvent("Warning"));
        await _sut.CreateFromEventAsync(BuildEvent("Critical"));

        var results = await _sut.GetAllAsync();

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_FilterByStatus_ReturnsOnlyMatchingAlerts()
    {
        var created = await _sut.CreateFromEventAsync(BuildEvent("Warning"));
        await _sut.AcknowledgeAsync(created.Id, "tester");
        await _sut.CreateFromEventAsync(BuildEvent("Critical")); // remains Open

        var openAlerts = await _sut.GetAllAsync(status: "Open");
        var acknowledgedAlerts = await _sut.GetAllAsync(status: "Acknowledged");

        openAlerts.Should().HaveCount(1).And.OnlyContain(a => a.Status == "Open");
        acknowledgedAlerts.Should().HaveCount(1).And.OnlyContain(a => a.Status == "Acknowledged");
    }

    [Fact]
    public async Task GetAllAsync_FilterBySeverity_ReturnsOnlyMatchingAlerts()
    {
        await _sut.CreateFromEventAsync(BuildEvent("Warning"));
        await _sut.CreateFromEventAsync(BuildEvent("Critical"));

        var warnings = await _sut.GetAllAsync(severity: "Warning");
        var criticals = await _sut.GetAllAsync(severity: "Critical");

        warnings.Should().HaveCount(1).And.OnlyContain(a => a.Severity == "Warning");
        criticals.Should().HaveCount(1).And.OnlyContain(a => a.Severity == "Critical");
    }

    [Fact]
    public async Task GetAllAsync_FilterByEquipmentId_ReturnsOnlyMatchingAlerts()
    {
        await _sut.CreateFromEventAsync(BuildEvent("Warning")); // eq-001

        var otherEvt = new MetricThresholdBreachedEvent
        {
            EquipmentId   = "eq-999",
            EquipmentName = "Other Machine",
            MetricType    = "Temperature",
            Value         = 380,
            ThresholdValue = 350,
            Severity      = "Warning"
        };
        await _sut.CreateFromEventAsync(otherEvt);

        var results = await _sut.GetAllAsync(equipmentId: "eq-001");

        results.Should().HaveCount(1).And.OnlyContain(a => a.EquipmentId == "eq-001");
    }

    // ── AcknowledgeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task AcknowledgeAsync_OpenAlert_ChangesStatusToAcknowledged()
    {
        var created = await _sut.CreateFromEventAsync(BuildEvent());

        var result = await _sut.AcknowledgeAsync(created.Id, "engineer-jane");

        result.Should().NotBeNull();
        result!.Status.Should().Be("Acknowledged");
        result.AcknowledgedBy.Should().Be("engineer-jane");
        result.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AcknowledgeAsync_AlreadyAcknowledgedAlert_ReturnsNull()
    {
        var created = await _sut.CreateFromEventAsync(BuildEvent());
        await _sut.AcknowledgeAsync(created.Id, "first-user");

        // Try to acknowledge again
        var result = await _sut.AcknowledgeAsync(created.Id, "second-user");

        result.Should().BeNull("only Open alerts can be acknowledged");
    }

    [Fact]
    public async Task AcknowledgeAsync_NonExistentAlert_ReturnsNull()
    {
        var result = await _sut.AcknowledgeAsync(Guid.NewGuid(), "engineer");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AcknowledgeAsync_ResolvedAlert_ReturnsNull()
    {
        var created = await _sut.CreateFromEventAsync(BuildEvent());
        await _sut.ResolveAsync(created.Id, "resolved");

        var result = await _sut.AcknowledgeAsync(created.Id, "late-ack");

        result.Should().BeNull("resolved alerts cannot be acknowledged");
    }

    // ── ResolveAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_OpenAlert_ChangesStatusToResolved()
    {
        var created = await _sut.CreateFromEventAsync(BuildEvent());

        var result = await _sut.ResolveAsync(created.Id, "Coolant refilled");

        result.Should().NotBeNull();
        result!.Status.Should().Be("Resolved");
        result.ResolutionNote.Should().Be("Coolant refilled");
        result.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_AcknowledgedAlert_ChangesStatusToResolved()
    {
        var created = await _sut.CreateFromEventAsync(BuildEvent());
        await _sut.AcknowledgeAsync(created.Id, "engineer");

        var result = await _sut.ResolveAsync(created.Id, "Fixed");

        result.Should().NotBeNull();
        result!.Status.Should().Be("Resolved");
    }

    [Fact]
    public async Task ResolveAsync_AlreadyResolvedAlert_ReturnsNull()
    {
        var created = await _sut.CreateFromEventAsync(BuildEvent());
        await _sut.ResolveAsync(created.Id, "first resolve");

        var result = await _sut.ResolveAsync(created.Id, "second resolve");

        result.Should().BeNull("already-resolved alerts cannot be resolved again");
    }

    [Fact]
    public async Task ResolveAsync_NonExistentAlert_ReturnsNull()
    {
        var result = await _sut.ResolveAsync(Guid.NewGuid(), "note");

        result.Should().BeNull();
    }

    // ── GetSummaryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_EmptyDatabase_ReturnsAllZeros()
    {
        var summary = await _sut.GetSummaryAsync();

        summary.Total.Should().Be(0);
        summary.Open.Should().Be(0);
        summary.Acknowledged.Should().Be(0);
        summary.Resolved.Should().Be(0);
        summary.Warning.Should().Be(0);
        summary.Critical.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        // Create 3 alerts, acknowledge 1, resolve 1
        var a1 = await _sut.CreateFromEventAsync(BuildEvent("Warning"));     // Open/Warning
        var a2 = await _sut.CreateFromEventAsync(BuildEvent("Critical"));    // Open/Critical
        var a3 = await _sut.CreateFromEventAsync(BuildEvent("Warning"));     // Open/Warning
        await _sut.AcknowledgeAsync(a2.Id, "engineer");                      // Acknowledged
        await _sut.ResolveAsync(a3.Id, null);                                // Resolved

        var summary = await _sut.GetSummaryAsync();

        summary.Total.Should().Be(3);
        summary.Open.Should().Be(1);
        summary.Acknowledged.Should().Be(1);
        summary.Resolved.Should().Be(1);
        summary.Warning.Should().Be(2);
        summary.Critical.Should().Be(1);
    }
}
