using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using CanteenManagementSystem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Canteen.Tests;

public class TvPairingAndDeviceTests
{
    private readonly FakeTvDeviceRepository _fakeTvRepo;
    private readonly FakeCanteenRepository _fakeCanteenRepo;
    private readonly FakeOrderRepository _fakeOrderRepo;
    private readonly TvDeviceService _tvDeviceService;
    private readonly TvDevicesController _controller;

    public TvPairingAndDeviceTests()
    {
        _fakeTvRepo = new FakeTvDeviceRepository();
        _fakeCanteenRepo = new FakeCanteenRepository();
        _fakeOrderRepo = new FakeOrderRepository();

        _tvDeviceService = new TvDeviceService(
            _fakeTvRepo,
            _fakeCanteenRepo,
            _fakeOrderRepo,
            null);

        var fakeCanteenService = new FakeCanteenService();
        _controller = new TvDevicesController(_tvDeviceService, fakeCanteenService, null);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Role, "CanteenOwner"),
            new("OwnerId", "1"),
            new("CanteenId", "1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task CreatePairingSession_GeneratesValid6DigitPinAnd5MinuteExpiry()
    {
        // Arrange
        _fakeCanteenRepo.Canteens.Add(new Canteen.Model.Entities.Canteen { CanteenId = 1, Name = "CT Catten" });

        // Act
        var result = await _tvDeviceService.CreatePairingSessionAsync(1, new CreateTvPairingRequestDto
        {
            DeviceName = "Main Kitchen Wall TV",
            StationScope = "All"
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.PairingCode.Length);
        Assert.True(int.TryParse(result.PairingCode, out _));
        Assert.Equal("All", result.StationScope);
        Assert.True(result.ExpiresInSeconds > 250);
        Assert.Single(_fakeTvRepo.Sessions);
    }

    [Fact]
    public async Task ClaimPairingCode_RegistersTvDeviceAndReturnsDeviceToken()
    {
        // Arrange
        _fakeCanteenRepo.Canteens.Add(new Canteen.Model.Entities.Canteen { CanteenId = 1, Name = "CT Catten" });
        var session = await _tvDeviceService.CreatePairingSessionAsync(1, new CreateTvPairingRequestDto
        {
            DeviceName = "Beverage Counter TV",
            StationScope = "Beverage"
        });

        // Act
        var claimResult = await _tvDeviceService.ClaimPairingCodeAsync(new ClaimTvPairingDto
        {
            PairingCode = session.PairingCode,
            DeviceIdentifier = "SMART_TV_MAC_AA_BB_CC",
            DeviceName = "Samsung Smart TV"
        });

        // Assert
        Assert.True(claimResult.Success);
        Assert.NotNull(claimResult.DeviceToken);
        Assert.Equal(64, claimResult.DeviceToken.Length);
        Assert.Equal(1, claimResult.CanteenId);
        Assert.Equal("Beverage", claimResult.StationScope);
        Assert.True(_fakeTvRepo.Sessions[0].IsClaimed);
        Assert.Single(_fakeTvRepo.Devices);
    }

    [Fact]
    public async Task ClaimPairingCode_RejectsExpiredOrInvalidCode()
    {
        // Act
        var result = await _tvDeviceService.ClaimPairingCodeAsync(new ClaimTvPairingDto
        {
            PairingCode = "000000",
            DeviceIdentifier = "ANY_TV"
        });

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid or expired", result.Message);
    }

    [Fact]
    public async Task ValidateDeviceToken_SucceedsForActiveDeviceAndFailsForRevokedDevice()
    {
        // Arrange
        _fakeCanteenRepo.Canteens.Add(new Canteen.Model.Entities.Canteen { CanteenId = 1, Name = "CT Catten" });
        var session = await _tvDeviceService.CreatePairingSessionAsync(1, new CreateTvPairingRequestDto());
        var claim = await _tvDeviceService.ClaimPairingCodeAsync(new ClaimTvPairingDto
        {
            PairingCode = session.PairingCode,
            DeviceIdentifier = "TV_DEVICE_1"
        });

        // Act 1: Validate active token
        var valid = await _tvDeviceService.ValidateDeviceTokenAsync(claim.DeviceToken!);
        Assert.NotNull(valid);
        Assert.True(valid.IsActive);

        // Act 2: Revoke device
        await _tvDeviceService.RevokeDeviceAsync(1, valid.TvDeviceId);

        // Act 3: Validate revoked token
        var invalid = await _tvDeviceService.ValidateDeviceTokenAsync(claim.DeviceToken!);
        Assert.Null(invalid);
    }

    [Fact]
    public async Task GetStationDisplayData_FiltersBeveragesWhenScopeIsBeverage()
    {
        // Arrange
        _fakeCanteenRepo.Canteens.Add(new Canteen.Model.Entities.Canteen { CanteenId = 1, Name = "CT Catten" });

        var beverageOrder = new Order
        {
            OrderId = 1,
            CanteenId = 1,
            OrderNumber = "ORD-20261103-1001",
            Status = "Ready",
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, Product = new Product { Name = "Masala Chai", CategoryId = 1 } }
            }
        };

        var foodOrder = new Order
        {
            OrderId = 2,
            CanteenId = 1,
            OrderNumber = "ORD-20261103-1002",
            Status = "Ready",
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>
            {
                new() { ProductId = 2, Product = new Product { Name = "Paneer Tikka Roll", CategoryId = 2 } }
            }
        };

        _fakeOrderRepo.Orders.AddRange(new[] { beverageOrder, foodOrder });

        // Act 1: Query Beverage Station
        var bevData = await _tvDeviceService.GetStationDisplayDataAsync(1, "Beverage");
        Assert.Single(bevData.ReadyOrders);
        Assert.Equal("1001", bevData.ReadyOrders[0].TokenNumber);

        // Act 2: Query All Stations
        var allData = await _tvDeviceService.GetStationDisplayDataAsync(1, "All");
        Assert.Equal(2, allData.ReadyOrders.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // In-Memory Fakes
    // ─────────────────────────────────────────────────────────────────────────

    private class FakeTvDeviceRepository : ITvDeviceRepository
    {
        public List<TvPairingSession> Sessions { get; } = new();
        public List<TvDevice> Devices { get; } = new();

        public Task<TvPairingSession> CreatePairingSessionAsync(TvPairingSession session)
        {
            session.SessionId = Sessions.Count + 1;
            Sessions.Add(session);
            return Task.FromResult(session);
        }

        public Task<TvPairingSession?> GetActivePairingSessionByCodeAsync(string pairingCode)
        {
            var now = DateTime.UtcNow;
            var session = Sessions.FirstOrDefault(s => s.PairingCode == pairingCode && !s.IsClaimed && s.ExpiresAt > now);
            return Task.FromResult(session);
        }

        public Task UpdatePairingSessionAsync(TvPairingSession session) => Task.CompletedTask;

        public Task<TvDevice> RegisterDeviceAsync(TvDevice device)
        {
            device.TvDeviceId = Devices.Count + 1;
            Devices.Add(device);
            return Task.FromResult(device);
        }

        public Task<TvDevice?> GetDeviceByIdAsync(int deviceId) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.TvDeviceId == deviceId));

        public Task<TvDevice?> GetDeviceByIdentifierAsync(int canteenId, string deviceIdentifier) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.CanteenId == canteenId && d.DeviceIdentifier == deviceIdentifier));

        public Task<TvDevice?> GetDeviceByTokenHashAsync(string tokenHash) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.DeviceTokenHash == tokenHash && d.IsActive));

        public Task<IEnumerable<TvDevice>> GetAllByCanteenAsync(int canteenId) =>
            Task.FromResult<IEnumerable<TvDevice>>(Devices.Where(d => d.CanteenId == canteenId));

        public Task UpdateDeviceAsync(TvDevice device) => Task.CompletedTask;
        public Task DeleteDeviceAsync(TvDevice device)
        {
            Devices.Remove(device);
            return Task.CompletedTask;
        }
    }

    private class FakeCanteenRepository : ICanteenRepository
    {
        public List<Canteen.Model.Entities.Canteen> Canteens { get; } = new();

        public Task<Canteen.Model.Entities.Canteen?> GetByIdAsync(int id) =>
            Task.FromResult(Canteens.FirstOrDefault(c => c.CanteenId == id));

        public Task<bool> IsOwnerAuthorizedAndActiveAsync(int ownerId, int canteenId) => Task.FromResult(true);

        public Task<IEnumerable<Canteen.Model.Entities.Canteen>> GetAllAsync() => throw new NotImplementedException();
        public Task<Canteen.Model.Entities.Canteen?> GetBySlugAsync(string slug) => throw new NotImplementedException();
        public Task<Canteen.Model.Entities.Canteen> AddAsync(Canteen.Model.Entities.Canteen canteen) => throw new NotImplementedException();
        public Task<Canteen.Model.Entities.Canteen?> UpdateAsync(int id, Canteen.Model.Entities.Canteen canteen) => throw new NotImplementedException();
        public Task<Canteen.Model.Entities.Canteen?> UpdateAsync(Canteen.Model.Entities.Canteen canteen) => Task.FromResult<Canteen.Model.Entities.Canteen?>(canteen);
        public Task<bool> ExistsAsync(int id) => Task.FromResult(true);
        public Task<IEnumerable<Canteen.Model.Entities.Canteen>> GetAdminListAsync() => Task.FromResult<IEnumerable<Canteen.Model.Entities.Canteen>>(Canteens);
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<Canteen.Model.Entities.Canteen>> GetByOwnerIdAsync(int ownerId) => throw new NotImplementedException();


        public Task<Canteen.Model.Entities.CanteenCancellationPolicy?> GetCancellationPolicyAsync(int canteenId) => Task.FromResult<Canteen.Model.Entities.CanteenCancellationPolicy?>(null);
        public Task<Canteen.Model.Entities.CanteenCancellationPolicy> SaveCancellationPolicyAsync(int canteenId, Canteen.Model.Entities.CanteenCancellationPolicy policy) => Task.FromResult(policy);

        public Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId) => Task.FromResult(true);


        public Task<bool> IsCanteenActiveAsync(int canteenId) => Task.FromResult(true);
        public Task<bool> SlugExistsAsync(string slug, int? excludeCanteenId = null) => Task.FromResult(false);
    }

    private class FakeOrderRepository : IOrderRepository
    {
        public List<Order> Orders { get; } = new();

        public Task<IEnumerable<Order>> GetAllByCanteenAsync(int canteenId) =>
            Task.FromResult<IEnumerable<Order>>(Orders.Where(o => o.CanteenId == canteenId));

        public Task<Order?> GetByIdAsync(int canteenId, int orderId, bool asNoTracking = false) =>
            Task.FromResult(Orders.FirstOrDefault(o => o.CanteenId == canteenId && o.OrderId == orderId));

        public Task<Order?> GetByIdAsync(int orderId, bool asNoTracking = false) =>
            Task.FromResult(Orders.FirstOrDefault(o => o.OrderId == orderId));

        public Task<Order?> GetByTrackingTokenAsync(string trackingToken, bool asNoTracking = false) => throw new NotImplementedException();
        public Task<Order> AddAsync(Order order) => throw new NotImplementedException();
        public Task<Order> UpdateAsync(Order order) => Task.FromResult(order);
        public Task<bool> OrderNumberExistsAsync(int canteenId, string orderNumber) => Task.FromResult(false);
        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation) => operation();
    }

    private class FakeCanteenService : ICanteenService
    {
        public Task<bool> IsOwnerAuthorizedAndActiveAsync(int ownerId, int canteenId) => Task.FromResult(true);
        public Task<IEnumerable<CanteenDto>> GetAllAsync() => throw new NotImplementedException();
        public Task<CanteenDto?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<CanteenDto?> GetBySlugAsync(string slug) => throw new NotImplementedException();
        public Task<CanteenDto> CreateAsync(CreateCanteenDto dto, int? ownerId = null) => throw new NotImplementedException();
        public Task<CanteenDto?> UpdateAsync(int id, UpdateCanteenDto dto) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<CanteenSummaryDto>> GetOwnerCanteensAsync(int ownerId) => throw new NotImplementedException();


        public Task<CanteenCancellationPolicyDto> GetCancellationPolicyAsync(int canteenId) => Task.FromResult(new CanteenCancellationPolicyDto { CanteenId = canteenId });
        public Task<CanteenCancellationPolicyDto> UpdateCancellationPolicyAsync(int canteenId, UpdateCanteenCancellationPolicyDto dto) => Task.FromResult(new CanteenCancellationPolicyDto { CanteenId = canteenId });

        public Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId) => Task.FromResult(true);


        public Task<bool> IsCanteenActiveAsync(int canteenId) => throw new NotImplementedException();
        public Task<PlatformAdminMetricsDto> GetPlatformMetricsAsync() => throw new NotImplementedException();
        public Task<IEnumerable<AdminCanteenItemDto>> GetAdminCanteensAsync() => throw new NotImplementedException();
        public Task<bool> UpdateCanteenStatusAsync(int canteenId, bool isActive) => throw new NotImplementedException();
        public Task<bool> UpdateCanteenStatusAsync(int canteenId, bool? isActive, Canteen.Model.Enums.TenantStatus? tenantStatus) => throw new NotImplementedException();
    }
}
