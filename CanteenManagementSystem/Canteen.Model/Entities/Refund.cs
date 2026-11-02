using System;

namespace Canteen.Model.Entities;

public class Refund
{
    public int RefundId { get; set; }

    public int OrderId { get; set; }

    public int PaymentId { get; set; }

    public int CanteenId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    // "Online" (Razorpay / Mock) or "CounterCash"
    public string RefundMethod { get; set; } = "Online";

    // "Pending", "Processing", "Succeeded", "Failed"
    public string Status { get; set; } = "Pending";

    // Gateway reference (e.g. rfnd_xxx or mock_rfnd_xxx)
    public string? GatewayRefundId { get; set; }

    public string Reason { get; set; } = string.Empty;

    // "Customer", "CanteenOwner", "PlatformAdmin"
    public string InitiatedByRole { get; set; } = "Customer";

    public int? InitiatedByOwnerId { get; set; }

    public string? FailureMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    // Navigation properties
    public Order Order { get; set; } = null!;

    public Payment Payment { get; set; } = null!;

    public Canteen Canteen { get; set; } = null!;
}

