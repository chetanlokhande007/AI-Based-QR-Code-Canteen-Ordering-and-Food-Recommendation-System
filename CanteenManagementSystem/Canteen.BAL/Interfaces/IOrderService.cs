using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderDto dto, string qrToken);

    Task<OrderDto?> GetByIdAsync(int canteenId, int orderId);

    Task<IEnumerable<OrderDto>> GetAllByCanteenAsync(int canteenId);

    Task<OrderDto?> UpdateStatusAsync(int canteenId, int orderId, string newStatus);

    Task<OrderDto?> GetByTrackingTokenAsync(string trackingToken);

    Task<OrderDto?> CancelOrderByTrackingTokenAsync(string trackingToken, string? reason = null);

    Task<OrderDto?> CancelOrderFromCanteenAsync(int canteenId, int orderId, string reason, int actorOwnerId);

    Task<OrderCancellationDto?> GetCancellationByOrderIdAsync(int canteenId, int orderId);

    Task<IEnumerable<OrderCancellationDto>> GetCancellationsByCanteenAsync(int canteenId);
}
