using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    private readonly ICategoryRepository _categoryRepo;

    public ProductService(IProductRepository repo, ICategoryRepository categoryRepo)
    {
        _repo = repo;
        _categoryRepo = categoryRepo;
    }

    public async Task<IEnumerable<ProductDto>> GetAllByCanteenAsync(int canteenId, bool availableOnly = false)
    {
        var entities = await _repo.GetAllByCanteenAsync(canteenId, availableOnly);
        return entities.Select(ToDto);
    }

    public async Task<IEnumerable<ProductDto>> GetByCategoryAsync(int canteenId, int categoryId, bool availableOnly = false)
    {
        var entities = await _repo.GetByCategoryAsync(canteenId, categoryId, availableOnly);
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
        var categoryExists = await _categoryRepo.ExistsAsync(dto.CanteenId, dto.CategoryId);
        if (!categoryExists)
        {
            throw new ArgumentException($"Category with ID {dto.CategoryId} does not exist in canteen {dto.CanteenId}.");
        }

        var entity = new Product
        {
            CanteenId = dto.CanteenId,
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            FoodType = NormalizeFoodType(dto.FoodType),
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

        if (dto.CategoryId.HasValue && dto.CategoryId.Value != entity.CategoryId)
        {
            var categoryExists = await _categoryRepo.ExistsAsync(entity.CanteenId, dto.CategoryId.Value);
            if (!categoryExists)
            {
                throw new ArgumentException($"Category with ID {dto.CategoryId.Value} does not exist in canteen {entity.CanteenId}.");
            }
            entity.CategoryId = dto.CategoryId.Value;
        }

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.StockQuantity = dto.StockQuantity;
        entity.IsAvailable = dto.IsAvailable;
        if (!string.IsNullOrWhiteSpace(dto.FoodType))
        {
            entity.FoodType = NormalizeFoodType(dto.FoodType);
        }
        entity.ImageUrl = dto.ImageUrl;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<ProductDto?> UpdateAsync(int canteenId, int productId, UpdateProductDto dto)
    {
        var entity = await _repo.GetByIdAsync(canteenId, productId);
        if (entity is null) return null;

        if (dto.CategoryId.HasValue && dto.CategoryId.Value != entity.CategoryId)
        {
            var categoryExists = await _categoryRepo.ExistsAsync(canteenId, dto.CategoryId.Value);
            if (!categoryExists)
            {
                throw new ArgumentException($"Category with ID {dto.CategoryId.Value} does not exist in canteen {canteenId}.");
            }
            entity.CategoryId = dto.CategoryId.Value;
        }

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.StockQuantity = dto.StockQuantity;
        entity.IsAvailable = dto.IsAvailable;
        if (!string.IsNullOrWhiteSpace(dto.FoodType))
        {
            entity.FoodType = NormalizeFoodType(dto.FoodType);
        }
        entity.ImageUrl = dto.ImageUrl;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(int productId)
        => await _repo.DeleteAsync(productId);

    public async Task<bool> DeleteAsync(int canteenId, int productId)
        => await _repo.DeleteAsync(canteenId, productId);

    public async Task<bool> ReserveStockAsync(int canteenId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity to reserve must be greater than zero.");
        }

        return await _repo.DeductStockAsync(canteenId, productId, quantity);
    }

    public async Task<bool> ReleaseStockAsync(int canteenId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity to release must be greater than zero.");
        }

        return await _repo.RestoreStockAsync(canteenId, productId, quantity);
    }

    private static string NormalizeFoodType(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Veg";
        var cleaned = input.Trim().Replace("-", "").Replace(" ", "");
        if (cleaned.Equals("NonVeg", StringComparison.OrdinalIgnoreCase)) return "NonVeg";
        return "Veg";
    }

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
        FoodType = string.IsNullOrWhiteSpace(p.FoodType) ? "Veg" : p.FoodType,
        ImageUrl = p.ImageUrl,
        CreatedAt = p.CreatedAt
    };
}
