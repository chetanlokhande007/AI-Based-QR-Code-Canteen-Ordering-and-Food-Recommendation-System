using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterOwnerDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto dto);
    string GenerateJwtToken(Owner owner);
}
