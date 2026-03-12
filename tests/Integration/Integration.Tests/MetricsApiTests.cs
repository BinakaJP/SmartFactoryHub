using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Integration.Tests.Helpers;
using Metrics.API.DTOs;

namespace Integration.Tests;

/// <summary>
/// Each test creates its own factory (+ isolated InMemory DB) for full test isolation.
/// </summary>
public class MetricsApiTests : IDisposable
{
    private readonly MetricsApiFactory _factory;
    private readonly HttpClient _client;

    public MetricsApiTests()
    {
        _factory = new MetricsApiFactory();
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── POST /api/metrics/ingest ──────────────────────────────────────────────

    [Fact]
    public async Task IngestMetric_ValidPayload_Returns201WithResult()
    {
        var payload = new IngestMetricDto
        {
            EquipmentId = "int-test-equip-001",
            MetricType  = "Temperature",
            Value       = 42.5,
            Unit        = "Celsius",
            Timestamp   = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/metrics/ingest", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MetricQueryResult>();
        body.Should().NotBeNull();
        body!.EquipmentId.Should().Be("int-test-equip-001");
        body.MetricType.Should().Be("Temperature");
        body.Value.Should().Be(42.5);
    }

    [Fact]
    public async Task IngestMetric_MissingRequiredField_Returns400()
    {
        var payload = new { MetricType = "Temperature", Value = 42.5 };

        var response = await _client.PostAsJsonAsync("/api/metrics/ingest", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/metrics/ingest/batch ────────────────────────────────────────

    [Fact]
    public async Task IngestBatch_ValidPayload_Returns201WithAllResults()
    {
        var payload = new BatchIngestDto
        {
            Metrics = new List<IngestMetricDto>
            {
                new() { EquipmentId = "batch-equip-001", MetricType = "OEE",         Value = 85, Unit = "%" },
                new() { EquipmentId = "batch-equip-001", MetricType = "Temperature", Value = 75, Unit = "Celsius" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/metrics/ingest/batch", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<List<MetricQueryResult>>();
        body.Should().HaveCount(2);
    }

    // ── GET /api/metrics/equipment/{id}/latest ────────────────────────────────

    [Fact]
    public async Task GetLatest_AfterIngest_Returns200WithData()
    {
        await _client.PostAsJsonAsync("/api/metrics/ingest", new IngestMetricDto
        {
            EquipmentId = "latest-equip-001",
            MetricType  = "Vibration",
            Value       = 2.1,
            Unit        = "mm/s"
        });

        var response = await _client.GetAsync("/api/metrics/equipment/latest-equip-001/latest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<MetricQueryResult>>();
        body.Should().ContainSingle(r => r.EquipmentId == "latest-equip-001");
    }

    [Fact]
    public async Task GetLatest_UnknownEquipment_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/metrics/equipment/nonexistent-equip/latest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<MetricQueryResult>>();
        body.Should().BeEmpty();
    }

    // ── GET /api/metrics/equipment/{id}/{type}/aggregate ─────────────────────

    [Fact]
    public async Task GetAggregation_AfterTwoIngests_ReturnsCorrectMinMaxAvg()
    {
        const string eqId = "agg-equip-001";
        await _client.PostAsJsonAsync("/api/metrics/ingest", new IngestMetricDto
        {
            EquipmentId = eqId, MetricType = "PowerConsumption", Value = 100, Unit = "kWh"
        });
        await _client.PostAsJsonAsync("/api/metrics/ingest", new IngestMetricDto
        {
            EquipmentId = eqId, MetricType = "PowerConsumption", Value = 200, Unit = "kWh"
        });

        var from = Uri.EscapeDataString(DateTime.UtcNow.AddHours(-1).ToString("O"));
        var to   = Uri.EscapeDataString(DateTime.UtcNow.AddHours(1).ToString("O"));
        var url  = $"/api/metrics/equipment/{eqId}/PowerConsumption/aggregate?from={from}&to={to}";

        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MetricAggregation>();
        body.Should().NotBeNull();
        body!.Min.Should().Be(100);
        body.Max.Should().Be(200);
        body.Average.Should().Be(150);
        body.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetAggregation_NoDataInRange_Returns404()
    {
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-30).ToString("O"));
        var to   = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-29).ToString("O"));
        var url  = $"/api/metrics/equipment/ghost-equip/Temperature/aggregate?from={from}&to={to}";

        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/metrics/dashboard/summary ───────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_Returns200WithValidBody()
    {
        var response = await _client.GetAsync("/api/metrics/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DashboardSummary>();
        body.Should().NotBeNull();
        body!.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    // ── GET /api/metrics/health ───────────────────────────────────────────────

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/api/metrics/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
