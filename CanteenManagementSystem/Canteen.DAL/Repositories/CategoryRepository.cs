using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly CanteenDbContext _context;

    public CategoryRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Category>> GetAllByCanteenAsync(int canteenId)
        => await _context.Categories
            .AsNoTracking()
            .Where(c => c.CanteenId == canteenId)
            .ToListAsync();

    public async Task<Category?> GetByIdAsync(int canteenId, int categoryId)
        => await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CanteenId == canteenId && c.CategoryId == categoryId);

    public async Task<Category> AddAsync(Category category)
    {
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<Category> UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<bool> DeleteAsync(int canteenId, int categoryId)
    {
        var entity = await _context.Categories
            .FirstOrDefaultAsync(c => c.CanteenId == canteenId && c.CategoryId == categoryId);
        if (entity is null) return false;
        _context.Categories.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int canteenId, int categoryId)
        => await _context.Categories.AnyAsync(c => c.CanteenId == canteenId && c.CategoryId == categoryId);
}
