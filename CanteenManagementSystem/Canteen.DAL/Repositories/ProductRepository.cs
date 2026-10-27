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

    public async Task<IEnumerable<Product>> GetAllByCanteenAsync(
        int canteenId,
        bool availableOnly = false)
    {
        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.CanteenId == canteenId);

        if (availableOnly)
        {
            query = query.Where(p =>
                p.IsAvailable && p.StockQuantity > 0);
        }

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(
        int canteenId,
        int categoryId,
        bool availableOnly = false)
    {
        var query = _context.Products
            .AsNoTracking()
            .Where(p =>
                p.CanteenId == canteenId &&
                p.CategoryId == categoryId);

        if (availableOnly)
        {
            query = query.Where(p =>
                p.IsAvailable && p.StockQuantity > 0);
        }

        return await query.ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int productId)
        => await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == productId);

    public async Task<Product?> GetByIdAsync(int canteenId, int productId)
        => await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.CanteenId == canteenId &&
                p.ProductId == productId);

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

        if (entity is null)
            return false;

        _context.Products.Remove(entity);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int canteenId, int productId)
    {
        var entity = await _context.Products
            .FirstOrDefaultAsync(p =>
                p.CanteenId == canteenId &&
                p.ProductId == productId);

        if (entity is null)
            return false;

        _context.Products.Remove(entity);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ExistsAsync(int productId)
        => await _context.Products
            .AnyAsync(p => p.ProductId == productId);

    public async Task<bool> AnyWithCategoryAsync(
        int canteenId,
        int categoryId)
        => await _context.Products.AnyAsync(p =>
            p.CanteenId == canteenId &&
            p.CategoryId == categoryId);

    public async Task<bool> DeductStockAsync(int canteenId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (_context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory" && _context.Database.IsRelational())
        {
            var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Products\" SET \"StockQuantity\" = \"StockQuantity\" - {quantity} WHERE \"ProductId\" = {productId} AND \"CanteenId\" = {canteenId} AND \"IsAvailable\" = TRUE AND \"StockQuantity\" >= {quantity}");

            return rowsAffected > 0;
        }

        var product = await _context.Products.FirstOrDefaultAsync(p =>
            p.ProductId == productId &&
            p.CanteenId == canteenId);

        if (product == null || !product.IsAvailable || product.StockQuantity < quantity)
        {
            return false;
        }

        product.StockQuantity -= quantity;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreStockAsync(int canteenId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (_context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory" && _context.Database.IsRelational())
        {
            var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Products\" SET \"StockQuantity\" = \"StockQuantity\" + {quantity} WHERE \"ProductId\" = {productId} AND \"CanteenId\" = {canteenId}");

            return rowsAffected > 0;
        }

        var product = await _context.Products.FirstOrDefaultAsync(p =>
            p.ProductId == productId &&
            p.CanteenId == canteenId);

        if (product == null)
        {
            return false;
        }

        product.StockQuantity += quantity;
        await _context.SaveChangesAsync();
        return true;
    }
}
