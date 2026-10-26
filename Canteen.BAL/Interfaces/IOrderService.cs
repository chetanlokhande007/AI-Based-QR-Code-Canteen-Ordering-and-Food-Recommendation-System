using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderDto dto, string qrToken);

    Task<OrderDto?> GetByIdAsync(int canteenId, int orderId);

    Task<IEnumerable<OrderDto>> GetAllByCanteenAsync(int canteenId);

    Task<OrderDto?> UpdateStatusAsync(int canteenId, int orderId, string newStatus);
}
