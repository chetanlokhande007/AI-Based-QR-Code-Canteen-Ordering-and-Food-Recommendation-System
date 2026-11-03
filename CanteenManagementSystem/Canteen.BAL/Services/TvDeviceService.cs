using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class TvDeviceService : ITvDeviceService
{
    private readonly ITvDeviceRepository _tvRepo;
    private readonly ICanteenRepository _canteenRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IOrderNotificationService? _notificationService;

    public TvDeviceService(
        ITvDeviceRepository tvRepo,
        ICanteenRepository canteenRepo,
        IOrderRepository orderRepo,
        IOrderNotificationService? notificationService = null)
    {
        _tvRepo = tvRepo;
        _canteenRepo = canteenRepo;
        _orderRepo = orderRepo;
        _notificationService = notificationService;
    }

    public async Task<TvPairingSessionResponseDto> CreatePairingSessionAsync(int canteenId, CreateTvPairingRequestDto dto)
    {
        var canteen = await _canteenRepo.GetByIdAsync(canteenId);
        if (canteen == null)
        {
            throw new ArgumentException("Canteen not found.");
        }

        // Generate cryptographically secure 6-digit numeric PIN
        var pin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

        var session = new TvPairingSession
        {
            CanteenId = canteenId,
            PairingCode = pin,
            StationScope = NormalizeStationScope(dto.StationScope),
            IsClaimed = false,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        var saved = await _tvRepo.CreatePairingSessionAsync(session);

        return new TvPairingSessionResponseDto
        {
            SessionId = saved.SessionId,
            CanteenId = canteenId,
            PairingCode = saved.PairingCode,
            StationScope = saved.StationScope,
            ExpiresAt = saved.ExpiresAt,
            ExpiresInSeconds = (int)(saved.ExpiresAt - DateTime.UtcNow).TotalSeconds
        };
    }

    public async Task<TvClaimResultDto> ClaimPairingCodeAsync(ClaimTvPairingDto dto)
    {
        var cleanCode = dto.PairingCode?.Trim() ?? string.Empty;
        var session = await _tvRepo.GetActivePairingSessionByCodeAsync(cleanCode);

        if (session == null)
        {
            return new TvClaimResultDto
            {
                Success = false,
                Message = "Invalid or expired pairing code. Please generate a new code from CanteenHub settings."
            };
        }

        // Rate limiting: max 5 attempts per session
        if (session.AttemptCount >= 5)
        {
            return new TvClaimResultDto
            {
                Success = false,
                Message = "Too many failed attempts. Pairing code has been invalidated for security."
            };
        }

        session.AttemptCount++;

        // Generate unguessable 64-character device secret
        var rawDeviceToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = HashDeviceToken(rawDeviceToken);

        // Check if device already registered for this canteen
        var existing = await _tvRepo.GetDeviceByIdentifierAsync(session.CanteenId, dto.DeviceIdentifier);
        TvDevice device;

        if (existing != null)
        {
            existing.DeviceName = !string.IsNullOrWhiteSpace(dto.DeviceName) ? dto.DeviceName.Trim() : existing.DeviceName;
            existing.DeviceTokenHash = tokenHash;
            existing.StationScope = session.StationScope;
            existing.IsActive = true;
            existing.RevokedAt = null;
            existing.LastConnectedAt = DateTime.UtcNow;
            await _tvRepo.UpdateDeviceAsync(existing);
            device = existing;
        }
        else
        {
            device = new TvDevice
            {
                CanteenId = session.CanteenId,
                DeviceName = !string.IsNullOrWhiteSpace(dto.DeviceName) ? dto.DeviceName.Trim() : "Dining Hall Smart TV",
                DeviceIdentifier = dto.DeviceIdentifier.Trim(),
                DeviceTokenHash = tokenHash,
                StationScope = session.StationScope,
                IsActive = true,
                RegisteredAt = DateTime.UtcNow,
                LastConnectedAt = DateTime.UtcNow
            };
            await _tvRepo.RegisterDeviceAsync(device);
        }

        // Mark pairing session claimed
        session.IsClaimed = true;
        await _tvRepo.UpdatePairingSessionAsync(session);

        return new TvClaimResultDto
        {
            Success = true,
            Message = "Smart TV successfully paired!",
            DeviceToken = rawDeviceToken,
            CanteenId = session.CanteenId,
            CanteenName = session.Canteen?.Name ?? "Smart Canteen",
            TvDeviceId = device.TvDeviceId,
            DeviceName = device.DeviceName,
            StationScope = device.StationScope
        };
    }

    public async Task<IEnumerable<TvDeviceDto>> GetDevicesByCanteenAsync(int canteenId)
    {
        var devices = await _tvRepo.GetAllByCanteenAsync(canteenId);
        return devices.Select(d => new TvDeviceDto
        {
            TvDeviceId = d.TvDeviceId,
            CanteenId = d.CanteenId,
            DeviceName = d.DeviceName,
            DeviceIdentifier = d.DeviceIdentifier,
            StationScope = d.StationScope,
            IsActive = d.IsActive,
            RegisteredAt = d.RegisteredAt,
            LastConnectedAt = d.LastConnectedAt,
            RevokedAt = d.RevokedAt
        });
    }

    public async Task<TvDeviceDto?> UpdateDeviceAsync(int canteenId, int deviceId, UpdateTvDeviceDto dto)
    {
        var device = await _tvRepo.GetDeviceByIdAsync(deviceId);
        if (device == null || device.CanteenId != canteenId)
        {
            return null;
        }

        device.DeviceName = dto.DeviceName.Trim();
        device.StationScope = NormalizeStationScope(dto.StationScope);
        await _tvRepo.UpdateDeviceAsync(device);

        return new TvDeviceDto
        {
            TvDeviceId = device.TvDeviceId,
            CanteenId = device.CanteenId,
            DeviceName = device.DeviceName,
            DeviceIdentifier = device.DeviceIdentifier,
            StationScope = device.StationScope,
            IsActive = device.IsActive,
            RegisteredAt = device.RegisteredAt,
            LastConnectedAt = device.LastConnectedAt,
            RevokedAt = device.RevokedAt
        };
    }

    public async Task<bool> RevokeDeviceAsync(int canteenId, int deviceId)
    {
        var device = await _tvRepo.GetDeviceByIdAsync(deviceId);
        if (device == null || device.CanteenId != canteenId)
        {
            return false;
        }

        device.IsActive = false;
        device.RevokedAt = DateTime.UtcNow;
        await _tvRepo.UpdateDeviceAsync(device);

        return true;
    }

    public async Task<TvDeviceDto?> ValidateDeviceTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var tokenHash = HashDeviceToken(rawToken);
        var device = await _tvRepo.GetDeviceByTokenHashAsync(tokenHash);

        if (device == null || !device.IsActive)
        {
            return null;
        }

        device.LastConnectedAt = DateTime.UtcNow;
        await _tvRepo.UpdateDeviceAsync(device);

        return new TvDeviceDto
        {
            TvDeviceId = device.TvDeviceId,
            CanteenId = device.CanteenId,
            DeviceName = device.DeviceName,
            DeviceIdentifier = device.DeviceIdentifier,
            StationScope = device.StationScope,
            IsActive = device.IsActive,
            RegisteredAt = device.RegisteredAt,
            LastConnectedAt = device.LastConnectedAt,
            RevokedAt = device.RevokedAt
        };
    }

    public async Task<CanteenTvDisplayDto> GetStationDisplayDataAsync(int canteenId, string stationScope, string deviceName = "")
    {
        var canteen = await _canteenRepo.GetByIdAsync(canteenId);
        var canteenName = canteen?.Name ?? "Smart Canteen";

        var orders = await _orderRepo.GetAllByCanteenAsync(canteenId);
        var activeOrders = orders.Where(o =>
            string.Equals(o.Status, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Status, "Preparing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Status, "Ready", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        // Apply station scoping
        var normalizedScope = NormalizeStationScope(stationScope);
        if (normalizedScope != "All")
        {
            activeOrders = activeOrders.Where(o => MatchesStationScope(o, normalizedScope)).ToList();
        }

        var preparing = activeOrders
            .Where(o => string.Equals(o.Status, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Status, "Preparing", StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.CreatedAt)
            .Select(MapToTvToken)
            .ToList();

        var ready = activeOrders
            .Where(o => string.Equals(o.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.CreatedAt)
            .Select(MapToTvToken)
            .ToList();

        return new CanteenTvDisplayDto
        {
            CanteenId = canteenId,
            CanteenName = canteenName,
            StationScope = normalizedScope,
            DeviceName = deviceName,
            PreparingOrders = preparing,
            ReadyOrders = ready
        };
    }

    private static bool MatchesStationScope(Order order, string stationScope)
    {
        if (stationScope == "All") return true;

        if (order.Items == null || !order.Items.Any()) return true;

        var isBeverageScope = string.Equals(stationScope, "Beverage", StringComparison.OrdinalIgnoreCase);

        // Keywords for beverages
        string[] beverageKeywords = ["drink", "beverage", "tea", "coffee", "juice", "shake", "soda", "water", "cold drink", "chai", "lassi"];

        bool hasBeverage = order.Items.Any(i =>
            i.Product != null && beverageKeywords.Any(k => i.Product.Name.Contains(k, StringComparison.OrdinalIgnoreCase)));

        return isBeverageScope ? hasBeverage : !hasBeverage;
    }

    private static TvTokenDto MapToTvToken(Order order)
    {
        var token = order.OrderNumber;
        if (!string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            var parts = order.OrderNumber.Split('-');
            token = parts.Length > 0 ? parts[^1] : order.OrderNumber;
        }

        return new TvTokenDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            TokenNumber = token,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.CreatedAt,
            ItemCount = order.Items?.Count ?? 0
        };
    }

    private static string NormalizeStationScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope)) return "All";
        var s = scope.Trim().ToLowerInvariant();
        return s switch
        {
            "beverage" or "drinks" => "Beverage",
            "food" or "kitchen" => "Food",
            _ => "All"
        };
    }

    private static string HashDeviceToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
