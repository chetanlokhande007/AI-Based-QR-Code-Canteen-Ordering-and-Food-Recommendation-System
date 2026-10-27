using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface ICanteenRepository
{
    Task<IEnumerable<Model.Entities.Canteen>> GetAllAsync();
    Task<Model.Entities.Canteen?> GetByIdAsync(int id);
    Task<Model.Entities.Canteen> AddAsync(Model.Entities.Canteen canteen);
    Task<Model.Entities.Canteen> UpdateAsync(Model.Entities.Canteen canteen);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<IEnumerable<Model.Entities.Canteen>> GetByOwnerIdAsync(int ownerId);
    Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId);
}
