using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface ITvDeviceRepository
{
    Task<TvPairingSession> CreatePairingSessionAsync(TvPairingSession session);
    Task<TvPairingSession?> GetActivePairingSessionByCodeAsync(string pairingCode);
    Task UpdatePairingSessionAsync(TvPairingSession session);

    Task<TvDevice> RegisterDeviceAsync(TvDevice device);
    Task<TvDevice?> GetDeviceByIdAsync(int deviceId);
    Task<TvDevice?> GetDeviceByIdentifierAsync(int canteenId, string deviceIdentifier);
    Task<TvDevice?> GetDeviceByTokenHashAsync(string tokenHash);
    Task<IEnumerable<TvDevice>> GetAllByCanteenAsync(int canteenId);
    Task UpdateDeviceAsync(TvDevice device);
    Task DeleteDeviceAsync(TvDevice device);
}

