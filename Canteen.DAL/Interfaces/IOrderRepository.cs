using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order);

    Task<Order?> GetByIdAsync(int canteenId, int orderId, bool asNoTracking = true);

    Task<Order?> GetByIdAsync(int orderId, bool asNoTracking = true);

    Task<IEnumerable<Order>> GetAllByCanteenAsync(int canteenId);

    Task<Order> UpdateAsync(Order order);

    Task<bool> OrderNumberExistsAsync(int canteenId, string orderNumber);
}
