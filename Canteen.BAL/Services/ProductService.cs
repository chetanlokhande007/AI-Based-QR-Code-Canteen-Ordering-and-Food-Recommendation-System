using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repo;

    public ProductService(IProductRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<ProductDto>> GetAllByCanteenAsync(int canteenId)
    {
        var entities = await _repo.GetAllByCanteenAsync(canteenId);
        return entities.Select(ToDto);
    }

    public async Task<IEnumerable<ProductDto>> GetByCategoryAsync(int canteenId, int categoryId)
    {
        var entities = await _repo.GetByCategoryAsync(canteenId, categoryId);
        return entities.Select(ToDto);
    }

    public async Task<ProductDto?> GetByIdAsync(int productId)
    {
        var entity = await _repo.GetByIdAsync(productId);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<ProductDto?> GetByIdAsync(int canteenId, int productId)
    {
        var entity = await _repo.GetByIdAsync(canteenId, productId);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var entity = new Product
        {
            CanteenId = dto.CanteenId,
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ImageUrl = dto.ImageUrl
        };
        var created = await _repo.AddAsync(entity);
        return ToDto(created);
    }

    public async Task<ProductDto> CreateAsync(int canteenId, CreateProductDto dto)
    {
        dto.CanteenId = canteenId;
        return await CreateAsync(dto);
    }

    public async Task<ProductDto?> UpdateAsync(int productId, UpdateProductDto dto)
    {
        var entity = await _repo.GetByIdAsync(productId);
        if (entity is null) return null;

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.StockQuantity = dto.StockQuantity;
        entity.IsAvailable = dto.IsAvailable;
        entity.ImageUrl = dto.ImageUrl;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<ProductDto?> UpdateAsync(int canteenId, int productId, UpdateProductDto dto)
    {
        var entity = await _repo.GetByIdAsync(canteenId, productId);
        if (entity is null) return null;

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.StockQuantity = dto.StockQuantity;
        entity.IsAvailable = dto.IsAvailable;
        entity.ImageUrl = dto.ImageUrl;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(int productId)
        => await _repo.DeleteAsync(productId);

    public async Task<bool> DeleteAsync(int canteenId, int productId)
        => await _repo.DeleteAsync(canteenId, productId);

    private static ProductDto ToDto(Product p) => new()
    {
        ProductId = p.ProductId,
        CanteenId = p.CanteenId,
        CategoryId = p.CategoryId,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        IsAvailable = p.IsAvailable,
        ImageUrl = p.ImageUrl,
        CreatedAt = p.CreatedAt
    };
}
