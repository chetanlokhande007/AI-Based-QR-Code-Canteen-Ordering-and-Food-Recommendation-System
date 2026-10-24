using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllByCanteenAsync(int canteenId);
    Task<IEnumerable<ProductDto>> GetByCategoryAsync(int canteenId, int categoryId);
    Task<ProductDto?> GetByIdAsync(int productId);
    Task<ProductDto?> GetByIdAsync(int canteenId, int productId);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto> CreateAsync(int canteenId, CreateProductDto dto);
    Task<ProductDto?> UpdateAsync(int productId, UpdateProductDto dto);
    Task<ProductDto?> UpdateAsync(int canteenId, int productId, UpdateProductDto dto);
    Task<bool> DeleteAsync(int productId);
    Task<bool> DeleteAsync(int canteenId, int productId);
}
