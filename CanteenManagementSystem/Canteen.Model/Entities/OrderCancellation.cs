using System;

namespace Canteen.Model.Entities;

public class OrderCancellation
{
    public int CancellationId { get; set; }

    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    // "Customer", "CanteenOwner", "PlatformAdmin"
    public string InitiatedByRole { get; set; } = "Customer";

    // Null for customer self-cancellation; OwnerId for staff/admin
    public int? ActorOwnerId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string PreviousOrderStatus { get; set; } = string.Empty;

    public decimal FoodRefundAmount { get; set; }

    public decimal PlatformFeeRefundAmount { get; set; }

    public decimal TotalRefundAmount { get; set; }

    public bool StockRestored { get; set; }

    public int PlatformPolicyVersion { get; set; }

    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Order Order { get; set; } = null!;

    public Canteen Canteen { get; set; } = null!;
}

