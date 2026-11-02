using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IOrderCancellationRepository
{
    Task<OrderCancellation> AddAsync(OrderCancellation cancellation);
    Task<OrderCancellation?> GetByIdAsync(int cancellationId);
    Task<OrderCancellation?> GetByOrderIdAsync(int orderId);
    Task<IEnumerable<OrderCancellation>> GetAllByCanteenAsync(int canteenId);
    Task<IEnumerable<OrderCancellation>> GetAllAsync();
}

