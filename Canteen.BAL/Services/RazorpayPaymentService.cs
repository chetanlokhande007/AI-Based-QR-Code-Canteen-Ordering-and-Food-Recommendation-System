using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
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

public class RazorpayPaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILogger<RazorpayPaymentService> _logger;

    public RazorpayPaymentService(
        HttpClient httpClient,
        IConfiguration configuration,
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        ILogger<RazorpayPaymentService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<PaymentOrderResponseDto> CreatePaymentOrderAsync(int orderId)
    {
        var keyId = _configuration["Razorpay:KeyId"];
        var keySecret = _configuration["Razorpay:KeySecret"];

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
        {
            throw new InvalidOperationException("Razorpay credentials are not configured in application configuration.");
        }

        var order = await _orderRepository.GetByIdAsync(orderId, asNoTracking: false);
        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} was not found.");
        }

        if (order.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot initiate payment for a cancelled order.");
        }

        if (order.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This order has already been paid.");
        }

        // Check if a payment record already exists for this order
        var existingPayment = await _paymentRepository.GetByOrderIdAsync(order.CanteenId, order.OrderId);
        if (existingPayment != null)
        {
            if (existingPayment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This order has already been paid.");
            }

            if (existingPayment.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(existingPayment.RazorpayOrderId))
            {
                _logger.LogInformation(
                    "Reusing existing pending Razorpay order {RazorpayOrderId} for Order ID {OrderId}.",
                    existingPayment.RazorpayOrderId,
                    order.OrderId);

                return new PaymentOrderResponseDto
                {
                    OrderId = order.OrderId,
                    OrderNumber = order.OrderNumber,
                    RazorpayOrderId = existingPayment.RazorpayOrderId,
                    Amount = existingPayment.Amount,
                    AmountInPaise = (long)Math.Round(existingPayment.Amount * 100, MidpointRounding.AwayFromZero),
                    Currency = existingPayment.Currency,
                    KeyId = keyId
                };
            }
        }

        // Calculate amount in paise (1 INR = 100 paise) based strictly on backend order total
        var amountInPaise = (long)Math.Round(order.TotalAmount * 100, MidpointRounding.AwayFromZero);
        if (amountInPaise <= 0)
        {
            throw new InvalidOperationException("Order total amount must be greater than zero.");
        }

        var orderPayload = new
        {
            amount = amountInPaise,
            currency = "INR",
            receipt = order.OrderNumber,
            notes = new Dictionary<string, string>
            {
                ["orderId"] = order.OrderId.ToString(),
                ["canteenId"] = order.CanteenId.ToString(),
                ["orderNumber"] = order.OrderNumber
            }
        };

        var requestJson = JsonSerializer.Serialize(orderPayload);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        var authBytes = Encoding.UTF8.GetBytes($"{keyId}:{keySecret}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        var response = await _httpClient.SendAsync(requestMessage);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Razorpay order creation failed for Order ID {OrderId} with HTTP {StatusCode}: {Response}",
                order.OrderId,
                response.StatusCode,
                responseContent);

            throw new InvalidOperationException("Payment gateway failed to initialize the payment order. Please try again or contact canteen staff.");
        }

        using var jsonDoc = JsonDocument.Parse(responseContent);
        if (!jsonDoc.RootElement.TryGetProperty("id", out var idProp) || string.IsNullOrWhiteSpace(idProp.GetString()))
        {
            _logger.LogError(
                "Razorpay response did not contain a valid order ID for Order {OrderId}. Raw response: {Response}",
                order.OrderId,
                responseContent);

            throw new InvalidOperationException("Payment gateway returned an invalid order response. Please try again.");
        }

        var razorpayOrderId = idProp.GetString()!;

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = order.CanteenId,
            RazorpayOrderId = razorpayOrderId,
            Amount = order.TotalAmount,
            Currency = "INR",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _paymentRepository.AddAsync(payment);

        return new PaymentOrderResponseDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            RazorpayOrderId = razorpayOrderId,
            Amount = order.TotalAmount,
            AmountInPaise = amountInPaise,
            Currency = "INR",
            KeyId = keyId
        };
    }

    public async Task<PaymentDto> VerifyPaymentAsync(VerifyPaymentDto dto)
    {
        var keySecret = _configuration["Razorpay:KeySecret"];
        if (string.IsNullOrWhiteSpace(keySecret))
        {
            throw new InvalidOperationException("Razorpay KeySecret is not configured.");
        }

        var payment = await _paymentRepository.GetByRazorpayOrderIdAsync(dto.RazorpayOrderId);
        if (payment == null || payment.OrderId != dto.OrderId)
        {
            throw new PaymentVerificationException("No matching payment record found for this Razorpay order.");
        }

        if (payment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            return MapToDto(payment);
        }

        // Expected signature payload is: "{razorpay_order_id}|{razorpay_payment_id}"
        var payloadToSign = $"{dto.RazorpayOrderId}|{dto.RazorpayPaymentId}";
        var isSignatureValid = VerifyHmacSha256(payloadToSign, dto.RazorpaySignature, keySecret);

        if (!isSignatureValid)
        {
            payment.Status = "Failed";
            payment.FailureReason = "HMAC signature verification failed.";
            await _paymentRepository.UpdateAsync(payment);

            _logger.LogWarning(
                "Payment verification failed for Order ID {OrderId}, Razorpay Order {RazorpayOrderId}.",
                dto.OrderId,
                dto.RazorpayOrderId);

            throw new PaymentVerificationException("Payment signature verification failed. Possible payload tampering.");
        }

        payment.RazorpayPaymentId = dto.RazorpayPaymentId;
        payment.RazorpaySignature = dto.RazorpaySignature;
        payment.Status = "Paid";
        payment.PaidAt = DateTime.UtcNow;
        payment.FailureReason = null;

        // Atomically update both the order's payment/kitchen status and the payment record
        var order = await _orderRepository.GetByIdAsync(payment.OrderId, asNoTracking: false);
        if (order != null)
        {
            order.PaymentStatus = "Paid";

            // Only transition kitchen status if currently Pending (preserve InPreparation, Ready, etc.)
            if (order.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "Confirmed";
            }

            await _paymentRepository.UpdatePaymentAndOrderAsync(payment, order);
        }
        else
        {
            await _paymentRepository.UpdateAsync(payment);
        }

        _logger.LogInformation(
            "Payment verified successfully for Order ID {OrderId}. Razorpay Payment ID: {PaymentId}",
            order?.OrderId,
            dto.RazorpayPaymentId);

        return MapToDto(payment);
    }

    public async Task<bool> ProcessWebhookAsync(string rawBody, string signature)
    {
        var webhookSecret = _configuration["Razorpay:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogError("Razorpay WebhookSecret is not configured. Rejecting webhook request.");
            throw new PaymentVerificationException("Webhook signature verification is unavailable due to missing server configuration.");
        }

        var isSignatureValid = VerifyHmacSha256(rawBody, signature, webhookSecret);
        if (!isSignatureValid)
        {
            _logger.LogWarning("Invalid Razorpay webhook signature received.");
            throw new PaymentVerificationException("Invalid webhook signature.");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        // Extract event ID for idempotency check
        string eventId;
        if (root.TryGetProperty("id", out var idElement) && !string.IsNullOrWhiteSpace(idElement.GetString()))
        {
            eventId = idElement.GetString()!;
        }
        else if (root.TryGetProperty("event_id", out var eventIdElement) && !string.IsNullOrWhiteSpace(eventIdElement.GetString()))
        {
            eventId = eventIdElement.GetString()!;
        }
        else
        {
            // Fallback deterministic event ID from SHA-256 of payload
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            eventId = Convert.ToHexString(hash);
        }

        // Idempotency check: if this event was already processed, ignore safely
        if (await _paymentRepository.WebhookEventExistsAsync(eventId))
        {
            _logger.LogInformation("Webhook event {EventId} has already been processed. Skipping.", eventId);
            return true;
        }

        var eventType = root.TryGetProperty("event", out var eventProp)
            ? eventProp.GetString() ?? "unknown"
            : "unknown";

        // Record webhook event
        await _paymentRepository.AddWebhookEventAsync(new PaymentWebhookEvent
        {
            EventId = eventId,
            EventType = eventType,
            ReceivedAt = DateTime.UtcNow,
            Payload = rawBody
        });

        // Handle specific payment events
        if (eventType.Equals("payment.captured", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("order.paid", StringComparison.OrdinalIgnoreCase))
        {
            await HandlePaymentCapturedEventAsync(root);
        }
        else if (eventType.Equals("payment.failed", StringComparison.OrdinalIgnoreCase))
        {
            await HandlePaymentFailedEventAsync(root);
        }

        return true;
    }

    public async Task<PaymentDto?> GetPaymentByOrderIdAsync(int canteenId, int orderId)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(canteenId, orderId);
        return payment == null ? null : MapToDto(payment);
    }

    private async Task HandlePaymentCapturedEventAsync(JsonElement root)
    {
        if (!root.TryGetProperty("payload", out var payloadElement) ||
            !payloadElement.TryGetProperty("payment", out var paymentElement) ||
            !paymentElement.TryGetProperty("entity", out var entityElement))
        {
            return;
        }

        var razorpayOrderId = entityElement.TryGetProperty("order_id", out var orderIdProp)
            ? orderIdProp.GetString()
            : null;

        var razorpayPaymentId = entityElement.TryGetProperty("id", out var paymentIdProp)
            ? paymentIdProp.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(razorpayOrderId))
        {
            return;
        }

        var payment = await _paymentRepository.GetByRazorpayOrderIdAsync(razorpayOrderId);
        if (payment == null)
        {
            _logger.LogWarning("Webhook payment.captured received for unknown Razorpay Order ID {RazorpayOrderId}", razorpayOrderId);
            return;
        }

        if (!payment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            payment.RazorpayPaymentId = razorpayPaymentId;
            payment.Status = "Paid";
            payment.PaidAt = DateTime.UtcNow;
            payment.FailureReason = null;

            var order = await _orderRepository.GetByIdAsync(payment.OrderId, asNoTracking: false);
            if (order != null)
            {
                order.PaymentStatus = "Paid";
                if (order.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = "Confirmed";
                }

                await _paymentRepository.UpdatePaymentAndOrderAsync(payment, order);
            }
            else
            {
                await _paymentRepository.UpdateAsync(payment);
            }

            _logger.LogInformation("Order ID {OrderId} marked as Paid via webhook event.", payment.OrderId);
        }
    }

    private async Task HandlePaymentFailedEventAsync(JsonElement root)
    {
        if (!root.TryGetProperty("payload", out var payloadElement) ||
            !payloadElement.TryGetProperty("payment", out var paymentElement) ||
            !paymentElement.TryGetProperty("entity", out var entityElement))
        {
            return;
        }

        var razorpayOrderId = entityElement.TryGetProperty("order_id", out var orderIdProp)
            ? orderIdProp.GetString()
            : null;

        var failureDescription = entityElement.TryGetProperty("error_description", out var descProp)
            ? descProp.GetString()
            : "Payment failed";

        if (string.IsNullOrWhiteSpace(razorpayOrderId))
        {
            return;
        }

        var payment = await _paymentRepository.GetByRazorpayOrderIdAsync(razorpayOrderId);
        if (payment != null && !payment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = "Failed";
            payment.FailureReason = failureDescription;
            await _paymentRepository.UpdateAsync(payment);
        }
    }

    private static bool VerifyHmacSha256(string payload, string expectedHexSignature, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(expectedHexSignature) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var computedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var computedBytes = Encoding.UTF8.GetBytes(computedHex);
        var receivedBytes = Encoding.UTF8.GetBytes(expectedHexSignature.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(computedBytes, receivedBytes);
    }

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
            Status = payment.Status,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            FailureReason = payment.FailureReason
        };
    }
}
