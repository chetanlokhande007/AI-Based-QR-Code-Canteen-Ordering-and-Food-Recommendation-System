
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Canteen.BAL.Services;

public class MockPaymentService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderNotificationService? _notificationService;
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(
        IConfiguration configuration,
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        ILogger<MockPaymentService> logger)
        : this(configuration, orderRepository, paymentRepository, null, logger)
    {
    }

    public MockPaymentService(
        IConfiguration configuration,
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IOrderNotificationService? notificationService,
        ILogger<MockPaymentService> logger)
    {
        _configuration = configuration;
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    // Create Razorpay payment order
    public async Task<PaymentOrderResponseDto> CreatePaymentOrderAsync(int orderId)
    {
        var keyId = _configuration["Razorpay:KeyId"] ?? "rzp_test_mock_key_id";

        var order = await _orderRepository.GetByIdAsync(orderId, asNoTracking: false);

        if (order == null)
            throw new KeyNotFoundException($"Order with ID {orderId} was not found.");

        if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot initiate payment for a cancelled order.");

        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This order has already been paid.");

        var existingPayment = await _paymentRepository.GetByOrderIdAsync(
            order.CanteenId,
            order.OrderId);

        if (existingPayment != null)
        {
            if (string.Equals(existingPayment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This order has already been paid.");

            if (string.Equals(existingPayment.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(existingPayment.PaymentMethod, "Counter", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A counter payment is already pending for this order.");

                if (!string.IsNullOrWhiteSpace(existingPayment.RazorpayOrderId))
                {
                    return new PaymentOrderResponseDto
                    {
                        OrderId = order.OrderId,
                        OrderNumber = order.OrderNumber,
                        RazorpayOrderId = existingPayment.RazorpayOrderId,
                        Amount = existingPayment.Amount,
                        AmountInPaise = (long)Math.Round(
                            existingPayment.Amount * 100,
                            MidpointRounding.AwayFromZero),
                        Currency = existingPayment.Currency,
                        KeyId = keyId
                    };
                }
            }
        }

        var amountInPaise = (long)Math.Round(
            order.TotalAmount * 100,
            MidpointRounding.AwayFromZero);

        if (amountInPaise <= 0)
            throw new InvalidOperationException("Order total amount must be greater than zero.");

        var mockRazorpayOrderId = $"order_mock_{order.OrderId}_{Guid.NewGuid():N}";

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = order.CanteenId,
            RazorpayOrderId = mockRazorpayOrderId,
            Amount = order.TotalAmount,
            Currency = "INR",
            PaymentMethod = "Online",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _paymentRepository.AddAsync(payment);

        return new PaymentOrderResponseDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            RazorpayOrderId = mockRazorpayOrderId,
            Amount = order.TotalAmount,
            AmountInPaise = amountInPaise,
            Currency = "INR",
            KeyId = keyId
        };
    }

    // Create or reuse Pay at Counter payment
    public async Task<PaymentDto> CreateCounterPaymentAsync(int orderId)
    {
        var order = await _orderRepository.GetByIdAsync(
            orderId,
            asNoTracking: false);

        if (order == null)
            throw new KeyNotFoundException($"Order with ID {orderId} was not found.");

        if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot create payment for a cancelled order.");

        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This order has already been paid.");

        if (order.TotalAmount <= 0)
            throw new InvalidOperationException("Order total amount must be greater than zero.");

        var existingPayment = await _paymentRepository.GetByOrderIdAsync(
            order.CanteenId,
            order.OrderId);

        if (existingPayment != null)
        {
            if (string.Equals(existingPayment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This order has already been paid.");

            if (string.Equals(existingPayment.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(existingPayment.PaymentMethod, "Counter", StringComparison.OrdinalIgnoreCase))
                    return MapToDto(existingPayment);

                if (!string.IsNullOrWhiteSpace(existingPayment.RazorpayOrderId))
                    throw new InvalidOperationException(
                        "An online payment is already pending. Complete or resolve that payment before choosing Pay at Counter.");

                throw new InvalidOperationException(
                    "Another pending payment already exists for this order.");
            }

            // Reuse a previous failed payment record.
            existingPayment.RazorpayOrderId = null;
            existingPayment.RazorpayPaymentId = null;
            existingPayment.RazorpaySignature = null;
            existingPayment.PaymentMethod = "Counter";
            existingPayment.Status = "Pending";
            existingPayment.Amount = order.TotalAmount;
            existingPayment.Currency = "INR";
            existingPayment.CreatedAt = DateTime.UtcNow;
            existingPayment.PaidAt = null;
            existingPayment.FailureReason = null;

            var updatedPayment = await _paymentRepository.UpdateAsync(existingPayment);

            return MapToDto(updatedPayment);
        }

        var counterPayment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = order.CanteenId,
            RazorpayOrderId = null,
            RazorpayPaymentId = null,
            RazorpaySignature = null,
            Amount = order.TotalAmount,
            Currency = "INR",
            PaymentMethod = "Counter",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        var createdPayment = await _paymentRepository.AddAsync(counterPayment);

        return MapToDto(createdPayment);
    }


    /// <summary>
    /// Confirms that a pending counter payment has been collected.
    /// </summary>
    public async Task<PaymentDto> ConfirmCounterPaymentAsync(
        int canteenId,
        int orderId)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(
            canteenId,
            orderId);

        if (payment == null)
            throw new KeyNotFoundException("Payment record not found.");

        if (!string.Equals(
            payment.PaymentMethod,
            "Counter",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "This order does not have a counter payment.");
        }

        if (string.Equals(
            payment.Status,
            "Paid",
            StringComparison.OrdinalIgnoreCase))
        {
            return MapToDto(payment);
        }

        if (!string.Equals(
            payment.Status,
            "Pending",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only pending counter payments can be confirmed.");
        }

        var order = await _orderRepository.GetByIdAsync(
            orderId,
            asNoTracking: false);

        if (order == null || order.CanteenId != canteenId)
            throw new KeyNotFoundException("Order not found.");

        if (string.Equals(
            order.Status,
            "Cancelled",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cannot confirm payment for a cancelled order.");
        }

        payment.Status = "Paid";
        payment.PaidAt = DateTime.UtcNow;
        payment.FailureReason = null;

        order.PaymentStatus = "Paid";

        if (string.Equals(
            order.Status,
            "Pending",
            StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Confirmed";
        }

        await _paymentRepository.UpdatePaymentAndOrderAsync(
            payment,
            order);

        if (_notificationService != null)
        {
            await _notificationService.NotifyPaymentConfirmedAsync(
                payment.CanteenId,
                payment.OrderId,
                order.OrderNumber,
                payment.Amount,
                payment.PaidAt.Value);
        }

        return MapToDto(payment);
    }

    // Verify Razorpay payment
    public async Task<PaymentDto> VerifyPaymentAsync(VerifyPaymentDto dto)
    {
        var payment = await _paymentRepository.GetByRazorpayOrderIdAsync(
            dto.RazorpayOrderId);

        if (payment == null || payment.OrderId != dto.OrderId)
            throw new PaymentVerificationException(
                "No matching payment record found for this Razorpay order.");

        if (string.Equals(payment.PaymentMethod, "Counter", StringComparison.OrdinalIgnoreCase))
            throw new PaymentVerificationException(
                "Counter payments cannot be verified through Razorpay.");

        if (string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return MapToDto(payment);

        var keySecret = _configuration["Razorpay:KeySecret"] ?? "mock_secret";

        // Mock signature is intended for local testing only.
        bool isValid =
            dto.RazorpaySignature.Equals(
                "mock_signature_valid",
                StringComparison.OrdinalIgnoreCase)
            ||
            VerifyHmacSha256(
                $"{dto.RazorpayOrderId}|{dto.RazorpayPaymentId}",
                dto.RazorpaySignature,
                keySecret);

        if (!isValid)
        {
            payment.Status = "Failed";
            payment.FailureReason = "Mock HMAC signature verification failed.";

            await _paymentRepository.UpdateAsync(payment);

            throw new PaymentVerificationException(
                "Payment signature verification failed. Possible payload tampering.");
        }

        payment.RazorpayPaymentId = dto.RazorpayPaymentId;
        payment.RazorpaySignature = dto.RazorpaySignature;
        payment.Status = "Paid";
        payment.PaidAt = DateTime.UtcNow;
        payment.FailureReason = null;

        var order = await _orderRepository.GetByIdAsync(
            payment.OrderId,
            asNoTracking: false);

        if (order != null)
        {
            order.PaymentStatus = "Paid";

            if (string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                order.Status = "Confirmed";

            await _paymentRepository.UpdatePaymentAndOrderAsync(payment, order);

            if (_notificationService != null)
            {
                await _notificationService.NotifyPaymentConfirmedAsync(
                    payment.CanteenId,
                    payment.OrderId,
                    order.OrderNumber,
                    payment.Amount,
                    payment.PaidAt ?? DateTime.UtcNow);
            }
        }
        else
        {
            await _paymentRepository.UpdateAsync(payment);
        }

        return MapToDto(payment);
    }

    // Process Razorpay webhook
    public async Task<bool> ProcessWebhookAsync(string rawBody, string signature)
    {
        var webhookSecret = _configuration["Razorpay:WebhookSecret"] ?? "mock_webhook_secret";

        if (string.IsNullOrWhiteSpace(webhookSecret))
            throw new PaymentVerificationException(
                "Webhook signature verification is unavailable due to missing server configuration.");

        if (!signature.Equals("mock_valid_signature", StringComparison.OrdinalIgnoreCase) &&
            !VerifyHmacSha256(rawBody, signature, webhookSecret))
        {
            throw new PaymentVerificationException("Invalid webhook signature.");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        string eventId;

        if (root.TryGetProperty("id", out var idElement) &&
            idElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(idElement.GetString()))
        {
            eventId = idElement.GetString()!;
        }
        else if (root.TryGetProperty("event_id", out var eventIdElement) &&
                 eventIdElement.ValueKind == JsonValueKind.String &&
                 !string.IsNullOrWhiteSpace(eventIdElement.GetString()))
        {
            eventId = eventIdElement.GetString()!;
        }
        else
        {
            using var sha = SHA256.Create();
            eventId = Convert.ToHexString(
                sha.ComputeHash(Encoding.UTF8.GetBytes(rawBody)));
        }

        if (await _paymentRepository.WebhookEventExistsAsync(eventId))
            return true;

        var eventType = root.TryGetProperty("event", out var eventProp)
            ? eventProp.GetString() ?? "unknown"
            : "unknown";

        await _paymentRepository.AddWebhookEventAsync(new PaymentWebhookEvent
        {
            EventId = eventId,
            EventType = eventType,
            ReceivedAt = DateTime.UtcNow,
            Payload = rawBody
        });

        if (eventType.Equals("payment.captured", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("order.paid", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("payload", out var payloadElement) &&
                payloadElement.TryGetProperty("payment", out var paymentElement) &&
                paymentElement.TryGetProperty("entity", out var entityElement))
            {
                var razorpayOrderId = entityElement.TryGetProperty("order_id", out var oProp)
                    ? oProp.GetString()
                    : null;

                var razorpayPaymentId = entityElement.TryGetProperty("id", out var pProp)
                    ? pProp.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(razorpayOrderId))
                {
                    var payment = await _paymentRepository.GetByRazorpayOrderIdAsync(
                        razorpayOrderId);

                    if (payment != null &&
                        !string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    {
                        payment.RazorpayPaymentId = razorpayPaymentId;
                        payment.Status = "Paid";
                        payment.PaidAt = DateTime.UtcNow;

                        var order = await _orderRepository.GetByIdAsync(
                            payment.OrderId,
                            asNoTracking: false);

                        if (order != null)
                        {
                            order.PaymentStatus = "Paid";

                            if (string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                                order.Status = "Confirmed";

                            await _paymentRepository.UpdatePaymentAndOrderAsync(payment, order);

                            if (_notificationService != null)
                            {
                                await _notificationService.NotifyPaymentConfirmedAsync(
                                    payment.CanteenId,
                                    payment.OrderId,
                                    order.OrderNumber,
                                    payment.Amount,
                                    payment.PaidAt ?? DateTime.UtcNow);
                            }
                        }
                        else
                        {
                            await _paymentRepository.UpdateAsync(payment);
                        }
                    }
                }
            }
        }

        return true;
    }

    // Get payment details
    public async Task<PaymentDto?> GetPaymentByOrderIdAsync(int canteenId, int orderId)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(canteenId, orderId);

        return payment == null ? null : MapToDto(payment);
    }

    // Verify HMAC SHA256 signature
    private static bool VerifyHmacSha256(
        string payload,
        string expectedHexSignature,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) ||
            string.IsNullOrWhiteSpace(expectedHexSignature) ||
            string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        var computedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var computedBytes = Encoding.UTF8.GetBytes(computedHex);
        var receivedBytes = Encoding.UTF8.GetBytes(
            expectedHexSignature.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(
            computedBytes,
            receivedBytes);
    }

    // Map Payment entity to DTO
    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto
        {
            PaymentId = payment.PaymentId,
            OrderId = payment.OrderId,
            CanteenId = payment.CanteenId,
            RazorpayOrderId = payment.RazorpayOrderId,
            RazorpayPaymentId = payment.RazorpayPaymentId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            FailureReason = payment.FailureReason
        };
    }
}
