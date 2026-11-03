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
    public int CanteenId { get; set; }
}

public class TvVoiceAnnouncementDto
{
    public int CanteenId { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TokenNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime AnnouncedAt { get; set; } = DateTime.UtcNow;
}

public class CanteenTvDisplayDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string StationScope { get; set; } = "All";
    public string DeviceName { get; set; } = string.Empty;
    public List<TvTokenDto> PreparingOrders { get; set; } = new();
    public List<TvTokenDto> ReadyOrders { get; set; } = new();
}

public class TvTokenDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TokenNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ItemCount { get; set; }
}
