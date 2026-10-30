
using System;

namespace Canteen.Model.Entities;

public class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    // Nullable because counter payments do not have a Razorpay order.
    public string? RazorpayOrderId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public string? RazorpaySignature { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    // Online or Counter
    public string PaymentMethod { get; set; } = "Online";

    // Pending, Paid, Failed, Refunded
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public string? FailureReason { get; set; }

    public Order Order { get; set; } = null!;
}
