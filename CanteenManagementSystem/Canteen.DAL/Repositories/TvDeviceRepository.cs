using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class TvDeviceRepository : ITvDeviceRepository
{
    private readonly CanteenDbContext _context;

    public TvDeviceRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<TvPairingSession> CreatePairingSessionAsync(TvPairingSession session)
    {
        _context.TvPairingSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<TvPairingSession?> GetActivePairingSessionByCodeAsync(string pairingCode)
    {
        var now = DateTime.UtcNow;
        return await _context.TvPairingSessions
            .Include(s => s.Canteen)
            .FirstOrDefaultAsync(s => s.PairingCode == pairingCode && !s.IsClaimed && s.ExpiresAt > now);
    }

    public async Task UpdatePairingSessionAsync(TvPairingSession session)
    {
        _context.TvPairingSessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<TvDevice> RegisterDeviceAsync(TvDevice device)
    {
        _context.TvDevices.Add(device);
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task<TvDevice?> GetDeviceByIdAsync(int deviceId)
    {
        return await _context.TvDevices
            .Include(d => d.Canteen)
            .FirstOrDefaultAsync(d => d.TvDeviceId == deviceId);
    }

    public async Task<TvDevice?> GetDeviceByIdentifierAsync(int canteenId, string deviceIdentifier)
    {
        return await _context.TvDevices
            .Include(d => d.Canteen)
            .FirstOrDefaultAsync(d => d.CanteenId == canteenId && d.DeviceIdentifier == deviceIdentifier);
    }

    public async Task<TvDevice?> GetDeviceByTokenHashAsync(string tokenHash)
    {
        return await _context.TvDevices
            .Include(d => d.Canteen)
            .FirstOrDefaultAsync(d => d.DeviceTokenHash == tokenHash && d.IsActive);
    }

    public async Task<IEnumerable<TvDevice>> GetAllByCanteenAsync(int canteenId)
    {
        return await _context.TvDevices
            .Where(d => d.CanteenId == canteenId)
            .OrderByDescending(d => d.RegisteredAt)
            .ToListAsync();
    }

    public async Task UpdateDeviceAsync(TvDevice device)
    {
        _context.TvDevices.Update(device);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteDeviceAsync(TvDevice device)
    {
        _context.TvDevices.Remove(device);
        await _context.SaveChangesAsync();
    }
}

