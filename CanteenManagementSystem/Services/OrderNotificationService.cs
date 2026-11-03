using System;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Services;

public class OrderNotificationService : IOrderNotificationService
{
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<OrderNotificationService> _logger;

    public OrderNotificationService(
        IHubContext<OrderHub> hubContext,
        ILogger<OrderNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyNewOrderPlacedAsync(OrderDto order)
    {
        try
        {
            var kitchenGroup = $"canteen_{order.CanteenId}";
            var tvGroup = $"canteen_tv_{order.CanteenId}";

            var payload = new NewOrderPlacedNotificationDto
            {
                OrderId = order.OrderId,
                OrderNumber = order.OrderNumber,
                CanteenId = order.CanteenId,
                TableId = order.TableId,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(i => new OrderItemNotificationDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            await _hubContext.Clients.Group(kitchenGroup).SendAsync("NewOrderPlaced", payload);
            await _hubContext.Clients.Group(tvGroup).SendAsync("NewOrderPlaced", payload);
            _logger.LogInformation("Dispatched 'NewOrderPlaced' notification to kitchen ({KitchenGroup}) and TV ({TvGroup}) for Order {OrderId}.", kitchenGroup, tvGroup, order.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch 'NewOrderPlaced' notification for Order ID {OrderId}.", order.OrderId);
        }
    }

    public async Task NotifyPaymentConfirmedAsync(int canteenId, int orderId, string orderNumber, decimal amount, DateTime paidAt)
    {
        try
        {
            var canteenGroup = $"canteen_{canteenId}";
            var tvGroup = $"canteen_tv_{canteenId}";
            var orderGroup = $"order_{orderId}";
            var payload = new PaymentConfirmedNotificationDto
            {
                OrderId = orderId,
                OrderNumber = orderNumber,
                CanteenId = canteenId,
                Amount = amount,
                PaidAt = paidAt
            };

            // Dispatch to kitchen, TV, and customer tracking groups
            await _hubContext.Clients.Group(canteenGroup).SendAsync("PaymentConfirmed", payload);
            await _hubContext.Clients.Group(tvGroup).SendAsync("PaymentConfirmed", payload);
            await _hubContext.Clients.Group(orderGroup).SendAsync("PaymentConfirmed", payload);

            _logger.LogInformation(
                "Dispatched 'PaymentConfirmed' notification to kitchen ({CanteenGroup}), TV ({TvGroup}) and customer ({OrderGroup}) for Order {OrderId}.",
                canteenGroup,
                tvGroup,
                orderGroup,
                orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch 'PaymentConfirmed' notification for Order ID {OrderId}.", orderId);
        }
    }

    public Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt)
    {
        return NotifyOrderStatusUpdatedAsync(orderId, orderNumber, newStatus, updatedAt, null);
    }

    public async Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt, int? canteenId)
    {
        try
        {
            var groupName = $"order_{orderId}";
            var payload = new OrderStatusUpdatedNotificationDto
            {
                OrderId = orderId,
                OrderNumber = orderNumber,
                NewStatus = newStatus,
                UpdatedAt = updatedAt,
                CanteenId = canteenId ?? 0
            };

            await _hubContext.Clients.Group(groupName).SendAsync("OrderStatusUpdated", payload);

            if (canteenId.HasValue && canteenId.Value > 0)
            {
                var kitchenGroup = $"canteen_{canteenId.Value}";
                var tvGroup = $"canteen_tv_{canteenId.Value}";
                await _hubContext.Clients.Group(kitchenGroup).SendAsync("OrderStatusUpdated", payload);
                await _hubContext.Clients.Group(tvGroup).SendAsync("OrderStatusUpdated", payload);
            }

            await _hubContext.Clients.All.SendAsync("OrderStatusUpdated", payload);

            _logger.LogInformation("Dispatched 'OrderStatusUpdated' notification to group {GroupName} and kitchen monitors for Order {OrderId} (Status: {NewStatus}, CanteenId: {CanteenId}).", groupName, orderId, newStatus, canteenId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch 'OrderStatusUpdated' notification for Order ID {OrderId}.", orderId);
        }
    }

    public async Task NotifyTvAnnouncementAsync(int canteenId, int orderId, string orderNumber, string tokenNumber, string message)
    {
        try
        {
            var tvGroup = $"canteen_tv_{canteenId}";
            var kitchenGroup = $"canteen_{canteenId}";
            var payload = new TvVoiceAnnouncementDto
            {
                CanteenId = canteenId,
                OrderId = orderId,
                OrderNumber = orderNumber,
                TokenNumber = tokenNumber,
                Message = message,
                AnnouncedAt = DateTime.UtcNow
            };

            await _hubContext.Clients.Group(tvGroup).SendAsync("TvVoiceAnnouncement", payload);
            await _hubContext.Clients.Group(kitchenGroup).SendAsync("TvVoiceAnnouncement", payload);

            _logger.LogInformation("Dispatched 'TvVoiceAnnouncement' notification for Token {TokenNumber} to TV ({TvGroup}).", tokenNumber, tvGroup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch 'TvVoiceAnnouncement' for Order ID {OrderId}.", orderId);
        }
    }
}
