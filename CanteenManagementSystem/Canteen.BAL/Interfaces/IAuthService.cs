using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterOwnerDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto dto);
    Task<AuthResponseDto?> RefreshTokenAsync(int ownerId);
    Task<string?> GenerateTokenForOwnerAsync(int ownerId);
    Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto dto, bool isDevelopment);
    Task<bool> ResetPasswordAsync(ResetPasswordRequestDto dto);
    Task<AuthResponseDto?> SwitchCanteenAsync(int ownerId, int canteenId);
}
