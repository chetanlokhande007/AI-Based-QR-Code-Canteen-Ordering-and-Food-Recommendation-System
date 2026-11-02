using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IRefundRepository
{
    Task<Refund> AddAsync(Refund refund);
    Task<Refund?> GetByIdAsync(int refundId);
    Task<Refund?> GetByOrderIdAsync(int orderId);
    Task<IEnumerable<Refund>> GetAllByCanteenAsync(int canteenId);
    Task<IEnumerable<Refund>> GetAllAsync();
    Task<Refund> UpdateAsync(Refund refund);
    Task<Refund?> GetByGatewayRefundIdAsync(string gatewayRefundId);
}

