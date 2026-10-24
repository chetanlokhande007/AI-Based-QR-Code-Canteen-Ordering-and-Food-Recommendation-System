using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface ICanteenTableService
{
    Task<IEnumerable<CanteenTableDto>> GetAllByCanteenAsync(int canteenId);
    Task<CanteenTableDto?> GetByIdAsync(int tableId);
    Task<CanteenTableDto?> GetByIdAsync(int canteenId, int tableId);
    Task<CanteenTableDto?> GetByQRTokenAsync(string qrToken);
    Task<CanteenTableDto> CreateAsync(CreateCanteenTableDto dto);
    Task<CanteenTableDto?> UpdateAsync(int tableId, UpdateCanteenTableDto dto);
    Task<CanteenTableDto?> UpdateAsync(int canteenId, int tableId, UpdateCanteenTableDto dto);
    Task<bool> DeleteAsync(int tableId);
    Task<bool> DeleteAsync(int canteenId, int tableId);
}
