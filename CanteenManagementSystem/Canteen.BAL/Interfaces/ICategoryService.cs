using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllByCanteenAsync(int canteenId);
    Task<CategoryDto?> GetByIdAsync(int canteenId, int categoryId);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task<CategoryDto?> UpdateAsync(int canteenId, int categoryId, UpdateCategoryDto dto);
    Task<bool> DeleteAsync(int canteenId, int categoryId);
}
