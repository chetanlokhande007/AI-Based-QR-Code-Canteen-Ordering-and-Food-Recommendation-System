using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly CanteenDbContext _context;

    public InventoryRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryIngredient?> GetIngredientByIdAsync(int canteenId, int ingredientId, bool asNoTracking = true)
    {
        var query = _context.InventoryIngredients.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(i => i.CanteenId == canteenId && i.IngredientId == ingredientId);
    }

    public async Task<IEnumerable<InventoryIngredient>> GetAllIngredientsAsync(int canteenId, bool activeOnly = false)
    {
        var query = _context.InventoryIngredients
            .AsNoTracking()
            .Where(i => i.CanteenId == canteenId);

        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query.OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync();
    }

    public async Task<IEnumerable<InventoryIngredient>> GetLowStockIngredientsAsync(int canteenId)
    {
        return await _context.InventoryIngredients
            .AsNoTracking()
            .Where(i => i.CanteenId == canteenId && i.IsActive && (i.CurrentStock - i.ReservedStock) <= i.MinimumStockLevel)
            .OrderBy(i => (i.CurrentStock - i.ReservedStock))
            .ToListAsync();
    }

    public async Task<bool> IngredientNameExistsAsync(int canteenId, string name, int? excludeId = null)
    {
        var normalized = name.Trim().ToLower();
        var query = _context.InventoryIngredients
            .Where(i => i.CanteenId == canteenId && i.Name.ToLower() == normalized);

        if (excludeId.HasValue)
        {
            query = query.Where(i => i.IngredientId != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<InventoryIngredient> AddIngredientAsync(InventoryIngredient ingredient)
    {
        _context.InventoryIngredients.Add(ingredient);
        await _context.SaveChangesAsync();
        return ingredient;
    }

    public async Task<InventoryIngredient> UpdateIngredientAsync(InventoryIngredient ingredient)
    {
        _context.InventoryIngredients.Update(ingredient);
        await _context.SaveChangesAsync();
        return ingredient;
    }

    public async Task<StockTransaction> AddStockTransactionAsync(StockTransaction transaction)
    {
        _context.StockTransactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<IEnumerable<StockTransaction>> GetTransactionsAsync(int canteenId, int? ingredientId = null, int limit = 50)
    {
        var query = _context.StockTransactions
            .AsNoTracking()
            .Include(t => t.Ingredient)
            .Where(t => t.CanteenId == canteenId);

        if (ingredientId.HasValue)
        {
            query = query.Where(t => t.IngredientId == ingredientId.Value);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Min(limit, 200))
            .ToListAsync();
    }

    public async Task<IngredientWastage> AddWastageAsync(IngredientWastage wastage)
    {
        _context.IngredientWastages.Add(wastage);
        await _context.SaveChangesAsync();
        return wastage;
    }

    public async Task<IEnumerable<IngredientWastage>> GetWastagesAsync(int canteenId, int? ingredientId = null, int limit = 50)
    {
        var query = _context.IngredientWastages
            .AsNoTracking()
            .Include(w => w.Ingredient)
            .Where(w => w.CanteenId == canteenId);

        if (ingredientId.HasValue)
        {
            query = query.Where(w => w.IngredientId == ingredientId.Value);
        }

        return await query
            .OrderByDescending(w => w.CreatedAt)
            .Take(Math.Min(limit, 200))
            .ToListAsync();
    }

    public async Task<StockReservation> AddReservationAsync(StockReservation reservation)
    {
        _context.StockReservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task<StockReservation?> GetReservationByOrderIdAsync(int canteenId, int orderId, bool asNoTracking = true)
    {
        var query = _context.StockReservations
            .Include(sr => sr.Items)
                .ThenInclude(i => i.Ingredient)
            .Where(sr => sr.CanteenId == canteenId && sr.OrderId == orderId);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<StockReservation> UpdateReservationAsync(StockReservation reservation)
    {
        _context.StockReservations.Update(reservation);
        await _context.SaveChangesAsync();
        return reservation;
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
