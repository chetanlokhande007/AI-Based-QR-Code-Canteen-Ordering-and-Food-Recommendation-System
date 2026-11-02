using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class RecommendationRepository : IRecommendationRepository
{
    private readonly CanteenDbContext _context;

    public RecommendationRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Order>> GetRecentOrdersWithItemsAsync(int canteenId, int daysBack = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, daysBack));
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.CanteenId == canteenId && o.CreatedAt >= cutoff && o.Status != "Cancelled")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetCandidateProductsAsync(int canteenId)
    {
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Recipe)
                .ThenInclude(r => r!.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
            .Where(p => p.CanteenId == canteenId && p.IsAvailable)
            .ToListAsync();
    }

    public async Task<RecommendationEvent> AddEventAsync(RecommendationEvent ev)
    {
        _context.RecommendationEvents.Add(ev);
        await _context.SaveChangesAsync();
        return ev;
    }

    public async Task<IEnumerable<RecommendationEvent>> GetEventsAsync(int canteenId, int daysBack = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, daysBack));
        return await _context.RecommendationEvents
            .AsNoTracking()
            .Include(e => e.Product)
            .Where(e => e.CanteenId == canteenId && e.CreatedAt >= cutoff)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }
}

