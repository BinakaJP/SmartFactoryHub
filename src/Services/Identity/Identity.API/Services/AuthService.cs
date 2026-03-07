using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.API.Data;
using Identity.API.Dtos;
using Identity.API.Models;
using Identity.API.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Identity.API.Services;

public class AuthService : IAuthService
{
    private readonly IdentityDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IdentityDbContext db, IConfiguration config, ILogger<AuthService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email.ToLowerInvariant() && u.IsActive);

        if (user is null || !PasswordHasher.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}", dto.Email);
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        var expiryMinutes = _config.GetValue<int>("Jwt:ExpiryMinutes", 480);

        _logger.LogInformation("User {Email} ({Role}) logged in successfully", user.Email, user.Role);

        return new TokenResponseDto(
            AccessToken: token,
            TokenType: "Bearer",
            ExpiresIn: expiryMinutes * 60,
            User: ToDto(user)
        );
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _db.Users.OrderBy(u => u.Name).ToListAsync();
        return users.Select(ToDto);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        var user = new User
        {
            Email = dto.Email.ToLowerInvariant(),
            Name = dto.Name,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Role = dto.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {Email} created with role {Role}", user.Email, user.Role);
        return ToDto(user);
    }

    public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return null;

        if (dto.Name is not null) user.Name = dto.Name;
        if (dto.Role is not null) user.Role = dto.Role.Value;
        if (dto.IsActive is not null) user.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<bool> ChangePasswordAsync(Guid id, ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;
        if (!PasswordHasher.Verify(dto.CurrentPassword, user.PasswordHash)) return false;

        user.PasswordHash = PasswordHasher.Hash(dto.NewPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateUserAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;

        user.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    private string GenerateJwtToken(User user)
    {
        var secretKey = _config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = _config.GetValue<int>("Jwt:ExpiryMinutes", 480);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserDto ToDto(User u) => new(
        u.Id, u.Email, u.Name, u.Role.ToString(), u.IsActive, u.CreatedAt, u.LastLoginAt
    );
}
