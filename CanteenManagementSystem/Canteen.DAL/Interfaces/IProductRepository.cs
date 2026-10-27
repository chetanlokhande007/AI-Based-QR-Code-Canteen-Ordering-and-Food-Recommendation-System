using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllByCanteenAsync(
        int canteenId,
        bool availableOnly = false);

    Task<IEnumerable<Product>> GetByCategoryAsync(
        int canteenId,
        int categoryId,
        bool availableOnly = false);

    Task<Product?> GetByIdAsync(int productId);

    Task<Product?> GetByIdAsync(int canteenId, int productId);

    Task<Product> AddAsync(Product product);

    Task<Product> UpdateAsync(Product product);

    Task<bool> DeleteAsync(int productId);

    Task<bool> DeleteAsync(int canteenId, int productId);

    Task<bool> ExistsAsync(int productId);

    Task<bool> AnyWithCategoryAsync(int canteenId, int categoryId);

    Task<bool> DeductStockAsync(int canteenId, int productId, int quantity);

    Task<bool> RestoreStockAsync(int canteenId, int productId, int quantity);
}
