using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface ITvDeviceService
{
    Task<TvPairingSessionResponseDto> CreatePairingSessionAsync(int canteenId, CreateTvPairingRequestDto dto);
    Task<TvClaimResultDto> ClaimPairingCodeAsync(ClaimTvPairingDto dto);
    Task<IEnumerable<TvDeviceDto>> GetDevicesByCanteenAsync(int canteenId);
    Task<TvDeviceDto?> UpdateDeviceAsync(int canteenId, int deviceId, UpdateTvDeviceDto dto);
    Task<bool> RevokeDeviceAsync(int canteenId, int deviceId);
    Task<CanteenTvDisplayDto> GetStationDisplayDataAsync(int canteenId, string stationScope, string deviceName = "");
    Task<TvDeviceDto?> ValidateDeviceTokenAsync(string rawToken);
}
