using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class RefundRepository : IRefundRepository
{
    private readonly CanteenDbContext _context;

    public RefundRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<Refund> AddAsync(Refund refund)
    {
        _context.Refunds.Add(refund);
        await _context.SaveChangesAsync();
        return refund;
    }

    public async Task<Refund?> GetByIdAsync(int refundId)
    {
        return await _context.Refunds
            .Include(r => r.Order)
            .Include(r => r.Payment)
            .Include(r => r.Canteen)
            .FirstOrDefaultAsync(r => r.RefundId == refundId);
    }

    public async Task<Refund?> GetByOrderIdAsync(int orderId)
    {
        return await _context.Refunds
            .Include(r => r.Order)
            .Include(r => r.Payment)
            .Include(r => r.Canteen)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(r => r.OrderId == orderId);
    }

    public async Task<IEnumerable<Refund>> GetAllByCanteenAsync(int canteenId)
    {
        return await _context.Refunds
            .Include(r => r.Order)
            .Include(r => r.Payment)
            .Include(r => r.Canteen)
            .Where(r => r.CanteenId == canteenId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Refund>> GetAllAsync()
    {
        return await _context.Refunds
            .Include(r => r.Order)
            .Include(r => r.Payment)
            .Include(r => r.Canteen)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Refund> UpdateAsync(Refund refund)
    {
        _context.Refunds.Update(refund);
        await _context.SaveChangesAsync();
        return refund;
    }

    public async Task<Refund?> GetByGatewayRefundIdAsync(string gatewayRefundId)
    {
        return await _context.Refunds
            .Include(r => r.Order)
            .Include(r => r.Payment)
            .Include(r => r.Canteen)
            .FirstOrDefaultAsync(r => r.GatewayRefundId == gatewayRefundId);
    }
}

