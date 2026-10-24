using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order);

    Task<Order?> GetByIdAsync(int canteenId, int orderId);

    Task<IEnumerable<Order>> GetAllByCanteenAsync(int canteenId);
}
