using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface ICanteenService
{
    Task<IEnumerable<CanteenDto>> GetAllAsync();
    Task<CanteenDto?> GetByIdAsync(int id);
    Task<CanteenDto> CreateAsync(CreateCanteenDto dto, int? ownerId = null);
    Task<CanteenDto?> UpdateAsync(int id, UpdateCanteenDto dto);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<CanteenSummaryDto>> GetOwnerCanteensAsync(int ownerId);
    Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId);
}
