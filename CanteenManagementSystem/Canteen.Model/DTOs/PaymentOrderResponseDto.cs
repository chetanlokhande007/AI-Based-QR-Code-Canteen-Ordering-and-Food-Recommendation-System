namespace Canteen.Model.DTOs;

public class PaymentOrderResponseDto
{
    public int OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string RazorpayOrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public long AmountInPaise { get; set; }

    public string Currency { get; set; } = "INR";

    public string KeyId { get; set; } = string.Empty;
}
