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
}
