using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class OwnerRepository : IOwnerRepository
{
    private readonly CanteenDbContext _context;

    public OwnerRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<Owner?> GetByIdAsync(int id)
    {
        return await _context.Owners
            .Include(o => o.Canteen)
            .FirstOrDefaultAsync(o => o.OwnerId == id);
    }

    public async Task<Owner?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLower();
        return await _context.Owners
            .Include(o => o.Canteen)
            .FirstOrDefaultAsync(o => o.Email.ToLower() == normalizedEmail);
    }

    public async Task<Owner> AddAsync(Owner owner)
    {
        _context.Owners.Add(owner);
        await _context.SaveChangesAsync();
        return owner;
    }

    public async Task<Owner> UpdateAsync(Owner owner)
    {
        _context.Owners.Update(owner);
        await _context.SaveChangesAsync();
        return owner;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLower();
        return await _context.Owners.AnyAsync(o => o.Email.ToLower() == normalizedEmail);
    }
}
