using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllByCanteenAsync(int canteenId, bool availableOnly = false);
    Task<IEnumerable<ProductDto>> GetByCategoryAsync(int canteenId, int categoryId, bool availableOnly = false);
    Task<ProductDto?> GetByIdAsync(int productId);
    Task<ProductDto?> GetByIdAsync(int canteenId, int productId);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto> CreateAsync(int canteenId, CreateProductDto dto);
    Task<ProductDto?> UpdateAsync(int productId, UpdateProductDto dto);
    Task<ProductDto?> UpdateAsync(int canteenId, int productId, UpdateProductDto dto);
    Task<bool> DeleteAsync(int productId);
    Task<bool> DeleteAsync(int canteenId, int productId);
    Task<bool> ReserveStockAsync(int canteenId, int productId, int quantity);
    Task<bool> ReleaseStockAsync(int canteenId, int productId, int quantity);
}
