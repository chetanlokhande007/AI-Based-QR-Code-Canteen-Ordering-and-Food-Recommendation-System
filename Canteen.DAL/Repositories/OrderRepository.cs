using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly CanteenDbContext _context;

    public OrderRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<Order> AddAsync(Order order)
    {
        _context.Orders.Add(order);

        await _context.SaveChangesAsync();

        return order;
    }

    public async Task<Order?> GetByIdAsync(
        int canteenId,
        int orderId)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o =>
                o.CanteenId == canteenId &&
                o.OrderId == orderId);
    }

    public async Task<IEnumerable<Order>> GetAllByCanteenAsync(
        int canteenId)
    {
        return await _context.Orders
            .AsNoTracking()
            .Where(o => o.CanteenId == canteenId)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
}
