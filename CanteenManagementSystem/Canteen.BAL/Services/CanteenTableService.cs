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
        if (await _repo.TableNumberExistsAsync(dto.CanteenId, dto.TableNumber))
        {
            throw new InvalidOperationException($"Table number {dto.TableNumber} already exists in this canteen.");
        }

        var entity = new CanteenTable
        {
            CanteenId = dto.CanteenId,
            TableNumber = dto.TableNumber,
            Capacity = dto.Capacity,
            QRToken = Guid.NewGuid().ToString("N")
        };

        try
        {
            var created = await _repo.AddAsync(entity);
            return ToDto(created);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException($"Table number {dto.TableNumber} already exists in this canteen.", ex);
        }
    }

    public async Task<CanteenTableDto?> UpdateAsync(int tableId, UpdateCanteenTableDto dto)
    {
        var entity = await _repo.GetByIdAsync(tableId);
        if (entity is null) return null;

        if (entity.TableNumber != dto.TableNumber &&
            await _repo.TableNumberExistsAsync(entity.CanteenId, dto.TableNumber, tableId))
        {
            throw new InvalidOperationException($"Table number {dto.TableNumber} already exists in this canteen.");
        }

        entity.TableNumber = dto.TableNumber;
        entity.Capacity = dto.Capacity;
        entity.IsAvailable = dto.IsAvailable;

        try
        {
            var updated = await _repo.UpdateAsync(entity);
            return ToDto(updated);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException($"Table number {dto.TableNumber} already exists in this canteen.", ex);
        }
    }

    public async Task<CanteenTableDto?> UpdateAsync(int canteenId, int tableId, UpdateCanteenTableDto dto)
    {
        var entity = await _repo.GetByIdAsync(canteenId, tableId);
        if (entity is null) return null;

        if (entity.TableNumber != dto.TableNumber &&
            await _repo.TableNumberExistsAsync(canteenId, dto.TableNumber, tableId))
        {
            throw new InvalidOperationException($"Table number {dto.TableNumber} already exists in this canteen.");
        }

        entity.TableNumber = dto.TableNumber;
        entity.Capacity = dto.Capacity;
        entity.IsAvailable = dto.IsAvailable;

        try
        {
            var updated = await _repo.UpdateAsync(entity);
            return ToDto(updated);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException($"Table number {dto.TableNumber} already exists in this canteen.", ex);
        }
    }

    public async Task<bool> DeleteAsync(int tableId)
        => await _repo.DeleteAsync(tableId);

    public async Task<bool> DeleteAsync(int canteenId, int tableId)
        => await _repo.DeleteAsync(canteenId, tableId);

    public async Task<CanteenTableDto?> RegenerateQrTokenAsync(int canteenId, int tableId)
    {
        var entity = await _repo.GetByIdAsync(canteenId, tableId);
        if (entity is null) return null;

        entity.QRToken = Guid.NewGuid().ToString("N");
        var updated = await _repo.UpdateAsync(entity);
        return ToDto(updated);
    }

    private static CanteenTableDto ToDto(CanteenTable t) => new()
    {
        TableId = t.TableId,
        CanteenId = t.CanteenId,
        TableNumber = t.TableNumber,
        Capacity = t.Capacity,
        QRToken = t.QRToken,
        IsAvailable = t.IsAvailable
    };

    private static bool IsUniqueConstraintViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            return true;
        }

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("CK_Tables_CanteenId_TableNumber", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Tables_CanteenId_TableNumber", StringComparison.OrdinalIgnoreCase) ||
               (message.Contains("TableNumber", StringComparison.OrdinalIgnoreCase) && message.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }
}
