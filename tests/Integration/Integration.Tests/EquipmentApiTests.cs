using System.Net;
using System.Net.Http.Json;
using Equipment.API.DTOs;
using Equipment.API.Models;
using FluentAssertions;
using Integration.Tests.Helpers;

namespace Integration.Tests;

/// <summary>
/// Each test creates its own factory (+ isolated InMemory DB) for full test isolation.
/// Note: EquipmentDbContext is seeded with 5 default equipment items.
/// </summary>
public class EquipmentApiTests : IDisposable
{
    private readonly EquipmentApiFactory _factory;
    private readonly HttpClient _client;

    public EquipmentApiTests()
    {
        _factory = new EquipmentApiFactory();
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CreateEquipmentDto BuildCreateDto(string? code = null) => new()
    {
        EquipmentCode  = code ?? $"EQ-{Guid.NewGuid():N}"[..10],
        Name           = "Test CNC Machine",
        Description    = "Integration test equipment",
        Type           = EquipmentType.ProductionMachine,
        Location       = "Plant-A",
        PlantArea      = "Assembly",
        ProductionLine = "Line-1",
        InstalledDate  = DateTime.UtcNow.AddYears(-1)
    };

    private async Task<EquipmentDto> CreateEquipmentAsync(string? code = null)
    {
        var response = await _client.PostAsJsonAsync("/api/equipment", BuildCreateDto(code));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EquipmentDto>())!;
    }

    // ── GET /api/equipment ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Returns200WithSeededEquipment()
    {
        var response = await _client.GetAsync("/api/equipment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<EquipmentDto>>();
        // 5 items are seeded by EquipmentDbContext.SeedData
        body.Should().NotBeNull().And.HaveCount(5);
    }

    // ── POST /api/equipment ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateEquipment_ValidPayload_Returns201WithEquipment()
    {
        var dto = BuildCreateDto("EQ-INTTEST1");

        var response = await _client.PostAsJsonAsync("/api/equipment", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EquipmentDto>();
        body.Should().NotBeNull();
        body!.EquipmentCode.Should().Be("EQ-INTTEST1");
        body.Name.Should().Be("Test CNC Machine");
        body.Location.Should().Be("Plant-A");
    }

    [Fact]
    public async Task CreateEquipment_MissingRequiredFields_Returns400()
    {
        // Only sending EquipmentCode — Name, Type, Location are Required
        var payload = new { EquipmentCode = "EQ-MISSING" };

        var response = await _client.PostAsJsonAsync("/api/equipment", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/equipment/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingEquipment_Returns200()
    {
        var created = await CreateEquipmentAsync("EQ-GETBYID");

        var response = await _client.GetAsync($"/api/equipment/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EquipmentDto>();
        body!.Id.Should().Be(created.Id);
        body.EquipmentCode.Should().Be("EQ-GETBYID");
    }

    [Fact]
    public async Task GetById_NonExistentEquipment_Returns404()
    {
        var response = await _client.GetAsync($"/api/equipment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/equipment/code/{code} ────────────────────────────────────────

    [Fact]
    public async Task GetByCode_ExistingCode_Returns200()
    {
        await CreateEquipmentAsync("EQ-CODE-001");

        var response = await _client.GetAsync("/api/equipment/code/EQ-CODE-001");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EquipmentDto>();
        body!.EquipmentCode.Should().Be("EQ-CODE-001");
    }

    [Fact]
    public async Task GetByCode_SeededEquipment_Returns200()
    {
        // CNC-001 is seeded by EquipmentDbContext
        var response = await _client.GetAsync("/api/equipment/code/CNC-001");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── PUT /api/equipment/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateEquipment_ValidPayload_Returns200WithUpdatedData()
    {
        var created = await CreateEquipmentAsync();
        var updateDto = new UpdateEquipmentDto { Name = "Updated Machine Name", Location = "Plant-B" };

        var response = await _client.PutAsJsonAsync($"/api/equipment/{created.Id}", updateDto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EquipmentDto>();
        body!.Name.Should().Be("Updated Machine Name");
        body.Location.Should().Be("Plant-B");
    }

    [Fact]
    public async Task UpdateEquipment_NonExistent_Returns404()
    {
        var updateDto = new UpdateEquipmentDto { Name = "Ghost Machine" };

        var response = await _client.PutAsJsonAsync($"/api/equipment/{Guid.NewGuid()}", updateDto);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH /api/equipment/{id}/status ─────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_ValidStatus_Returns200WithNewStatus()
    {
        var created = await CreateEquipmentAsync();
        var statusDto = new UpdateStatusDto { Status = EquipmentStatus.Maintenance };

        var response = await _client.PatchAsJsonAsync($"/api/equipment/{created.Id}/status", statusDto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EquipmentDto>();
        body!.Status.Should().Be("Maintenance");
    }

    // ── DELETE /api/equipment/{id} ────────────────────────────────────────────

    [Fact]
    public async Task DeleteEquipment_ExistingEquipment_Returns204()
    {
        var created = await CreateEquipmentAsync();

        var response = await _client.DeleteAsync($"/api/equipment/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteEquipment_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/equipment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/equipment/summary/status ─────────────────────────────────────

    [Fact]
    public async Task GetStatusSummary_Returns200()
    {
        var response = await _client.GetAsync("/api/equipment/summary/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /api/equipment/health ─────────────────────────────────────────────

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/api/equipment/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
