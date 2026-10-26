using System;

namespace Canteen.Model.Entities;

public class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    public string RazorpayOrderId { get; set; } = string.Empty;

    public string? RazorpayPaymentId { get; set; }

    public string? RazorpaySignature { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public string? FailureReason { get; set; }

    // Navigation property
    public Order Order { get; set; } = null!;
}
