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
        int orderId,
        bool asNoTracking = true)
    {
        IQueryable<Order> query = _context.Orders;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o =>
                o.CanteenId == canteenId &&
                o.OrderId == orderId);
    }

    public async Task<Order?> GetByIdAsync(
        int orderId,
        bool asNoTracking = true)
    {
        IQueryable<Order> query = _context.Orders;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);
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

    public async Task<Order> UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> GetByTrackingTokenAsync(
        string trackingToken,
        bool asNoTracking = true)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return null;
        }

        IQueryable<Order> query = _context.Orders;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.TrackingToken == trackingToken);
    }

    public async Task<bool> OrderNumberExistsAsync(int canteenId, string orderNumber)
    {
        return await _context.Orders
            .AnyAsync(o => o.CanteenId == canteenId && o.OrderNumber == orderNumber);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        if (!_context.Database.IsRelational() || _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return await operation();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var result = await operation();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
