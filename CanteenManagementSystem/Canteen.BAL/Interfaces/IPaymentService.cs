using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Creates a Razorpay payment order using the server-calculated order amount.
    /// </summary>
    Task<PaymentOrderResponseDto> CreatePaymentOrderAsync(int orderId);

    /// <summary>
    /// Verifies the Razorpay signature and confirms the captured payment.
    /// </summary>
    Task<PaymentDto> VerifyPaymentAsync(VerifyPaymentDto dto);

    /// <summary>
    /// Processes Razorpay webhook events with signature validation and idempotency.
    /// </summary>
    Task<bool> ProcessWebhookAsync(string rawBody, string signature);

    /// <summary>
    /// Retrieves payment details for an order within a specific canteen.
    /// </summary>
    Task<PaymentDto?> GetPaymentByOrderIdAsync(int canteenId, int orderId);

    /// <summary>
    /// Creates or reuses a pending Pay at Counter payment.
    /// </summary>
    Task<PaymentDto> CreateCounterPaymentAsync(int orderId);

    /// <summary>
    /// Confirms that a pending counter payment has been collected.
    /// </summary>
    Task<PaymentDto> ConfirmCounterPaymentAsync(int canteenId, int orderId);
}
