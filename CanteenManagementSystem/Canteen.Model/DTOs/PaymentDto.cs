
using System;

namespace Canteen.Model.DTOs;

public class PaymentDto
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    public string? RazorpayOrderId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public string PaymentMethod { get; set; } = "Online";

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? FailureReason { get; set; }
}
