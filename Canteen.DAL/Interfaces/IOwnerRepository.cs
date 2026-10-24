using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IOwnerRepository
{
    Task<Owner?> GetByIdAsync(int id);
    Task<Owner?> GetByEmailAsync(string email);
    Task<Owner> AddAsync(Owner owner);
    Task<bool> EmailExistsAsync(string email);
}
