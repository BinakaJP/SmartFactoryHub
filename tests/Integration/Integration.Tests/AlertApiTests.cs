using System.Net;
using System.Net.Http.Json;
using Alert.API.Data;
using Alert.API.Dtos;
using Alert.API.Models;
using FluentAssertions;
using Integration.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// Each test creates its own factory (+ isolated InMemory DB) for full test isolation.
/// </summary>
public class AlertApiTests : IDisposable
{
    private readonly AlertApiFactory _factory;
    private readonly HttpClient _client;

    public AlertApiTests()
    {
        _factory = new AlertApiFactory();
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<Guid> CreateAlertInDb(string severity = "Warning")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        var alert = new Alert.API.Models.Alert
        {
            EquipmentId    = "eq-int-001",
            EquipmentName  = "Test CNC",
            MetricType     = "Temperature",
            TriggerValue   = 380,
            ThresholdValue = 350,
            Severity       = severity == "Critical" ? AlertSeverity.Critical : AlertSeverity.Warning,
            Status         = AlertStatus.Open
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }

    // ── GET /api/alerts ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAlerts_EmptyDatabase_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<AlertDto>>();
        body.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetAlerts_WithTwoAlerts_ReturnsBothAlerts()
    {
        await CreateAlertInDb("Warning");
        await CreateAlertInDb("Critical");

        var response = await _client.GetAsync("/api/alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<AlertDto>>();
        body.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAlerts_FilterByStatus_ReturnsFilteredResults()
    {
        var created = await CreateAlertInDb("Warning");
        await _client.PatchAsJsonAsync($"/api/alerts/{created}/acknowledge", new AcknowledgeAlertDto("tester"));
        await CreateAlertInDb("Critical"); // remains Open

        var openAlerts = await (await _client.GetAsync("/api/alerts?status=Open"))
            .Content.ReadFromJsonAsync<List<AlertDto>>();
        var ackAlerts = await (await _client.GetAsync("/api/alerts?status=Acknowledged"))
            .Content.ReadFromJsonAsync<List<AlertDto>>();

        openAlerts.Should().HaveCount(1).And.OnlyContain(a => a.Status == "Open");
        ackAlerts.Should().HaveCount(1).And.OnlyContain(a => a.Status == "Acknowledged");
    }

    // ── GET /api/alerts/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAlertById_ExistingAlert_Returns200()
    {
        var alertId = await CreateAlertInDb();

        var response = await _client.GetAsync($"/api/alerts/{alertId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AlertDto>();
        body!.Id.Should().Be(alertId);
    }

    [Fact]
    public async Task GetAlertById_NonExistentAlert_Returns404()
    {
        var response = await _client.GetAsync($"/api/alerts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/alerts/summary ───────────────────────────────────────────────

    [Fact]
    public async Task GetSummary_EmptyDatabase_ReturnsAllZeroCounts()
    {
        var response = await _client.GetAsync("/api/alerts/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AlertSummaryDto>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetSummary_WithAlerts_ReturnsCorrectCounts()
    {
        var a1 = await CreateAlertInDb("Warning");   // Open/Warning
        var a2 = await CreateAlertInDb("Critical");  // will be Acknowledged
        await _client.PatchAsJsonAsync($"/api/alerts/{a2}/acknowledge", new AcknowledgeAlertDto("eng"));

        var response = await _client.GetAsync("/api/alerts/summary");
        var body = await response.Content.ReadFromJsonAsync<AlertSummaryDto>();

        body!.Total.Should().Be(2);
        body.Open.Should().Be(1);
        body.Acknowledged.Should().Be(1);
        body.Warning.Should().Be(1);
        body.Critical.Should().Be(1);
    }

    // ── PATCH /api/alerts/{id}/acknowledge ────────────────────────────────────

    [Fact]
    public async Task AcknowledgeAlert_OpenAlert_Returns200WithAcknowledgedStatus()
    {
        var alertId = await CreateAlertInDb("Warning");
        var dto = new AcknowledgeAlertDto("engineer-test");

        var response = await _client.PatchAsJsonAsync($"/api/alerts/{alertId}/acknowledge", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AlertDto>();
        body!.Status.Should().Be("Acknowledged");
        body.AcknowledgedBy.Should().Be("engineer-test");
    }

    [Fact]
    public async Task AcknowledgeAlert_NonExistent_Returns409()
    {
        var dto = new AcknowledgeAlertDto("engineer");

        var response = await _client.PatchAsJsonAsync($"/api/alerts/{Guid.NewGuid()}/acknowledge", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AcknowledgeAlert_EmptyAcknowledgedBy_Returns400()
    {
        var alertId = await CreateAlertInDb();
        var dto = new AcknowledgeAlertDto(string.Empty);

        var response = await _client.PatchAsJsonAsync($"/api/alerts/{alertId}/acknowledge", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PATCH /api/alerts/{id}/resolve ────────────────────────────────────────

    [Fact]
    public async Task ResolveAlert_OpenAlert_Returns200WithResolvedStatus()
    {
        var alertId = await CreateAlertInDb();
        var dto = new ResolveAlertDto("Cooling system repaired");

        var response = await _client.PatchAsJsonAsync($"/api/alerts/{alertId}/resolve", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AlertDto>();
        body!.Status.Should().Be("Resolved");
        body.ResolutionNote.Should().Be("Cooling system repaired");
    }

    [Fact]
    public async Task ResolveAlert_AlreadyResolved_Returns409()
    {
        var alertId = await CreateAlertInDb();
        await _client.PatchAsJsonAsync($"/api/alerts/{alertId}/resolve", new ResolveAlertDto(null));

        var response = await _client.PatchAsJsonAsync($"/api/alerts/{alertId}/resolve", new ResolveAlertDto("again"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
