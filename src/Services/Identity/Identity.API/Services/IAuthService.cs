using Identity.API.Dtos;

namespace Identity.API.Services;

public interface IAuthService
{
    Task<TokenResponseDto?> LoginAsync(LoginDto dto);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(Guid id);
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserDto dto);
    Task<bool> ChangePasswordAsync(Guid id, ChangePasswordDto dto);
    Task<bool> DeactivateUserAsync(Guid id);
}
