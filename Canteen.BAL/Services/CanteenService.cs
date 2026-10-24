using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class CanteenService : ICanteenService
{
    private readonly ICanteenRepository _repo;

    public CanteenService(ICanteenRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<CanteenDto>> GetAllAsync()
    {
        var entities = await _repo.GetAllAsync();
        return entities.Select(ToDto);
    }

    public async Task<CanteenDto?> GetByIdAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CanteenDto> CreateAsync(CreateCanteenDto dto)
    {
        var entity = new Model.Entities.Canteen
        {
            Name = dto.Name,
            Location = dto.Location
        };
        var created = await _repo.AddAsync(entity);
        return ToDto(created);
    }

    public async Task<CanteenDto?> UpdateAsync(int id, UpdateCanteenDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null) return null;

        entity.Name = dto.Name;
        entity.Location = dto.Location;
        entity.IsActive = dto.IsActive;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id)
        => await _repo.DeleteAsync(id);

    private static CanteenDto ToDto(Model.Entities.Canteen c) => new()
    {
        CanteenId = c.CanteenId,
        Name = c.Name,
        Location = c.Location,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt
    };
}
