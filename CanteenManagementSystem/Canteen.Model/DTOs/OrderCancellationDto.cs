using System;

namespace Canteen.Model.DTOs;

public class OrderCancellationDto
{
    public int CancellationId { get; set; }
    public int OrderId { get; set; }
    public int CanteenId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string InitiatedByRole { get; set; } = string.Empty;
    public int? ActorOwnerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PreviousOrderStatus { get; set; } = string.Empty;
    public decimal FoodRefundAmount { get; set; }
    public decimal PlatformFeeRefundAmount { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public bool StockRestored { get; set; }
    public int PlatformPolicyVersion { get; set; }
    public DateTime CancelledAt { get; set; }
}

public class CancelOrderRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public bool RestoreInventoryStock { get; set; } = true;
}

