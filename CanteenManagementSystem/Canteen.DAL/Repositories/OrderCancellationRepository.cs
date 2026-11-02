using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class OrderCancellationRepository : IOrderCancellationRepository
{
    private readonly CanteenDbContext _context;

    public OrderCancellationRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<OrderCancellation> AddAsync(OrderCancellation cancellation)
    {
        _context.OrderCancellations.Add(cancellation);
        await _context.SaveChangesAsync();
        return cancellation;
    }

    public async Task<OrderCancellation?> GetByIdAsync(int cancellationId)
    {
        return await _context.OrderCancellations
            .Include(oc => oc.Order)
            .Include(oc => oc.Canteen)
            .FirstOrDefaultAsync(oc => oc.CancellationId == cancellationId);
    }

    public async Task<OrderCancellation?> GetByOrderIdAsync(int orderId)
    {
        return await _context.OrderCancellations
            .Include(oc => oc.Order)
            .Include(oc => oc.Canteen)
            .FirstOrDefaultAsync(oc => oc.OrderId == orderId);
    }

    public async Task<IEnumerable<OrderCancellation>> GetAllByCanteenAsync(int canteenId)
    {
        return await _context.OrderCancellations
            .Include(oc => oc.Order)
            .Include(oc => oc.Canteen)
            .Where(oc => oc.CanteenId == canteenId)
            .OrderByDescending(oc => oc.CancelledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrderCancellation>> GetAllAsync()
    {
        return await _context.OrderCancellations
            .Include(oc => oc.Order)
            .Include(oc => oc.Canteen)
            .OrderByDescending(oc => oc.CancelledAt)
            .ToListAsync();
    }
}

