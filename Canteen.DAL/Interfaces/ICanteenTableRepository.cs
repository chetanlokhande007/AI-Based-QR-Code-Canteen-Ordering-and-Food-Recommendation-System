using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface ICanteenTableRepository
{
    Task<IEnumerable<CanteenTable>> GetAllByCanteenAsync(int canteenId);
    Task<CanteenTable?> GetByIdAsync(int tableId);
    Task<CanteenTable?> GetByQRTokenAsync(string qrToken);
    Task<CanteenTable> AddAsync(CanteenTable table);
    Task<CanteenTable> UpdateAsync(CanteenTable table);
    Task<bool> DeleteAsync(int tableId);
}
