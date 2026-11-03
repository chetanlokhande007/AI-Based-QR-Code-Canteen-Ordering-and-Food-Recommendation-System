using System;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IOrderNotificationService
{
    /// <summary>
    /// Notifies the kitchen staff group for a canteen when a new order is successfully placed and persisted.
    /// </summary>
    Task NotifyNewOrderPlacedAsync(OrderDto order);

    /// <summary>
    /// Notifies the kitchen staff group for a canteen when payment is verified and confirmed.
    /// </summary>
    Task NotifyPaymentConfirmedAsync(int canteenId, int orderId, string orderNumber, decimal amount, DateTime paidAt);

    /// <summary>
    /// Notifies the customer tracking group when the kitchen advances the order status (e.g. InPreparation, Ready).
    /// </summary>
    Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt);

    /// <summary>
    /// Notifies customer tracking and canteen kitchen/TV monitors when the order status changes, scoped by CanteenId.
    /// </summary>
    Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt, int? canteenId)
        => NotifyOrderStatusUpdatedAsync(orderId, orderNumber, newStatus, updatedAt);

    /// <summary>
    /// Dispatches a voice or visual announcement to the dining hall TV displays for a specific canteen.
    /// </summary>
    Task NotifyTvAnnouncementAsync(int canteenId, int orderId, string orderNumber, string tokenNumber, string message)
        => Task.CompletedTask;
}
