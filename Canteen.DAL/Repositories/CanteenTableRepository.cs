using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class CanteenTableRepository : ICanteenTableRepository
{
    private readonly CanteenDbContext _context;

    public CanteenTableRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CanteenTable>> GetAllByCanteenAsync(int canteenId)
        => await _context.Tables
            .AsNoTracking()
            .Where(t => t.CanteenId == canteenId)
            .ToListAsync();

    public async Task<CanteenTable?> GetByIdAsync(int tableId)
        => await _context.Tables
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TableId == tableId);

    public async Task<CanteenTable?> GetByIdAsync(int canteenId, int tableId)
        => await _context.Tables
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CanteenId == canteenId && t.TableId == tableId);

    public async Task<CanteenTable?> GetByQRTokenAsync(string qrToken)
        => await _context.Tables
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.QRToken == qrToken);

    public async Task<CanteenTable> AddAsync(CanteenTable table)
    {
        _context.Tables.Add(table);
        await _context.SaveChangesAsync();
        return table;
    }

    public async Task<CanteenTable> UpdateAsync(CanteenTable table)
    {
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();
        return table;
    }

    public async Task<bool> DeleteAsync(int tableId)
    {
        var entity = await _context.Tables.FindAsync(tableId);
        if (entity is null) return false;
        _context.Tables.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int canteenId, int tableId)
    {
        var entity = await _context.Tables
            .FirstOrDefaultAsync(t => t.CanteenId == canteenId && t.TableId == tableId);
        if (entity is null) return false;
        _context.Tables.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TableNumberExistsAsync(int canteenId, int tableNumber, int? excludeTableId = null)
    {
        var query = _context.Tables.Where(t => t.CanteenId == canteenId && t.TableNumber == tableNumber);
        if (excludeTableId.HasValue)
        {
            query = query.Where(t => t.TableId != excludeTableId.Value);
        }
        return await query.AnyAsync();
    }
}
