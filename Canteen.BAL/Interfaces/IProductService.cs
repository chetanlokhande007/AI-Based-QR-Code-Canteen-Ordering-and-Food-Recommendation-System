using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllByCanteenAsync(int canteenId);
    Task<IEnumerable<ProductDto>> GetByCategoryAsync(int canteenId, int categoryId);
    Task<ProductDto?> GetByIdAsync(int productId);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto?> UpdateAsync(int productId, UpdateProductDto dto);
    Task<bool> DeleteAsync(int productId);
}
