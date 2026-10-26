using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs;

public class NewOrderPlacedNotificationDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CanteenId { get; set; }
    public int TableId { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemNotificationDto> Items { get; set; } = new();
}

public class OrderItemNotificationDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}

public class PaymentConfirmedNotificationDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CanteenId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
}

public class OrderStatusUpdatedNotificationDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
