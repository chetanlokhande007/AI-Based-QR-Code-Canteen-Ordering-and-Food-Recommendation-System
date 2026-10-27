using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Creates a Razorpay payment order for the specified canteen order using the server-calculated amount.
    /// </summary>
    Task<PaymentOrderResponseDto> CreatePaymentOrderAsync(int orderId);

    /// <summary>
    /// Verifies the HMAC-SHA256 signature returned by Razorpay Checkout upon successful payment.
    /// </summary>
    Task<PaymentDto> VerifyPaymentAsync(VerifyPaymentDto dto);

    /// <summary>
    /// Processes a Razorpay webhook event, validating the HMAC signature and ensuring idempotent execution.
    /// </summary>
    Task<bool> ProcessWebhookAsync(string rawBody, string signature);

    /// <summary>
    /// Retrieves payment details for a specific order within a canteen.
    /// </summary>
    Task<PaymentDto?> GetPaymentByOrderIdAsync(int canteenId, int orderId);
}
