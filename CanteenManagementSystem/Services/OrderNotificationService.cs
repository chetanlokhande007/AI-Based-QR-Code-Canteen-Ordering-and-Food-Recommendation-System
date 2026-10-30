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
            var groupName = $"canteen_{order.CanteenId}";
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

            await _hubContext.Clients.Group(groupName).SendAsync("NewOrderPlaced", payload);
            _logger.LogInformation("Dispatched 'NewOrderPlaced' notification to group {GroupName} for Order {OrderId}.", groupName, order.OrderId);
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
            var orderGroup = $"order_{orderId}";
            var payload = new PaymentConfirmedNotificationDto
            {
                OrderId = orderId,
                OrderNumber = orderNumber,
                CanteenId = canteenId,
                Amount = amount,
                PaidAt = paidAt
            };

            // Dispatch to both the canteen kitchen group and the customer order tracking group
            await _hubContext.Clients.Group(canteenGroup).SendAsync("PaymentConfirmed", payload);
            await _hubContext.Clients.Group(orderGroup).SendAsync("PaymentConfirmed", payload);

            _logger.LogInformation(
                "Dispatched 'PaymentConfirmed' notification to kitchen ({CanteenGroup}) and customer ({OrderGroup}) for Order {OrderId}.",
                canteenGroup,
                orderGroup,
                orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch 'PaymentConfirmed' notification for Order ID {OrderId}.", orderId);
        }
    }

    public async Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt)
    {
        try
        {
            var groupName = $"order_{orderId}";
            var payload = new OrderStatusUpdatedNotificationDto
            {
                OrderId = orderId,
                OrderNumber = orderNumber,
                NewStatus = newStatus,
                UpdatedAt = updatedAt
            };

            await _hubContext.Clients.Group(groupName).SendAsync("OrderStatusUpdated", payload);
            _logger.LogInformation("Dispatched 'OrderStatusUpdated' notification to group {GroupName} for Order {OrderId} (Status: {NewStatus}).", groupName, orderId, newStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch 'OrderStatusUpdated' notification for Order ID {OrderId}.", orderId);
        }
    }
}
