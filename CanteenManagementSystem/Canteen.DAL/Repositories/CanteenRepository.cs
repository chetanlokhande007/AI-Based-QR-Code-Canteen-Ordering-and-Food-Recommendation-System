using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class CanteenRepository : ICanteenRepository
{
    private readonly CanteenDbContext _context;

    public CanteenRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Model.Entities.Canteen>> GetAllAsync()
        => await _context.Canteens
            .AsNoTracking()
            .ToListAsync();

    public async Task<Model.Entities.Canteen?> GetByIdAsync(int id)
        => await _context.Canteens
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CanteenId == id);

    public async Task<Model.Entities.Canteen> AddAsync(Model.Entities.Canteen canteen)
    {
        _context.Canteens.Add(canteen);
        await _context.SaveChangesAsync();
        return canteen;
    }

    public async Task<Model.Entities.Canteen> UpdateAsync(Model.Entities.Canteen canteen)
    {
        _context.Canteens.Update(canteen);
        await _context.SaveChangesAsync();
        return canteen;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Canteens.FindAsync(id);
        if (entity is null) return false;
        _context.Canteens.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Canteens.AnyAsync(c => c.CanteenId == id);

    public async Task<IEnumerable<Model.Entities.Canteen>> GetByOwnerIdAsync(int ownerId)
    {
        return await _context.Canteens
            .AsNoTracking()
            .Where(c => c.OwnerId == ownerId ||
                        (c.OwnerId == null && _context.Owners.Any(o => o.OwnerId == ownerId && o.CanteenId == c.CanteenId)))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId)
    {
        return await _context.Canteens
            .AsNoTracking()
            .AnyAsync(c => c.CanteenId == canteenId &&
                           (c.OwnerId == ownerId ||
                            (c.OwnerId == null && _context.Owners.Any(o => o.OwnerId == ownerId && o.CanteenId == canteenId))));
    }

    public async Task<bool> IsOwnerAuthorizedAndActiveAsync(int ownerId, int canteenId)
    {
        return await _context.Canteens
            .AsNoTracking()
            .AnyAsync(c => c.CanteenId == canteenId &&
                           c.TenantStatus == Model.Enums.TenantStatus.Active &&
                           (c.OwnerId == ownerId ||
                            (c.OwnerId == null && _context.Owners.Any(o => o.OwnerId == ownerId && o.CanteenId == canteenId))));
    }

    public async Task<bool> IsCanteenActiveAsync(int canteenId)
    {
        return await _context.Canteens
            .AsNoTracking()
            .AnyAsync(c => c.CanteenId == canteenId && c.TenantStatus == Model.Enums.TenantStatus.Active);
    }

    public async Task<IEnumerable<Model.Entities.Canteen>> GetAdminListAsync()
    {
        return await _context.Canteens
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.Products)
            .Include(c => c.Tables)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}
