using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Enums;
using CanteenManagementSystem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Canteen.Tests;

public class KitchenAndTvDisplayTests
{
    private readonly FakeOrderService _fakeOrderService;
    private readonly FakeCanteenService _fakeCanteenService;
    private readonly FakeNotificationService _fakeNotificationService;
    private readonly OrdersController _controller;

    public KitchenAndTvDisplayTests()
    {
        _fakeOrderService = new FakeOrderService();
        _fakeCanteenService = new FakeCanteenService();
        _fakeNotificationService = new FakeNotificationService();

        _controller = new OrdersController(
            _fakeOrderService,
            _fakeCanteenService,
            _fakeNotificationService);

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
    public async Task GetTvDisplayData_ReturnsPartitionedPreparingAndReadyTokens()
    {
        // Arrange
        int canteenId = 1;
        _fakeCanteenService.Canteen = new CanteenDto { CanteenId = 1, Name = "CT Catten" };

        _fakeOrderService.Orders = new List<OrderDto>
        {
            new() { OrderId = 1, OrderNumber = "ORD-20261103-1001", Status = "InPreparation", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { OrderId = 2, OrderNumber = "ORD-20261103-1002", Status = "Preparing", CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { OrderId = 3, OrderNumber = "ORD-20261103-1003", Status = "Ready", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new() { OrderId = 4, OrderNumber = "ORD-20261103-1004", Status = "Completed", CreatedAt = DateTime.UtcNow.AddMinutes(-15) },
            new() { OrderId = 5, OrderNumber = "ORD-20261103-1005", Status = "Cancelled", CreatedAt = DateTime.UtcNow.AddMinutes(-20) }
        };

        // Act
        var result = await _controller.GetTvDisplayData(canteenId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CanteenTvDisplayDto>(okResult.Value);

        Assert.Equal("CT Catten", dto.CanteenName);
        Assert.Equal(2, dto.PreparingOrders.Count);
        Assert.Single(dto.ReadyOrders);

        // Token extraction: last segment of ORD-YYYYMMDD-XXXX
        Assert.Equal("1001", dto.PreparingOrders[0].TokenNumber);
        Assert.Equal("1002", dto.PreparingOrders[1].TokenNumber);
        Assert.Equal("1003", dto.ReadyOrders[0].TokenNumber);
    }

    [Fact]
    public async Task GetTvDisplayData_ReturnsNotFound_WhenCanteenDoesNotExist()
    {
        // Arrange
        _fakeCanteenService.Canteen = null;

        // Act
        var result = await _controller.GetTvDisplayData(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AnnounceOrderOnTv_DispatchesVoiceNotification_WhenAuthorized()
    {
        // Arrange
        int canteenId = 1;
        int orderId = 42;

        _fakeCanteenService.IsAuthorized = true;
        _fakeOrderService.SingleOrder = new OrderDto
        {
            OrderId = 42,
            CanteenId = 1,
            OrderNumber = "ORD-20261103-8899",
            Status = "Ready"
        };

        // Act
        var result = await _controller.AnnounceOrderOnTv(canteenId, orderId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Single(_fakeNotificationService.TvAnnouncements);
        var ann = _fakeNotificationService.TvAnnouncements[0];
        Assert.Equal(1, ann.CanteenId);
        Assert.Equal(42, ann.OrderId);
        Assert.Equal("8899", ann.TokenNumber);
        Assert.Contains("8899", ann.Message);
    }

    [Fact]
    public async Task AnnounceOrderOnTv_ReturnsForbid_WhenOwnerNotAuthorizedForCanteen()
    {
        // Arrange
        int canteenId = 2; // Different canteen than claims (1)
        int orderId = 42;

        // Act
        var result = await _controller.AnnounceOrderOnTv(canteenId, orderId);

        // Assert
        Assert.IsType<ForbidResult>(result);
        Assert.Empty(_fakeNotificationService.TvAnnouncements);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fake Test Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private class FakeOrderService : IOrderService
    {
        public List<OrderDto> Orders { get; set; } = new();
        public OrderDto? SingleOrder { get; set; }

        public Task<OrderDto> CreateAsync(CreateOrderDto dto, string qrToken) => throw new NotImplementedException();
        public Task<OrderDto?> GetByIdAsync(int canteenId, int orderId) => Task.FromResult(SingleOrder);
        public Task<OrderDto?> GetByTrackingTokenAsync(string trackingToken) => Task.FromResult(SingleOrder);
        public Task<IEnumerable<OrderDto>> GetAllByCanteenAsync(int canteenId) => Task.FromResult<IEnumerable<OrderDto>>(Orders);
        public Task<OrderDto?> UpdateStatusAsync(int canteenId, int orderId, string newStatus) => Task.FromResult(SingleOrder);
        public Task<OrderDto?> CancelOrderByTrackingTokenAsync(string trackingToken, string? reason = null) => Task.FromResult(SingleOrder);
        public Task<OrderDto?> CancelOrderFromCanteenAsync(int canteenId, int orderId, string reason, int ownerId) => Task.FromResult(SingleOrder);
        public Task<OrderCancellationDto?> GetCancellationByOrderIdAsync(int canteenId, int orderId) => Task.FromResult<OrderCancellationDto?>(null);
        public Task<IEnumerable<OrderCancellationDto>> GetCancellationsByCanteenAsync(int canteenId) => Task.FromResult<IEnumerable<OrderCancellationDto>>(new List<OrderCancellationDto>());
    }

    private class FakeCanteenService : ICanteenService
    {
        public CanteenDto? Canteen { get; set; }
        public bool IsAuthorized { get; set; } = true;

        public Task<CanteenDto?> GetByIdAsync(int id) => Task.FromResult(Canteen);
        public Task<bool> IsOwnerAuthorizedAndActiveAsync(int ownerId, int canteenId) => Task.FromResult(IsAuthorized);
        public Task<IEnumerable<CanteenDto>> GetAllAsync() => Task.FromResult<IEnumerable<CanteenDto>>(new List<CanteenDto>());
        public Task<CanteenDto?> GetBySlugAsync(string slug) => Task.FromResult<CanteenDto?>(null);
        public Task<CanteenDto> CreateAsync(CreateCanteenDto dto, int? ownerId = null) => throw new NotImplementedException();
        public Task<CanteenDto?> UpdateAsync(int id, UpdateCanteenDto dto) => Task.FromResult<CanteenDto?>(null);
        public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
        public Task<IEnumerable<CanteenSummaryDto>> GetOwnerCanteensAsync(int ownerId) => Task.FromResult<IEnumerable<CanteenSummaryDto>>(new List<CanteenSummaryDto>());
        public Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId) => Task.FromResult(IsAuthorized);
        public Task<bool> IsCanteenActiveAsync(int canteenId) => Task.FromResult(true);
        public Task<PlatformAdminMetricsDto> GetPlatformMetricsAsync() => Task.FromResult(new PlatformAdminMetricsDto());
        public Task<IEnumerable<AdminCanteenItemDto>> GetAdminCanteensAsync() => Task.FromResult<IEnumerable<AdminCanteenItemDto>>(new List<AdminCanteenItemDto>());
        public Task<bool> UpdateCanteenStatusAsync(int canteenId, bool isActive) => Task.FromResult(true);
        public Task<bool> UpdateCanteenStatusAsync(int canteenId, bool? isActive, TenantStatus? tenantStatus) => Task.FromResult(true);
    }

    private class FakeNotificationService : IOrderNotificationService
    {
        public List<(int CanteenId, int OrderId, string OrderNumber, string TokenNumber, string Message)> TvAnnouncements { get; } = new();

        public Task NotifyNewOrderPlacedAsync(OrderDto order) => Task.CompletedTask;
        public Task NotifyPaymentConfirmedAsync(int canteenId, int orderId, string orderNumber, decimal amount, DateTime paidAt) => Task.CompletedTask;
        public Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt) => Task.CompletedTask;
        public Task NotifyTvAnnouncementAsync(int canteenId, int orderId, string orderNumber, string tokenNumber, string message)
        {
            TvAnnouncements.Add((canteenId, orderId, orderNumber, tokenNumber, message));
            return Task.CompletedTask;
        }
    }
}
