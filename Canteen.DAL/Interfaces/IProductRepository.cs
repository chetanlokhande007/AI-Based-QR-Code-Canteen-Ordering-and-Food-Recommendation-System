using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllByCanteenAsync(int canteenId);
    Task<IEnumerable<Product>> GetByCategoryAsync(int canteenId, int categoryId);
    Task<Product?> GetByIdAsync(int productId);
    Task<Product> AddAsync(Product product);
    Task<Product> UpdateAsync(Product product);
    Task<bool> DeleteAsync(int productId);
    Task<bool> ExistsAsync(int productId);
}
