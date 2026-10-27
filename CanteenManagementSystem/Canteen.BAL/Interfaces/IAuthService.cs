using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterOwnerDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto dto);
    Task<AuthResponseDto?> RefreshTokenAsync(int ownerId);
    Task<string?> GenerateTokenForOwnerAsync(int ownerId);
}
