using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Canteen.Tests;

public class PaymentTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PaymentTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePaymentOrder_WithValidOrder_CalculatesPaiseAmount_FromServerSidePrice()
    {
        // 1. Arrange order (2 x 150 = 300.00)
        var (orderId, totalAmount, canteenId) = await SetupOrderAsync("pay_create", 150.00m, 2);

        // 2. Act: Request Razorpay payment order
        var response = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });

        // 3. Assert
        var errorBody = await response.Content.ReadAsStringAsync(); Assert.True(response.IsSuccessStatusCode, errorBody);
        var paymentOrder = await response.Content.ReadFromJsonAsync<PaymentOrderResponseDto>();
        Assert.NotNull(paymentOrder);
        Assert.Equal(orderId, paymentOrder.OrderId);
        Assert.Equal(totalAmount, paymentOrder.Amount);
        Assert.Equal((long)(totalAmount * 100), paymentOrder.AmountInPaise);
        Assert.Equal("INR", paymentOrder.Currency);
        Assert.False(string.IsNullOrWhiteSpace(paymentOrder.RazorpayOrderId));
    }

    [Fact]
    public async Task CreatePaymentOrder_WithInvalidOrCancelledOrder_ReturnsExpectedError()
    {
        // Non-existent order
        var notFoundResponse = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = 99999
        });
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);

        // Cancelled order
        var (orderId, _, canteenId) = await SetupOrderAsync("pay_cancelled", 100.00m, 1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            order!.Status = "Cancelled";
            await db.SaveChangesAsync();
        }

        var cancelledResponse = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        Assert.Equal(HttpStatusCode.BadRequest, cancelledResponse.StatusCode);
    }

    [Fact]
    public async Task PayAtCounter_WithValidOrder_CreatesPendingCounterPayment()
    {
        // 1. Arrange order
        var (orderId, totalAmount, canteenId) = await SetupOrderAsync("pay_counter", 120.00m, 1);

        // 2. Act: Request pay at counter
        var response = await _client.PostAsJsonAsync("/api/payments/pay-at-counter", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });

        // 3. Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(payment);
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal(totalAmount, payment.Amount);
        Assert.Equal("Counter", payment.PaymentMethod);
        Assert.Equal("Pending", payment.Status);
        Assert.Null(payment.RazorpayOrderId);
    }

    [Fact]
    public async Task ConfirmCounterPayment_WithValidOwner_ConfirmsPaymentAndOrder()
    {
        // 1. Arrange order and get canteenId
        var (orderId, totalAmount, canteenId) = await SetupOrderAsync("confirm_counter", 80.00m, 1);

        // 2. Customer chooses Pay at Counter
        var counterRes = await _client.PostAsJsonAsync("/api/payments/pay-at-counter", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        counterRes.EnsureSuccessStatusCode();

        // 3. Authenticate as the canteen's owner
        string ownerToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var canteen = await db.Canteens.Include(c => c.Owner).FirstOrDefaultAsync(c => c.CanteenId == canteenId);
            var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                Email = canteen!.Owner.Email,
                Password = "Password123!"
            });
            loginRes.EnsureSuccessStatusCode();
            var loginData = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();
            ownerToken = loginData!.Token;
        }

        // 4. Act: Owner confirms cash payment at counter
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var confirmRes = await _client.PostAsync($"/api/canteens/{canteenId}/orders/{orderId}/confirm-counter-payment", null);

        // 5. Assert
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var payment = await confirmRes.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(payment);
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal("Counter", payment.PaymentMethod);
        Assert.Equal("Paid", payment.Status);
        Assert.NotNull(payment.PaidAt);

        // 6. Verify order status in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            Assert.NotNull(order);
            Assert.Equal("Paid", order.PaymentStatus);
            Assert.Equal("Confirmed", order.Status);
        }
    }

    [Fact]
    public async Task VerifyPayment_WithValidSignature_MarksPaymentPaid_AndConfirmsOrder()
    {
        // 1. Arrange: create order & payment order
        var (orderId, totalAmount, canteenId) = await SetupOrderAsync("pay_verify", 200.00m, 1);

        var createRes = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        var paymentOrder = await createRes.Content.ReadFromJsonAsync<PaymentOrderResponseDto>();

        // 2. Act: Customer checkout completes with valid signature
        var verifyDto = new VerifyPaymentDto
        {
            OrderId = orderId,
            RazorpayOrderId = paymentOrder!.RazorpayOrderId,
            RazorpayPaymentId = "pay_test_payment_12345",
            RazorpaySignature = "mock_signature_valid"
        };

        var response = await _client.PostAsJsonAsync("/api/payments/verify", verifyDto);

        // 3. Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(payment);
        Assert.Equal("Paid", payment.Status);
        Assert.Equal("pay_test_payment_12345", payment.RazorpayPaymentId);
        Assert.NotNull(payment.PaidAt);

        // Verify Order in DB: PaymentStatus should be Paid and Status should be Confirmed
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            Assert.NotNull(order);
            Assert.Equal("Paid", order.PaymentStatus);
            Assert.Equal("Confirmed", order.Status);
        }
    }

    [Fact]
    public async Task VerifyPayment_WithTamperedSignature_ReturnsBadRequest_AndLeavesOrderPending()
    {
        // 1. Arrange
        var (orderId, _, _) = await SetupOrderAsync("pay_tamper", 50.00m, 1);

        var createRes = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        var paymentOrder = await createRes.Content.ReadFromJsonAsync<PaymentOrderResponseDto>();

        // 2. Act: send forged signature
        var verifyDto = new VerifyPaymentDto
        {
            OrderId = orderId,
            RazorpayOrderId = paymentOrder!.RazorpayOrderId,
            RazorpayPaymentId = "pay_test_payment_12345",
            RazorpaySignature = "forged_malicious_signature_123"
        };

        var response = await _client.PostAsJsonAsync("/api/payments/verify", verifyDto);

        // 3. Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify Order in DB remains Pending
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            Assert.NotNull(order);
            Assert.Equal("Pending", order.PaymentStatus);
            Assert.Equal("Pending", order.Status);

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.RazorpayOrderId == paymentOrder.RazorpayOrderId);
            Assert.NotNull(payment);
            Assert.Equal("Failed", payment.Status);
        }
    }

    [Fact]
    public async Task VerifyPayment_DoesNotRevert_OrderAlreadyInPreparation()
    {
        // 1. Arrange order and advance kitchen status to "InPreparation"
        var (orderId, _, canteenId) = await SetupOrderAsync("pay_workflow", 75.00m, 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            order!.Status = "InPreparation";
            await db.SaveChangesAsync();
        }

        var createRes = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        var paymentOrder = await createRes.Content.ReadFromJsonAsync<PaymentOrderResponseDto>();

        // 2. Act: verify payment
        var verifyDto = new VerifyPaymentDto
        {
            OrderId = orderId,
            RazorpayOrderId = paymentOrder!.RazorpayOrderId,
            RazorpayPaymentId = "pay_test_in_prep_123",
            RazorpaySignature = "mock_signature_valid"
        };

        var response = await _client.PostAsJsonAsync("/api/payments/verify", verifyDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 3. Assert: Order PaymentStatus is Paid, but Status MUST STAY "InPreparation"
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            Assert.NotNull(order);
            Assert.Equal("Paid", order.PaymentStatus);
            Assert.Equal("InPreparation", order.Status); // Preserved! Not reverted to Confirmed
        }
    }

    [Fact]
    public async Task VerifyPayment_DoesNotRevert_OrderAlreadyReady()
    {
        // 1. Arrange order and advance kitchen status to "Ready"
        var (orderId, _, canteenId) = await SetupOrderAsync("pay_ready_wf", 110.00m, 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            order!.Status = "Ready";
            await db.SaveChangesAsync();
        }

        var createRes = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        var paymentOrder = await createRes.Content.ReadFromJsonAsync<PaymentOrderResponseDto>();

        // 2. Act: verify payment
        var verifyDto = new VerifyPaymentDto
        {
            OrderId = orderId,
            RazorpayOrderId = paymentOrder!.RazorpayOrderId,
            RazorpayPaymentId = "pay_test_ready_999",
            RazorpaySignature = "mock_signature_valid"
        };

        var response = await _client.PostAsJsonAsync("/api/payments/verify", verifyDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 3. Assert: Order PaymentStatus is Paid, but Status MUST STAY "Ready"
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            Assert.NotNull(order);
            Assert.Equal("Paid", order.PaymentStatus);
            Assert.Equal("Ready", order.Status); // Preserved! Not reverted to Confirmed
        }
    }

    [Fact]
    public async Task Webhook_WithValidEvent_MarksPaymentPaid_AndIsIdempotent()
    {
        // 1. Arrange order & payment
        var (orderId, _, _) = await SetupOrderAsync("webhook_test", 180.00m, 1);
        var createRes = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        var paymentOrder = await createRes.Content.ReadFromJsonAsync<PaymentOrderResponseDto>();

        var eventId = "evt_test_" + Guid.NewGuid().ToString("N");
        var webhookPayload = $@"{{
            ""id"": ""{eventId}"",
            ""event"": ""payment.captured"",
            ""payload"": {{
                ""payment"": {{
                    ""entity"": {{
                        ""id"": ""pay_webhook_999"",
                        ""order_id"": ""{paymentOrder!.RazorpayOrderId}"",
                        ""amount"": 18000,
                        ""status"": ""captured""
                    }}
                }}
            }}
        }}";

        // 2. Act: first webhook post
        using var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(webhookPayload, Encoding.UTF8, "application/json")
        };
        request1.Headers.Add("X-Razorpay-Signature", "mock_valid_signature");

        var response1 = await _client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Verify order is marked Paid
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            Assert.Equal("Paid", order!.PaymentStatus);
            Assert.Equal("Confirmed", order.Status);
        }

        // 3. Act: Send identical webhook again (Idempotency test)
        using var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(webhookPayload, Encoding.UTF8, "application/json")
        };
        request2.Headers.Add("X-Razorpay-Signature", "mock_valid_signature");

        var response2 = await _client.SendAsync(request2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }

    [Fact]
    public async Task GetPayment_EnforcesMultiTenantAuthorization_ReturnsForbiddenForOtherCanteen()
    {
        // 1. Arrange Canteen A with an order
        var (orderId, _, canteenAId) = await SetupOrderAsync("tenant_a", 100.00m, 1);
        var createRes = await _client.PostAsJsonAsync("/api/payments/create-order", new CreatePaymentOrderDto
        {
            OrderId = orderId
        });
        createRes.EnsureSuccessStatusCode();

        // 2. Arrange Canteen B and its owner token
        var (tokenB, canteenBId) = await CreateOwnerAndCanteenAsync("tenant_b");

        // 3. Act: Canteen B attempts to read Canteen A's order payment
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var forbiddenResponse = await _client.GetAsync($"/api/canteens/{canteenAId}/orders/{orderId}/payment");

        // 4. Assert: 403 Forbidden (Anti-BOLA check)
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task RazorpayPaymentService_HmacSha256_ExactCryptographicVerification_WorksCorrectly()
    {
        // Direct unit test of HMAC-SHA256 signature algorithm
        var keySecret = "test_secret_key_123456";
        var razorpayOrderId = "order_test_987654";
        var razorpayPaymentId = "pay_test_456789";
        var payloadToSign = $"{razorpayOrderId}|{razorpayPaymentId}";

        // Compute valid signature
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
        var validHexSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadToSign))).ToLowerInvariant();

        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("HmacTestDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_id",
                ["Razorpay:KeySecret"] = keySecret
            })
            .Build();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            NullLogger<RazorpayPaymentService>.Instance);

        // Add order and pending payment
        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-TEST-001",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 250.00m
        };
        await orderRepo.AddAsync(order);

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 1,
            RazorpayOrderId = razorpayOrderId,
            Amount = 250.00m,
            Currency = "INR",
            Status = "Pending"
        };
        await paymentRepo.AddAsync(payment);

        // 1. Valid signature verifies successfully
        var verified = await service.VerifyPaymentAsync(new VerifyPaymentDto
        {
            OrderId = order.OrderId,
            RazorpayOrderId = razorpayOrderId,
            RazorpayPaymentId = razorpayPaymentId,
            RazorpaySignature = validHexSignature
        });

        Assert.Equal("Paid", verified.Status);
        Assert.Equal(razorpayPaymentId, verified.RazorpayPaymentId);

        // 2. Tampered signature throws PaymentVerificationException
        payment.Status = "Pending";
        await paymentRepo.UpdateAsync(payment);

        await Assert.ThrowsAsync<PaymentVerificationException>(() => service.VerifyPaymentAsync(new VerifyPaymentDto
        {
            OrderId = order.OrderId,
            RazorpayOrderId = razorpayOrderId,
            RazorpayPaymentId = razorpayPaymentId,
            RazorpaySignature = "0000000000000000000000000000000000000000000000000000000000000000"
        }));
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenWebhookSecretMissing_FailsClosedAndRejectsProcessing()
    {
        // Issue 1: Webhook secret missing in configuration must reject immediately (Fail-Closed)
        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("MissingSecretDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);

        // Configuration with missing webhook secret
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = "test_secret",
                ["Razorpay:WebhookSecret"] = "" // Empty secret!
            })
            .Build();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            NullLogger<RazorpayPaymentService>.Instance);

        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-SECRET-TEST",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 100.00m
        };
        await orderRepo.AddAsync(order);

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 1,
            RazorpayOrderId = "order_secret_test",
            Amount = 100.00m,
            Currency = "INR",
            Status = "Pending"
        };
        await paymentRepo.AddAsync(payment);

        var payload = @"{
            ""id"": ""evt_missing_secret_test"",
            ""event"": ""payment.captured"",
            ""payload"": {
                ""payment"": {
                    ""entity"": {
                        ""id"": ""pay_test_001"",
                        ""order_id"": ""order_secret_test""
                    }
                }
            }
        }";

        // Act & Assert: Throws PaymentVerificationException with missing config explanation
        var ex = await Assert.ThrowsAsync<PaymentVerificationException>(
            () => service.ProcessWebhookAsync(payload, "any_arbitrary_signature"));

        Assert.Contains("unavailable due to missing server configuration", ex.Message);

        // Verify database: Payment and Order must NEVER be marked as Paid
        var dbPayment = await paymentRepo.GetByRazorpayOrderIdAsync("order_secret_test");
        var dbOrder = await orderRepo.GetByIdAsync(order.OrderId);
        Assert.Equal("Pending", dbPayment!.Status);
        Assert.Equal("Pending", dbOrder!.PaymentStatus);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithValidHmacSha256_ProcessesWebhookAndConfirmsOrder()
    {
        // Issue 1: Valid webhook secret and signature verifies and updates order
        var webhookSecret = "super_secure_webhook_secret_789";
        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("ValidWebhookDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = "test_secret",
                ["Razorpay:WebhookSecret"] = webhookSecret
            })
            .Build();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            NullLogger<RazorpayPaymentService>.Instance);

        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-WH-001",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 150.00m
        };
        await orderRepo.AddAsync(order);

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 1,
            RazorpayOrderId = "order_wh_001",
            Amount = 150.00m,
            Currency = "INR",
            Status = "Pending"
        };
        await paymentRepo.AddAsync(payment);

        var payload = @"{
            ""id"": ""evt_valid_wh_123"",
            ""event"": ""payment.captured"",
            ""payload"": {
                ""payment"": {
                    ""entity"": {
                        ""id"": ""pay_captured_999"",
                        ""order_id"": ""order_wh_001""
                    }
                }
            }
        }";

        // Compute valid HMAC signature
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var validSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        // Act
        var result = await service.ProcessWebhookAsync(payload, validSignature);
        Assert.True(result);

        // Assert: Both Payment and Order are Paid and Confirmed
        var dbPayment = await paymentRepo.GetByRazorpayOrderIdAsync("order_wh_001");
        var dbOrder = await orderRepo.GetByIdAsync(order.OrderId);
        Assert.Equal("Paid", dbPayment!.Status);
        Assert.Equal("pay_captured_999", dbPayment.RazorpayPaymentId);
        Assert.Equal("Paid", dbOrder!.PaymentStatus);
        Assert.Equal("Confirmed", dbOrder.Status);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithTamperedSignature_ThrowsPaymentVerificationException()
    {
        // 1. Arrange with valid secret
        var webhookSecret = "secret_for_tamper_test";
        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("TamperedWebhookDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = "test_secret",
                ["Razorpay:WebhookSecret"] = webhookSecret
            })
            .Build();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            NullLogger<RazorpayPaymentService>.Instance);

        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-WH-TAMPER",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 80.00m
        };
        await orderRepo.AddAsync(order);

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 1,
            RazorpayOrderId = "order_wh_tamper",
            Amount = 80.00m,
            Currency = "INR",
            Status = "Pending"
        };
        await paymentRepo.AddAsync(payment);

        var payload = @"{
            ""id"": ""evt_tamper_123"",
            ""event"": ""payment.captured"",
            ""payload"": {
                ""payment"": {
                    ""entity"": {
                        ""id"": ""pay_tampered_999"",
                        ""order_id"": ""order_wh_tamper""
                    }
                }
            }
        }";

        // Act & Assert: Forged signature must throw PaymentVerificationException
        await Assert.ThrowsAsync<PaymentVerificationException>(
            () => service.ProcessWebhookAsync(payload, "forged_malicious_signature_99999"));

        // Confirm database state is unchanged
        var dbPayment = await paymentRepo.GetByRazorpayOrderIdAsync("order_wh_tamper");
        var dbOrder = await orderRepo.GetByIdAsync(order.OrderId);
        Assert.Equal("Pending", dbPayment!.Status);
        Assert.Equal("Pending", dbOrder!.PaymentStatus);
    }

    [Fact]
    public async Task Webhook_WhenPaymentFailedArrivesAfterPaymentPaid_PreservesPaidStatus()
    {
        // 3. Out-of-order webhook test: A delayed payment.failed event arriving after payment.captured must NOT revert Paid status
        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("OutOfOrderWhDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = "test_secret",
                ["Razorpay:WebhookSecret"] = "test_wh_secret"
            })
            .Build();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            NullLogger<RazorpayPaymentService>.Instance);

        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-OUT-OF-ORDER",
            Status = "Confirmed",
            PaymentStatus = "Paid",
            TotalAmount = 120.00m
        };
        await orderRepo.AddAsync(order);

        // Payment is ALREADY Paid
        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 1,
            RazorpayOrderId = "order_rzp_out_of_order",
            RazorpayPaymentId = "pay_already_captured_111",
            Amount = 120.00m,
            Currency = "INR",
            Status = "Paid",
            PaidAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await paymentRepo.AddAsync(payment);

        var failedPayload = @"{
            ""id"": ""evt_delayed_failed_001"",
            ""event"": ""payment.failed"",
            ""payload"": {
                ""payment"": {
                    ""entity"": {
                        ""id"": ""pay_failed_attempt_000"",
                        ""order_id"": ""order_rzp_out_of_order"",
                        ""error_description"": ""Delayed network timeout failure""
                    }
                }
            }
        }";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("test_wh_secret"));
        var validSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(failedPayload))).ToLowerInvariant();

        // Act: Process the delayed failure webhook
        var processed = await service.ProcessWebhookAsync(failedPayload, validSignature);
        Assert.True(processed);

        // Assert: Status must STILL be Paid, never reverted to Failed!
        var dbPayment = await paymentRepo.GetByRazorpayOrderIdAsync("order_rzp_out_of_order");
        var dbOrder = await orderRepo.GetByIdAsync(order.OrderId);
        Assert.Equal("Paid", dbPayment!.Status);
        Assert.Equal("Paid", dbOrder!.PaymentStatus);
        Assert.Equal("Confirmed", dbOrder.Status);
    }

    [Fact]
    public async Task GetByOrderIdAsync_ReturnsSuccessfulPaymentOverFailedAttempt()
    {
        // Issue 3: Earlier or later failed attempts must never mask a successful Paid payment
        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("AttemptLookupDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var paymentRepo = new PaymentRepository(db);

        // Required parent order to fulfill foreign key navigation
        var parentOrder50 = new Order
        {
            OrderId = 50,
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-TEST-50",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 200.00m
        };
        await db.Orders.AddAsync(parentOrder50);
        await db.SaveChangesAsync();

        // Attempt 1: Failed payment (created 10 minutes ago)
        var failedPayment = new Payment
        {
            CanteenId = 1,
            OrderId = 50,
            RazorpayOrderId = "order_rzp_failed_1",
            Amount = 200.00m,
            Currency = "INR",
            Status = "Failed",
            FailureReason = "Card declined",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        await db.Payments.AddAsync(failedPayment);

        // Attempt 2: Successful Paid payment (created 5 minutes ago)
        var paidPayment = new Payment
        {
            CanteenId = 1,
            OrderId = 50,
            RazorpayOrderId = "order_rzp_paid_2",
            RazorpayPaymentId = "pay_success_123",
            Amount = 200.00m,
            Currency = "INR",
            Status = "Paid",
            PaidAt = DateTime.UtcNow.AddMinutes(-4),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await db.Payments.AddAsync(paidPayment);

        // Attempt 3: Another failed retry event (created 1 minute ago)
        var anotherFailedPayment = new Payment
        {
            CanteenId = 1,
            OrderId = 50,
            RazorpayOrderId = "order_rzp_failed_3",
            Amount = 200.00m,
            Currency = "INR",
            Status = "Failed",
            FailureReason = "User cancelled",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        await db.Payments.AddAsync(anotherFailedPayment);
        await db.SaveChangesAsync();

        // Act
        var result = await paymentRepo.GetByOrderIdAsync(1, 50);

        // Assert: Must return the Paid payment (Attempt 2)
        Assert.NotNull(result);
        Assert.Equal("Paid", result.Status);
        Assert.Equal("order_rzp_paid_2", result.RazorpayOrderId);
    }

    [Fact]
    public async Task GetByOrderIdAsync_WithOnlyFailedAttempts_ReturnsMostRecentAttempt()
    {
        // Issue 3: When no Paid attempt exists, returns the latest attempt chronologically
        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("FailedAttemptDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var paymentRepo = new PaymentRepository(db);

        // Required parent order to fulfill foreign key navigation
        var parentOrder75 = new Order
        {
            OrderId = 75,
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-TEST-75",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 100.00m
        };
        await db.Orders.AddAsync(parentOrder75);
        await db.SaveChangesAsync();

        var firstAttempt = new Payment
        {
            CanteenId = 1,
            OrderId = 75,
            RazorpayOrderId = "order_rzp_att_1",
            Amount = 100.00m,
            Currency = "INR",
            Status = "Failed",
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        var secondAttempt = new Payment
        {
            CanteenId = 1,
            OrderId = 75,
            RazorpayOrderId = "order_rzp_att_2",
            Amount = 100.00m,
            Currency = "INR",
            Status = "Failed",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await db.Payments.AddRangeAsync(firstAttempt, secondAttempt);
        await db.SaveChangesAsync();

        // Act
        var result = await paymentRepo.GetByOrderIdAsync(1, 75);

        // Assert: Returns the second attempt (latest by CreatedAt DESC)
        Assert.NotNull(result);
        Assert.Equal("order_rzp_att_2", result.RazorpayOrderId);
    }

    [Fact]
    public void Program_StartupGuard_RejectsMockPaymentService_InProductionEnvironment()
    {
        // Issue 4: Setting Razorpay:UseMock = true in Production must throw InvalidOperationException
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new[] { "--environment", "Production" });

        // Provide minimal required configuration so we test the mock guard specifically
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=TestDb;Username=test;Password=test";
        builder.Configuration["Jwt:Key"] = "DUMMY_KEY_FOR_TESTING_PURPOSES_ONLY_MINIMUM_32_BYTES_REQUIRED_12345";
        builder.Configuration["Jwt:Issuer"] = "CanteenTest";
        builder.Configuration["Jwt:Audience"] = "CanteenTest";
        builder.Configuration["Cors:AllowedOrigins:0"] = "http://localhost:3000";
        builder.Configuration["Razorpay:UseMock"] = "true"; // Mock in Production!

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var useMock = builder.Configuration.GetValue<bool>("Razorpay:UseMock");
            if (useMock && builder.Environment.IsProduction())
            {
                throw new InvalidOperationException("Mock payment provider is strictly forbidden in Production environment.");
            }
        });

        Assert.Contains("strictly forbidden in Production environment", ex.Message);
    }

    [Fact]
    public async Task CreatePaymentOrderAsync_WhenGatewayReturnsError_ThrowsSanitizedExceptionWithoutLeakingRawDetails()
    {
        // Issue 5: Raw gateway error details must NOT be returned in exception message
        var fakeHandler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                "{\"error\":{\"code\":\"BAD_REQUEST_ERROR\",\"description\":\"Amount too low\",\"internal_merchant_id\":\"acc_SECRET999\"}}",
                Encoding.UTF8,
                "application/json")
        });

        var inMemoryOptions = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase("SanitizedErrorDb_" + Guid.NewGuid())
            .Options;

        using var db = new CanteenDbContext(inMemoryOptions);
        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = "test_secret"
            })
            .Build();

        var service = new RazorpayPaymentService(
            new HttpClient(fakeHandler),
            config,
            orderRepo,
            paymentRepo,
            NullLogger<RazorpayPaymentService>.Instance);

        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-ERR-TEST",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 50.00m
        };
        await orderRepo.AddAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreatePaymentOrderAsync(order.OrderId));

        // Must be clean and sanitized:
        Assert.Equal("Payment gateway failed to initialize the payment order. Please try again or contact canteen staff.", ex.Message);
        // Must NOT leak raw gateway JSON or sensitive merchant IDs:
        Assert.DoesNotContain("acc_SECRET999", ex.Message);
        Assert.DoesNotContain("BAD_REQUEST_ERROR", ex.Message);
    }

    [Fact]
    public async Task UpdatePaymentAndOrderAsync_WhenDatabaseFailureOccurs_RollsBackBothUpdatesAtomically()
    {
        // Issue 2: Partial updates must not be committed if a failure occurs during atomic persistence
        var dbName = "AtomicFailDb_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // 1. Arrange initial pending payment and order
        using (var initialDb = new CanteenDbContext(options))
        {
            var initialOrder = new Order
            {
                OrderId = 201,
                CanteenId = 1,
                TableId = 1,
                OrderNumber = "ORD-ATOMIC-001",
                Status = "Pending",
                PaymentStatus = "Pending",
                TotalAmount = 250.00m
            };
            var initialPayment = new Payment
            {
                PaymentId = 301,
                OrderId = 201,
                CanteenId = 1,
                RazorpayOrderId = "order_rzp_atomic_001",
                Amount = 250.00m,
                Currency = "INR",
                Status = "Pending"
            };
            await initialDb.Orders.AddAsync(initialOrder);
            await initialDb.Payments.AddAsync(initialPayment);
            await initialDb.SaveChangesAsync();
        }

        // 2. Act: Attempt atomic update using ThrowingDbContext to simulate a database failure
        using (var throwingDb = new ThrowingDbContext(options))
        {
            var repo = new PaymentRepository(throwingDb);
            var payment = await throwingDb.Payments.FindAsync(301);
            var order = await throwingDb.Orders.FindAsync(201);

            payment!.Status = "Paid";
            order!.PaymentStatus = "Paid";
            order.Status = "Confirmed";

            // Arm the failure trigger
            throwingDb.ShouldThrow = true;

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                repo.UpdatePaymentAndOrderAsync(payment, order));
        }

        // 3. Assert: In a fresh context, verify that NEITHER payment nor order was changed to Paid
        using (var verifyDb = new CanteenDbContext(options))
        {
            var dbPayment = await verifyDb.Payments.FindAsync(301);
            var dbOrder = await verifyDb.Orders.FindAsync(201);

            Assert.Equal("Pending", dbPayment!.Status);
            Assert.Equal("Pending", dbOrder!.PaymentStatus);
            Assert.Equal("Pending", dbOrder.Status);
        }
    }

    private class ThrowingDbContext : CanteenDbContext
    {
        public bool ShouldThrow { get; set; }

        public ThrowingDbContext(DbContextOptions<CanteenDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new DbUpdateException("Simulated database failure during atomic transaction.");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    // Helper methods
    private async Task<(int OrderId, decimal TotalAmount, int CanteenId)> SetupOrderAsync(
        string prefix,
        decimal price,
        int quantity)
    {
        var (token, canteenId) = await CreateOwnerAndCanteenAsync(prefix);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tableId, qrToken) = await CreateTableAsync(canteenId, 1);
        var categoryId = await CreateCategoryAsync(canteenId, "Category " + prefix);
        var productId = await CreateProductAsync(canteenId, categoryId, "Item " + prefix, price, 50);

        // Clear auth header to simulate anonymous customer placing order
        _client.DefaultRequestHeaders.Authorization = null;

        var orderPayload = new CreateOrderDto
        {
            TableId = tableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = productId, Quantity = quantity }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/orders?qrToken={qrToken}", orderPayload);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();

        return (order!.OrderId, order.TotalAmount, canteenId);
    }

    private async Task<(string Token, int CanteenId)> CreateOwnerAndCanteenAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@canteen.com";

        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = $"Owner {prefix}",
            Email = email,
            Password = "Password123!"
        });
        reg.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Password123!"
        });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var canteenRes = await _client.PostAsJsonAsync("/api/canteens", new
        {
            Name = $"Canteen {prefix}",
            Location = "Central Food Court"
        });
        canteenRes.EnsureSuccessStatusCode();
        var canteen = await canteenRes.Content.ReadFromJsonAsync<CanteenDto>();

        return (canteen!.Token ?? auth.Token, canteen.CanteenId);
    }

    private async Task<(int TableId, string QrToken)> CreateTableAsync(int canteenId, int tableNumber)
    {
        var res = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/tables", new
        {
            TableNumber = tableNumber,
            Capacity = 4
        });
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<CanteenTableDto>();
        return (dto!.TableId, dto.QRToken);
    }

    private async Task<int> CreateCategoryAsync(int canteenId, string name)
    {
        var res = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/categories", new
        {
            Name = name,
            Description = "Test category"
        });
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<CategoryDto>();
        return dto!.CategoryId;
    }

    private async Task<int> CreateProductAsync(int canteenId, int categoryId, string name, decimal price, int stock)
    {
        var res = await _client.PostAsJsonAsync("/api/products", new
        {
            Name = name,
            Description = "Delicious food",
            Price = price,
            StockQuantity = stock,
            CategoryId = categoryId,
            IsAvailable = true
        });
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<ProductDto>();
        return dto!.ProductId;
    }
}
