using System.ComponentModel.DataAnnotations;
using Identity.API.Models;

namespace Identity.API.Dtos;

public record LoginDto(
    [Required] string Email,
    [Required] string Password
);

public record TokenResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record CreateUserDto(
    [Required][EmailAddress] string Email,
    [Required][MinLength(2)] string Name,
    [Required][MinLength(8)] string Password,
    [Required] UserRole Role
);

public record UpdateUserDto(
    string? Name,
    UserRole? Role,
    bool? IsActive
);

public record ChangePasswordDto(
    [Required] string CurrentPassword,
    [Required][MinLength(8)] string NewPassword
);
