using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Identity.API.Dtos;
using Integration.Tests.Helpers;

namespace Integration.Tests;

/// <summary>
/// Each test creates its own factory (+ isolated InMemory DB) for full test isolation.
/// IdentityDbContext seeds 3 default users: admin, engineer, operator.
/// </summary>
public class IdentityApiTests : IDisposable
{
    private readonly IdentityApiFactory _factory;
    private readonly HttpClient _client;

    public IdentityApiTests()
    {
        _factory = new IdentityApiFactory();
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidAdminCredentials_Returns200WithToken()
    {
        var payload = new LoginDto("admin@smartfactory.com", "Admin123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.TokenType.Should().Be("Bearer");
        body.User.Email.Should().Be("admin@smartfactory.com");
        body.User.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Login_ValidEngineerCredentials_Returns200()
    {
        var payload = new LoginDto("engineer@smartfactory.com", "Engineer123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        body!.User.Role.Should().Be("Engineer");
    }

    [Fact]
    public async Task Login_ValidOperatorCredentials_Returns200()
    {
        var payload = new LoginDto("operator@smartfactory.com", "Operator123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        body!.User.Role.Should().Be("Operator");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var payload = new LoginDto("admin@smartfactory.com", "WrongPassword!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var payload = new LoginDto("nobody@example.com", "Password123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_EmptyCredentials_Returns400()
    {
        // Both email and password are required — empty object triggers model validation
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_TokenContainsBearerType()
    {
        var payload = new LoginDto("admin@smartfactory.com", "Admin123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);
        var body = await response.Content.ReadFromJsonAsync<TokenResponseDto>();

        body!.TokenType.Should().Be("Bearer");
        body.ExpiresIn.Should().BeGreaterThan(0);
    }

    // ── GET /health ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
