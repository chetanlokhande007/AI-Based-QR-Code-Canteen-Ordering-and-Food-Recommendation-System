using System;

namespace Canteen.Model.DTOs;

public class PaymentDto
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    public string RazorpayOrderId { get; set; } = string.Empty;

    public string? RazorpayPaymentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? FailureReason { get; set; }
}
