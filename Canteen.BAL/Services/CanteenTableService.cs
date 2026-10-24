using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class CanteenTableService : ICanteenTableService
{
    private readonly ICanteenTableRepository _repo;

    public CanteenTableService(ICanteenTableRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<CanteenTableDto>> GetAllByCanteenAsync(int canteenId)
    {
        var entities = await _repo.GetAllByCanteenAsync(canteenId);
        return entities.Select(ToDto);
    }

    public async Task<CanteenTableDto?> GetByIdAsync(int tableId)
    {
        var entity = await _repo.GetByIdAsync(tableId);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CanteenTableDto?> GetByIdAsync(int canteenId, int tableId)
    {
        var entity = await _repo.GetByIdAsync(canteenId, tableId);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CanteenTableDto?> GetByQRTokenAsync(string qrToken)
    {
        var entity = await _repo.GetByQRTokenAsync(qrToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CanteenTableDto> CreateAsync(CreateCanteenTableDto dto)
    {
        var entity = new CanteenTable
        {
            CanteenId = dto.CanteenId,
            TableNumber = dto.TableNumber,
            Capacity = dto.Capacity,
            QRToken = Guid.NewGuid().ToString("N")
        };
        var created = await _repo.AddAsync(entity);
        return ToDto(created);
    }

    public async Task<CanteenTableDto?> UpdateAsync(int tableId, UpdateCanteenTableDto dto)
    {
        var entity = await _repo.GetByIdAsync(tableId);
        if (entity is null) return null;

        entity.TableNumber = dto.TableNumber;
        entity.Capacity = dto.Capacity;
        entity.IsAvailable = dto.IsAvailable;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<CanteenTableDto?> UpdateAsync(int canteenId, int tableId, UpdateCanteenTableDto dto)
    {
        var entity = await _repo.GetByIdAsync(canteenId, tableId);
        if (entity is null) return null;

        entity.TableNumber = dto.TableNumber;
        entity.Capacity = dto.Capacity;
        entity.IsAvailable = dto.IsAvailable;

        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(int tableId)
        => await _repo.DeleteAsync(tableId);

    public async Task<bool> DeleteAsync(int canteenId, int tableId)
        => await _repo.DeleteAsync(canteenId, tableId);

    private static CanteenTableDto ToDto(CanteenTable t) => new()
    {
        TableId = t.TableId,
        CanteenId = t.CanteenId,
        TableNumber = t.TableNumber,
        Capacity = t.Capacity,
        QRToken = t.QRToken,
        IsAvailable = t.IsAvailable
    };
}
