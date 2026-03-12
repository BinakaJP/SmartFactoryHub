using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using FluentAssertions;
using Metrics.API.Data;
using Metrics.API.DTOs;
using Metrics.API.Models;
using Metrics.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Metrics.API.Tests.Services;

public class MetricsServiceTests : IDisposable
{
    private readonly MetricsDbContext _db;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly MetricsService _sut;

    // A test equipment ID that is ≥ 8 chars (required for EquipmentId[..8] slice)
    private const string TestEquipmentId = "test-equipment-id-001";
    private const string TestMetricType  = "Temperature";

    public MetricsServiceTests()
    {
        var options = new DbContextOptionsBuilder<MetricsDbContext>()
            .UseInMemoryDatabase($"MetricsTest_{Guid.NewGuid()}")
            .Options;
        _db = new MetricsDbContext(options);
        _db.Database.EnsureCreated(); // applies HasData seed (thresholds for known equipment IDs)

        _eventBusMock = new Mock<IEventBus>();
        _eventBusMock
            .Setup(e => e.PublishAsync(It.IsAny<MetricRecordedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _eventBusMock
            .Setup(e => e.PublishAsync(It.IsAny<MetricThresholdBreachedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new MetricsService(_db, _eventBusMock.Object, NullLogger<MetricsService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IngestMetricDto BuildDto(
        string equipmentId = TestEquipmentId,
        string metricType  = TestMetricType,
        double value       = 25.0,
        string unit        = "Celsius",
        DateTime? timestamp = null) =>
        new()
        {
            EquipmentId = equipmentId,
            MetricType  = metricType,
            Value       = value,
            Unit        = unit,
            Timestamp   = timestamp
        };

    private async Task SeedThresholdAsync(
        string equipmentId,
        string metricType,
        double warningValue,
        double criticalValue,
        ThresholdDirection direction = ThresholdDirection.Above)
    {
        _db.AlertThresholds.Add(new AlertThreshold
        {
            Id = Guid.NewGuid(),
            EquipmentId    = equipmentId,
            MetricType     = metricType,
            WarningValue   = warningValue,
            CriticalValue  = criticalValue,
            Direction      = direction,
            IsActive       = true
        });
        await _db.SaveChangesAsync();
    }

    // ── IngestAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IngestAsync_SavesDataPointToDatabase()
    {
        var dto = BuildDto(value: 42.5);

        await _sut.IngestAsync(dto);

        var stored = await _db.MetricDataPoints.FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.EquipmentId.Should().Be(TestEquipmentId);
        stored.MetricType.Should().Be(TestMetricType);
        stored.Value.Should().Be(42.5);
        stored.Unit.Should().Be("Celsius");
    }

    [Fact]
    public async Task IngestAsync_ReturnsResultWithCorrectValues()
    {
        var dto = BuildDto(value: 100.0);

        var result = await _sut.IngestAsync(dto);

        result.EquipmentId.Should().Be(TestEquipmentId);
        result.MetricType.Should().Be(TestMetricType);
        result.Value.Should().Be(100.0);
        result.Unit.Should().Be("Celsius");
    }

    [Fact]
    public async Task IngestAsync_UsesProvidedTimestamp()
    {
        var specificTime = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var dto = BuildDto(timestamp: specificTime);

        var result = await _sut.IngestAsync(dto);

        result.Timestamp.Should().Be(specificTime);
    }

    [Fact]
    public async Task IngestAsync_UsesCurrentTime_WhenTimestampIsNull()
    {
        var before = DateTime.UtcNow;
        var dto = BuildDto(timestamp: null);

        var result = await _sut.IngestAsync(dto);

        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task IngestAsync_PublishesMetricRecordedEvent()
    {
        await _sut.IngestAsync(BuildDto());

        _eventBusMock.Verify(
            e => e.PublishAsync(It.IsAny<MetricRecordedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_AboveThreshold_Warning_PublishesBreachEvent()
    {
        await SeedThresholdAsync(TestEquipmentId, TestMetricType, warningValue: 300, criticalValue: 400, ThresholdDirection.Above);

        // Value ≥ warningValue but < criticalValue → Warning breach
        await _sut.IngestAsync(BuildDto(value: 350));

        _eventBusMock.Verify(
            e => e.PublishAsync(It.Is<MetricThresholdBreachedEvent>(ev => ev.Severity == "Warning"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_AboveThreshold_Critical_PublishesBreachEvent()
    {
        await SeedThresholdAsync(TestEquipmentId, TestMetricType, warningValue: 300, criticalValue: 400, ThresholdDirection.Above);

        // Value ≥ criticalValue → Critical breach
        await _sut.IngestAsync(BuildDto(value: 420));

        _eventBusMock.Verify(
            e => e.PublishAsync(It.Is<MetricThresholdBreachedEvent>(ev => ev.Severity == "Critical"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_BelowThreshold_Warning_PublishesBreachEvent()
    {
        await SeedThresholdAsync(TestEquipmentId, MetricTypes.OEE, warningValue: 70, criticalValue: 60, ThresholdDirection.Below);

        // Value ≤ warningValue but > criticalValue → Warning breach
        await _sut.IngestAsync(BuildDto(metricType: MetricTypes.OEE, value: 65));

        _eventBusMock.Verify(
            e => e.PublishAsync(It.Is<MetricThresholdBreachedEvent>(ev => ev.Severity == "Warning"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_BelowThreshold_NoBreach_DoesNotPublishBreachEvent()
    {
        await SeedThresholdAsync(TestEquipmentId, MetricTypes.OEE, warningValue: 70, criticalValue: 60, ThresholdDirection.Below);

        // Value > warningValue → no breach
        await _sut.IngestAsync(BuildDto(metricType: MetricTypes.OEE, value: 85));

        _eventBusMock.Verify(
            e => e.PublishAsync(It.IsAny<MetricThresholdBreachedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── GetLatestAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestAsync_ReturnsDataPointsForEquipment()
    {
        await _sut.IngestAsync(BuildDto(value: 10));
        await _sut.IngestAsync(BuildDto(value: 20));
        // Different equipment — should NOT appear
        await _sut.IngestAsync(BuildDto(equipmentId: "other-equipment-12345", value: 99));

        var results = await _sut.GetLatestAsync(TestEquipmentId);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.EquipmentId == TestEquipmentId);
    }

    [Fact]
    public async Task GetLatestAsync_FiltersByMetricType()
    {
        await _sut.IngestAsync(BuildDto(metricType: "Temperature", value: 100));
        await _sut.IngestAsync(BuildDto(metricType: "OEE", value: 80));

        var results = await _sut.GetLatestAsync(TestEquipmentId, metricType: "Temperature");

        results.Should().HaveCount(1);
        results.First().MetricType.Should().Be("Temperature");
    }

    [Fact]
    public async Task GetLatestAsync_RespectsCountLimit()
    {
        for (int i = 0; i < 10; i++)
            await _sut.IngestAsync(BuildDto(value: i));

        var results = await _sut.GetLatestAsync(TestEquipmentId, count: 3);

        results.Should().HaveCount(3);
    }

    // ── GetAggregationAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAggregationAsync_ReturnsCorrectMinMaxAverage()
    {
        var from = DateTime.UtcNow.AddHours(-1);
        var to   = DateTime.UtcNow.AddHours(1);
        await _sut.IngestAsync(BuildDto(value: 10));
        await _sut.IngestAsync(BuildDto(value: 20));
        await _sut.IngestAsync(BuildDto(value: 30));

        var result = await _sut.GetAggregationAsync(TestEquipmentId, TestMetricType, from, to);

        result.Should().NotBeNull();
        result!.Min.Should().Be(10);
        result.Max.Should().Be(30);
        result.Average.Should().Be(20);
        result.Count.Should().Be(3);
        result.EquipmentId.Should().Be(TestEquipmentId);
        result.MetricType.Should().Be(TestMetricType);
    }

    [Fact]
    public async Task GetAggregationAsync_EmptyRange_ReturnsNull()
    {
        var from = DateTime.UtcNow.AddDays(-10);
        var to   = DateTime.UtcNow.AddDays(-9);

        var result = await _sut.GetAggregationAsync(TestEquipmentId, TestMetricType, from, to);

        result.Should().BeNull("no data in the given time window");
    }

    // ── GetDashboardSummaryAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsCorrectAverageOEE()
    {
        await _sut.IngestAsync(BuildDto(metricType: MetricTypes.OEE, value: 80));
        await _sut.IngestAsync(BuildDto(metricType: MetricTypes.OEE, value: 90));

        var summary = await _sut.GetDashboardSummaryAsync();

        summary.AverageOEE.Should().Be(85.0);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ReportsTotalDataPoints()
    {
        await _sut.IngestAsync(BuildDto(metricType: MetricTypes.Temperature, value: 100));
        await _sut.IngestAsync(BuildDto(metricType: MetricTypes.OEE, value: 75));

        var summary = await _sut.GetDashboardSummaryAsync();

        summary.TotalDataPoints.Should().Be(2);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_NoDataInLastHour_ReturnsZeroOEE()
    {
        // Ingest a very old data point (outside the 1-hour window)
        _db.MetricDataPoints.Add(new MetricDataPoint
        {
            EquipmentId = TestEquipmentId,
            MetricType  = MetricTypes.OEE,
            Value       = 95,
            Timestamp   = DateTime.UtcNow.AddHours(-2) // outside window
        });
        await _db.SaveChangesAsync();

        var summary = await _sut.GetDashboardSummaryAsync();

        summary.AverageOEE.Should().Be(0, "data outside 1-hour window is excluded");
    }
}
