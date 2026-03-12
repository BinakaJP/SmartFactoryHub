using FluentAssertions;
using Identity.API.Data;
using Identity.API.Dtos;
using Identity.API.Models;
using Identity.API.Security;
using Identity.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Identity.API.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    // Fixed test secret — must be ≥ 32 chars for HMAC-SHA256
    private const string TestJwtSecret = "test-jwt-secret-key-at-least-32-chars-long!!";

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"IdentityTest_{Guid.NewGuid()}")
            .Options;
        _db = new IdentityDbContext(options);
        _db.Database.EnsureCreated(); // applies HasData seed (3 default users)

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestJwtSecret,
                ["Jwt:Issuer"]    = "test-issuer",
                ["Jwt:Audience"]  = "test-audience",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        _sut = new AuthService(_db, _config, NullLogger<AuthService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── LoginAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidAdminCredentials_ReturnsTokenResponse()
    {
        var dto = new LoginDto("admin@smartfactory.com", "Admin123!");

        var result = await _sut.LoginAsync(dto);

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().BeGreaterThan(0);
        result.User.Email.Should().Be("admin@smartfactory.com");
        result.User.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var dto = new LoginDto("admin@smartfactory.com", "WrongPassword!");

        var result = await _sut.LoginAsync(dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        var dto = new LoginDto("nobody@nowhere.com", "Admin123!");

        var result = await _sut.LoginAsync(dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        // Arrange: insert an inactive user
        var user = new User
        {
            Email = "inactive@smartfactory.com",
            Name = "Inactive User",
            PasswordHash = PasswordHasher.Hash("Pass123!"),
            Role = UserRole.Viewer,
            IsActive = false
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var dto = new LoginDto("inactive@smartfactory.com", "Pass123!");

        var result = await _sut.LoginAsync(dto);

        result.Should().BeNull("inactive users must not be able to log in");
    }

    [Fact]
    public async Task LoginAsync_EmailIsCaseInsensitive()
    {
        // Admin is seeded with lowercase email; test with uppercase input
        var dto = new LoginDto("ADMIN@SMARTFACTORY.COM", "Admin123!");

        var result = await _sut.LoginAsync(dto);

        result.Should().NotBeNull("email matching must be case-insensitive");
    }

    [Fact]
    public async Task LoginAsync_UpdatesLastLoginAt()
    {
        var dto = new LoginDto("engineer@smartfactory.com", "Engineer123!");
        var before = DateTime.UtcNow;

        await _sut.LoginAsync(dto);

        var user = await _db.Users.FirstAsync(u => u.Email == "engineer@smartfactory.com");
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task LoginAsync_Token_ContainsExpectedClaims()
    {
        var dto = new LoginDto("operator@smartfactory.com", "Operator123!");

        var result = await _sut.LoginAsync(dto);

        result.Should().NotBeNull();
        // Decode the JWT and inspect claims (without needing a full validation stack)
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result!.AccessToken);

        token.Subject.Should().NotBeNullOrEmpty("sub claim must be the user ID");
        token.Claims.Should().Contain(c => c.Type == "email" && c.Value == "operator@smartfactory.com");
        token.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Bob Operator");
        // Role claim uses ClaimTypes.Role which is the long URI in JWT
        token.Claims.Should().Contain(c => c.Value == "Operator");
    }

    // ── GetAllUsersAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllUsersAsync_ReturnsSeededUsers()
    {
        var users = await _sut.GetAllUsersAsync();

        // 3 seed users
        users.Should().HaveCount(3);
        users.Select(u => u.Email).Should().Contain("admin@smartfactory.com");
    }

    // ── GetUserByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserByIdAsync_ExistingUser_ReturnsDto()
    {
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var result = await _sut.GetUserByIdAsync(adminId);

        result.Should().NotBeNull();
        result!.Email.Should().Be("admin@smartfactory.com");
        result.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task GetUserByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetUserByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── CreateUserAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_StoresHashedPassword_NotPlainText()
    {
        var dto = new CreateUserDto(
            Email: "newuser@smartfactory.com",
            Name: "New User",
            Password: "NewUser123!",
            Role: UserRole.Viewer
        );

        var result = await _sut.CreateUserAsync(dto);

        var stored = await _db.Users.FindAsync(result.Id);
        stored!.PasswordHash.Should().NotBe("NewUser123!", "passwords must never be stored in plain text");
        stored.PasswordHash.Should().Contain(":", "hashed format is salt:hash");
        stored.Email.Should().Be("newuser@smartfactory.com");
    }

    // ── ChangePasswordAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_CorrectCurrentPassword_ReturnsTrue()
    {
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var dto = new ChangePasswordDto("Admin123!", "NewAdmin456!");

        var result = await _sut.ChangePasswordAsync(adminId, dto);

        result.Should().BeTrue();
        // New password must now verify
        var user = await _db.Users.FindAsync(adminId);
        PasswordHasher.Verify("NewAdmin456!", user!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsFalse()
    {
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var dto = new ChangePasswordDto("WrongPassword!", "NewAdmin456!");

        var result = await _sut.ChangePasswordAsync(adminId, dto);

        result.Should().BeFalse();
    }
}
