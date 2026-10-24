using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetAllByCanteenAsync(int canteenId);
    Task<Category?> GetByIdAsync(int canteenId, int categoryId);
    Task<Category> AddAsync(Category category);
    Task<Category> UpdateAsync(Category category);
    Task<bool> DeleteAsync(int canteenId, int categoryId);
    Task<bool> ExistsAsync(int canteenId, int categoryId);
}
