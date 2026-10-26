using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repo;
    private readonly IProductRepository _productRepo;

    public CategoryService(
        ICategoryRepository repo,
        IProductRepository productRepo)
    {
        _repo = repo;
        _productRepo = productRepo;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllByCanteenAsync(
        int canteenId)
    {
        var entities = await _repo.GetAllByCanteenAsync(canteenId);
        return entities.Select(ToDto);
    }

    public async Task<CategoryDto?> GetByIdAsync(
        int canteenId,
        int categoryId)
    {
        var entity = await _repo.GetByIdAsync(canteenId, categoryId);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var entity = new Category
        {
            CanteenId = dto.CanteenId,
            Name = dto.Name,
            Description = dto.Description
        };

        var created = await _repo.AddAsync(entity);
        return ToDto(created);
    }

    public async Task<CategoryDto?> UpdateAsync(
        int canteenId,
        int categoryId,
        UpdateCategoryDto dto)
    {
        var entity = await _repo.GetByIdAsync(canteenId, categoryId);

        if (entity is null)
            return null;

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(int canteenId, int categoryId)
    {
        var category = await _repo.GetByIdAsync(canteenId, categoryId);

        if (category is null)
            return false;

        var hasProducts = await _productRepo.AnyWithCategoryAsync(
            canteenId,
            categoryId);

        if (hasProducts)
        {
            throw new CategoryNotEmptyException(
                "Cannot delete this category because it contains products.");
        }

        return await _repo.DeleteAsync(canteenId, categoryId);
    }

    private static CategoryDto ToDto(Category c) => new()
    {
        CategoryId = c.CategoryId,
        CanteenId = c.CanteenId,
        Name = c.Name,
        Description = c.Description,
        IsActive = c.IsActive
    };
}
