using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly CanteenDbContext _context;

    public ProductRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllByCanteenAsync(int canteenId)
        => await _context.Products
            .AsNoTracking()
            .Where(p => p.CanteenId == canteenId)
            .ToListAsync();

    public async Task<IEnumerable<Product>> GetByCategoryAsync(int canteenId, int categoryId)
        => await _context.Products
            .AsNoTracking()
            .Where(p => p.CanteenId == canteenId && p.CategoryId == categoryId)
            .ToListAsync();

    public async Task<Product?> GetByIdAsync(int productId)
        => await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == productId);

    public async Task<Product?> GetByIdAsync(int canteenId, int productId)
        => await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CanteenId == canteenId && p.ProductId == productId);

    public async Task<Product> AddAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<Product> UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<bool> DeleteAsync(int productId)
    {
        var entity = await _context.Products.FindAsync(productId);
        if (entity is null) return false;
        _context.Products.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int canteenId, int productId)
    {
        var entity = await _context.Products
            .FirstOrDefaultAsync(p => p.CanteenId == canteenId && p.ProductId == productId);
        if (entity is null) return false;
        _context.Products.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int productId)
        => await _context.Products.AnyAsync(p => p.ProductId == productId);
}
