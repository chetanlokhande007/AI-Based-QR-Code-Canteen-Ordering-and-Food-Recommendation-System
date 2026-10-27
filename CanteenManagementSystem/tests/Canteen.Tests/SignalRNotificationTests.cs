using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using CanteenManagementSystem.Hubs;
using CanteenManagementSystem.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Canteen.Tests;

public class SignalRNotificationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRNotificationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 1: HUB AUTHENTICATION & MULTI-TENANT ISOLATION
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinKitchenGroup_UnauthenticatedConnection_ThrowsHubException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orderRepo = new OrderRepository(db);
        var hub = new OrderHub(orderRepo, NullLogger<OrderHub>.Instance);

        var context = new TestHubCallerContext("conn_unauth", user: null);
        var groups = new TestGroupManager();
        hub.Context = context;
        hub.Groups = groups;

        // Act & Assert: Caller without CanteenId claim is rejected
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinKitchenGroup());
        Assert.Contains("not associated with any canteen", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(groups.AddedGroups);
    }

    [Fact]
    public async Task JoinKitchenGroup_AuthenticatedOwner_JoinsOnlyTheirServerDerivedCanteenGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orderRepo = new OrderRepository(db);
        var hub = new OrderHub(orderRepo, NullLogger<OrderHub>.Instance);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "100"),
            new Claim(ClaimTypes.Role, "CanteenOwner"),
            new Claim("CanteenId", "12")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new TestHubCallerContext("conn_owner_12", principal);
        var groups = new TestGroupManager();
        hub.Context = context;
        hub.Groups = groups;

        // Act: Owner joins kitchen group
        await hub.JoinKitchenGroup();

        // Assert: Group name is strictly server-derived: canteen_12
        Assert.Single(groups.AddedGroups);
        Assert.Equal("conn_owner_12", groups.AddedGroups[0].ConnectionId);
        Assert.Equal("canteen_12", groups.AddedGroups[0].GroupName);
    }

    [Fact]
    public async Task OnConnectedAsync_CanteenOwner_AutoJoinsKitchenGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orderRepo = new OrderRepository(db);
        var hub = new OrderHub(orderRepo, NullLogger<OrderHub>.Instance);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "200"),
            new Claim(ClaimTypes.Role, "CanteenOwner"),
            new Claim("CanteenId", "7")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new TestHubCallerContext("conn_auto_7", principal);
        var groups = new TestGroupManager();
        hub.Context = context;
        hub.Groups = groups;

        // Act
        await hub.OnConnectedAsync();

        // Assert: Automatically joined canteen_7
        Assert.Contains(groups.AddedGroups, g => g.ConnectionId == "conn_auto_7" && g.GroupName == "canteen_7");
    }

    [Fact]
    public async Task OnConnectedAsync_AnonymousCustomer_DoesNotJoinAnyKitchenGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orderRepo = new OrderRepository(db);
        var hub = new OrderHub(orderRepo, NullLogger<OrderHub>.Instance);

        var context = new TestHubCallerContext("conn_customer", user: null);
        var groups = new TestGroupManager();
        hub.Context = context;
        hub.Groups = groups;

        // Act
        await hub.OnConnectedAsync();

        // Assert: Customer was not added to any kitchen group
        Assert.Empty(groups.AddedGroups);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 2: SECURE CUSTOMER TRACKING GATING
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinOrderTrackingGroup_ValidToken_SuccessfullyJoinsOrderGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-TEST-001",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 150m,
            TrackingToken = "valid_secure_tracking_token_123",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var hub = new OrderHub(orderRepo, NullLogger<OrderHub>.Instance);

        var context = new TestHubCallerContext("conn_customer_tracking", user: null);
        var groups = new TestGroupManager();
        hub.Context = context;
        hub.Groups = groups;

        // Act
        await hub.JoinOrderTrackingGroup(order.OrderId, "valid_secure_tracking_token_123");

        // Assert: Successfully joined order_{orderId} group
        Assert.Single(groups.AddedGroups);
        Assert.Equal($"order_{order.OrderId}", groups.AddedGroups[0].GroupName);
        Assert.Equal("conn_customer_tracking", groups.AddedGroups[0].ConnectionId);
    }

    [Fact]
    public async Task JoinOrderTrackingGroup_InvalidOrTamperedToken_ThrowsHubException_DoesNotJoinGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var order = new Order
        {
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-TEST-002",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 250m,
            TrackingToken = "original_token_secret",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var hub = new OrderHub(orderRepo, NullLogger<OrderHub>.Instance);

        var context = new TestHubCallerContext("conn_attacker", user: null);
        var groups = new TestGroupManager();
        hub.Context = context;
        hub.Groups = groups;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HubException>(() =>
            hub.JoinOrderTrackingGroup(order.OrderId, "forged_guessing_token"));

        Assert.Contains("Invalid order or tracking token", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(groups.AddedGroups);
    }

    [Fact]
    public async Task JoinOrderTrackingGroup_NonExistentOrderId_ThrowsHubException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orderRepo = new OrderRepository(db);
        var hub = new OrderHub(orderRepo, NullLogger<OrderHub>.Instance);

        var context = new TestHubCallerContext("conn_probe", user: null);
        var groups = new TestGroupManager();
        hub.Context = context;
        hub.Groups = groups;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HubException>(() =>
            hub.JoinOrderTrackingGroup(999999, "any_token"));

        Assert.Contains("Invalid order or tracking token", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(groups.AddedGroups);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 3: WEBSOCKET QUERY-STRING JWT AUTH FOR ANGULAR
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JwtBearerEvents_ExtractsTokenFromQueryString_OnHubPath()
    {
        // Arrange
        var events = new JwtBearerEvents();
        var capturedToken = string.Empty;

        // Construct standard Hub options with OnMessageReceived
        var jwtOptions = new JwtBearerOptions
        {
            Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/orders"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            }
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/hubs/orders";
        httpContext.Request.QueryString = new QueryString("?access_token=test_angular_jwt_token");

        var messageReceivedContext = new MessageReceivedContext(
            httpContext,
            new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                null,
                typeof(JwtBearerHandler)),
            jwtOptions);

        // Act
        await jwtOptions.Events.MessageReceived(messageReceivedContext);

        // Assert: Angular's query-string token was extracted and assigned to context.Token
        Assert.Equal("test_angular_jwt_token", messageReceivedContext.Token);
    }

    [Fact]
    public async Task JwtBearerEvents_DoesNotExtractTokenFromQueryString_OnNonHubPath()
    {
        // Arrange
        var jwtOptions = new JwtBearerOptions
        {
            Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/orders"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            }
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/orders"; // Non-hub REST endpoint
        httpContext.Request.QueryString = new QueryString("?access_token=some_token");

        var messageReceivedContext = new MessageReceivedContext(
            httpContext,
            new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                null,
                typeof(JwtBearerHandler)),
            jwtOptions);

        // Act
        await jwtOptions.Events.MessageReceived(messageReceivedContext);

        // Assert: Ignored on standard REST paths (only header-based auth allowed)
        Assert.Null(messageReceivedContext.Token);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 4: NEW ORDER NOTIFICATIONS
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_WhenSuccessful_DispatchesNewOrderPlaced_ToCorrectCanteenKitchenGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var table = new CanteenTable { TableId = 1, CanteenId = 3, TableNumber = 1, QRToken = "qr_token_123", IsAvailable = true };
        var product = new Product { ProductId = 10, CanteenId = 3, Name = "Veg Burger", Price = 80m, StockQuantity = 20, IsAvailable = true };
        db.Tables.Add(table);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var productRepo = new ProductRepository(db);
        var tableRepo = new CanteenTableRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new OrderService(orderRepo, productRepo, tableRepo, notificationService);

        var dto = new CreateOrderDto
        {
            TableId = 1,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 10, Quantity = 2 }
            }
        };

        // Act
        var result = await service.CreateAsync(dto, "qr_token_123");

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.TrackingToken));
        Assert.Single(notificationService.PlacedOrders);

        var sentOrder = notificationService.PlacedOrders[0];
        Assert.Equal(result.OrderId, sentOrder.OrderId);
        Assert.Equal(3, sentOrder.CanteenId);
        Assert.Equal("Pending", sentOrder.Status);
        Assert.Equal(160m, sentOrder.TotalAmount);
    }

    [Fact]
    public async Task CreateOrder_WhenStockInsufficient_ThrowsException_DoesNotSendNotification()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var table = new CanteenTable { TableId = 1, CanteenId = 3, TableNumber = 1, QRToken = "qr_token_123", IsAvailable = true };
        var product = new Product { ProductId = 10, CanteenId = 3, Name = "Veg Burger", Price = 80m, StockQuantity = 1, IsAvailable = true };
        db.Tables.Add(table);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var productRepo = new ProductRepository(db);
        var tableRepo = new CanteenTableRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new OrderService(orderRepo, productRepo, tableRepo, notificationService);

        var dto = new CreateOrderDto
        {
            TableId = 1,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 10, Quantity = 5 } // Request 5 when stock is 1
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientStockException>(() => service.CreateAsync(dto, "qr_token_123"));
        Assert.Empty(notificationService.PlacedOrders);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 5: PAYMENT CONFIRMED NOTIFICATIONS
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyPayment_WithValidSignature_DispatchesPaymentConfirmed_ToKitchen()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        const string keySecret = "secret_sig_test_key_1234567890123456";

        var order = new Order
        {
            CanteenId = 5,
            TableId = 1,
            OrderNumber = "ORD-PAY-100",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 200m,
            TrackingToken = "track_pay_1",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        const string razorpayOrderId = "order_rzp_pay_100";
        const string razorpayPaymentId = "pay_rzp_success_999";

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 5,
            RazorpayOrderId = razorpayOrderId,
            Amount = 200m,
            Currency = "INR",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        // Calculate valid signature: HMAC-SHA256(order_id + "|" + payment_id)
        var payloadToSign = $"{razorpayOrderId}|{razorpayPaymentId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
        var validSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadToSign))).ToLowerInvariant();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = keySecret
            })
            .Build();

        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            notificationService,
            NullLogger<RazorpayPaymentService>.Instance);

        var verifyDto = new VerifyPaymentDto
        {
            OrderId = order.OrderId,
            RazorpayOrderId = razorpayOrderId,
            RazorpayPaymentId = razorpayPaymentId,
            RazorpaySignature = validSignature
        };

        // Act
        var result = await service.VerifyPaymentAsync(verifyDto);

        // Assert
        Assert.Equal("Paid", result.Status);
        Assert.Single(notificationService.ConfirmedPayments);

        var notification = notificationService.ConfirmedPayments[0];
        Assert.Equal(5, notification.CanteenId);
        Assert.Equal(order.OrderId, notification.OrderId);
        Assert.Equal("ORD-PAY-100", notification.OrderNumber);
        Assert.Equal(200m, notification.Amount);
    }

    [Fact]
    public async Task VerifyPayment_WithTamperedSignature_ThrowsException_DoesNotSendNotification()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        const string keySecret = "secret_sig_test_key_1234567890123456";

        var order = new Order
        {
            CanteenId = 5,
            TableId = 1,
            OrderNumber = "ORD-PAY-101",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 200m,
            TrackingToken = "track_pay_2",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 5,
            RazorpayOrderId = "order_rzp_tamper",
            Amount = 200m,
            Currency = "INR",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = keySecret
            })
            .Build();

        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            notificationService,
            NullLogger<RazorpayPaymentService>.Instance);

        var verifyDto = new VerifyPaymentDto
        {
            OrderId = order.OrderId,
            RazorpayOrderId = "order_rzp_tamper",
            RazorpayPaymentId = "pay_fake",
            RazorpaySignature = "invalid_tampered_signature"
        };

        // Act & Assert
        await Assert.ThrowsAsync<PaymentVerificationException>(() => service.VerifyPaymentAsync(verifyDto));
        Assert.Empty(notificationService.ConfirmedPayments);
    }

    [Fact]
    public async Task VerifyPayment_DuplicateCallOnAlreadyPaid_ReturnsIdempotently_DoesNotSendDuplicateNotification()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var order = new Order
        {
            CanteenId = 5,
            TableId = 1,
            OrderNumber = "ORD-DUP-01",
            Status = "Confirmed",
            PaymentStatus = "Paid",
            TotalAmount = 300m,
            TrackingToken = "track_dup_1",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = order.OrderId,
            CanteenId = 5,
            RazorpayOrderId = "order_dup_1",
            RazorpayPaymentId = "pay_dup_already_done",
            RazorpaySignature = "sig_valid",
            Amount = 300m,
            Currency = "INR",
            Status = "Paid",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Razorpay:KeyId"] = "rzp_test_key",
                ["Razorpay:KeySecret"] = "any_secret"
            })
            .Build();

        var orderRepo = new OrderRepository(db);
        var paymentRepo = new PaymentRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new RazorpayPaymentService(
            new HttpClient(),
            config,
            orderRepo,
            paymentRepo,
            notificationService,
            NullLogger<RazorpayPaymentService>.Instance);

        var verifyDto = new VerifyPaymentDto
        {
            OrderId = order.OrderId,
            RazorpayOrderId = "order_dup_1",
            RazorpayPaymentId = "pay_dup_already_done",
            RazorpaySignature = "sig_valid"
        };

        // Act
        var result = await service.VerifyPaymentAsync(verifyDto);

        // Assert: Early exit, no extra notification emitted
        Assert.Equal("Paid", result.Status);
        Assert.Empty(notificationService.ConfirmedPayments);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 6: ORDER STATUS UPDATES (CUSTOMER NOTIFICATIONS)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_ToInPreparation_DispatchesOrderStatusUpdated_ToCustomerGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var order = new Order
        {
            CanteenId = 2,
            TableId = 1,
            OrderNumber = "ORD-STATUS-01",
            Status = "Confirmed",
            PaymentStatus = "Paid",
            TotalAmount = 100m,
            TrackingToken = "token_status_1",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var productRepo = new ProductRepository(db);
        var tableRepo = new CanteenTableRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new OrderService(orderRepo, productRepo, tableRepo, notificationService);

        // Act
        var result = await service.UpdateStatusAsync(2, order.OrderId, "InPreparation");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("InPreparation", result.Status);
        Assert.Single(notificationService.StatusUpdates);

        var update = notificationService.StatusUpdates[0];
        Assert.Equal(order.OrderId, update.OrderId);
        Assert.Equal("ORD-STATUS-01", update.OrderNumber);
        Assert.Equal("InPreparation", update.NewStatus);
    }

    [Fact]
    public async Task UpdateStatus_ToReady_DispatchesOrderStatusUpdated_ToCustomerGroup()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var order = new Order
        {
            CanteenId = 2,
            TableId = 1,
            OrderNumber = "ORD-STATUS-02",
            Status = "InPreparation",
            PaymentStatus = "Paid",
            TotalAmount = 120m,
            TrackingToken = "token_status_2",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var productRepo = new ProductRepository(db);
        var tableRepo = new CanteenTableRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new OrderService(orderRepo, productRepo, tableRepo, notificationService);

        // Act
        var result = await service.UpdateStatusAsync(2, order.OrderId, "Ready");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Ready", result.Status);
        Assert.Single(notificationService.StatusUpdates);

        var update = notificationService.StatusUpdates[0];
        Assert.Equal(order.OrderId, update.OrderId);
        Assert.Equal("Ready", update.NewStatus);
    }

    [Fact]
    public async Task UpdateStatus_ToCancelled_DoesNotDispatchCustomerNotification()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var order = new Order
        {
            CanteenId = 2,
            TableId = 1,
            OrderNumber = "ORD-STATUS-03",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 120m,
            TrackingToken = "token_status_3",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var productRepo = new ProductRepository(db);
        var tableRepo = new CanteenTableRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new OrderService(orderRepo, productRepo, tableRepo, notificationService);

        // Act
        var result = await service.UpdateStatusAsync(2, order.OrderId, "Cancelled");

        // Assert: Customer notification is only for InPreparation and Ready
        Assert.NotNull(result);
        Assert.Equal("Cancelled", result.Status);
        Assert.Empty(notificationService.StatusUpdates);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ThrowsException_DoesNotDispatchNotification()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var order = new Order
        {
            CanteenId = 2,
            TableId = 1,
            OrderNumber = "ORD-STATUS-04",
            Status = "Completed", // Terminal status
            PaymentStatus = "Paid",
            TotalAmount = 120m,
            TrackingToken = "token_status_4",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var orderRepo = new OrderRepository(db);
        var productRepo = new ProductRepository(db);
        var tableRepo = new CanteenTableRepository(db);
        var notificationService = new RecordingNotificationService();

        var service = new OrderService(orderRepo, productRepo, tableRepo, notificationService);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOrderException>(() =>
            service.UpdateStatusAsync(2, order.OrderId, "InPreparation"));

        Assert.Empty(notificationService.StatusUpdates);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 7: ORDER NOTIFICATION SERVICE DISPATCH & RESILIENCE
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderNotificationService_DispatchesToCorrectSignalRGroups()
    {
        // Arrange
        var hubContext = new TestHubContext();
        var service = new OrderNotificationService(hubContext, NullLogger<OrderNotificationService>.Instance);

        var orderDto = new OrderDto
        {
            OrderId = 15,
            OrderNumber = "ORD-GRP-01",
            CanteenId = 9,
            TableId = 2,
            Status = "Pending",
            TotalAmount = 250m,
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItemDto>
            {
                new() { ProductId = 1, ProductName = "Dosa", Quantity = 2, UnitPrice = 50m }
            }
        };

        // Act 1: New order placed
        await service.NotifyNewOrderPlacedAsync(orderDto);

        // Assert: Sent to kitchen group canteen_9
        Assert.True(hubContext.ClientsMock.GroupProxies.ContainsKey("canteen_9"));
        var canteenGroupProxy = hubContext.ClientsMock.GroupProxies["canteen_9"];
        Assert.Contains(canteenGroupProxy.SentMessages, m => m.Method == "NewOrderPlaced");

        // Act 2: Payment confirmed
        await service.NotifyPaymentConfirmedAsync(9, 15, "ORD-GRP-01", 250m, DateTime.UtcNow);

        // Assert: Sent to kitchen group canteen_9
        Assert.Contains(canteenGroupProxy.SentMessages, m => m.Method == "PaymentConfirmed");

        // Act 3: Order status updated
        await service.NotifyOrderStatusUpdatedAsync(15, "ORD-GRP-01", "InPreparation", DateTime.UtcNow);

        // Assert: Sent strictly to customer group order_15
        Assert.True(hubContext.ClientsMock.GroupProxies.ContainsKey("order_15"));
        var customerGroupProxy = hubContext.ClientsMock.GroupProxies["order_15"];
        Assert.Contains(customerGroupProxy.SentMessages, m => m.Method == "OrderStatusUpdated");
    }

    [Fact]
    public async Task OrderNotificationService_WhenHubThrowsException_DoesNotCrashCaller()
    {
        // Arrange: Hub proxy that throws an exception simulating a network failure
        var hubContext = new FaultyHubContext();
        var service = new OrderNotificationService(hubContext, NullLogger<OrderNotificationService>.Instance);

        var orderDto = new OrderDto
        {
            OrderId = 99,
            OrderNumber = "ORD-FAIL",
            CanteenId = 1,
            TotalAmount = 50m,
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert: Exception is caught and logged safely without bubbling up
        var ex1 = await Record.ExceptionAsync(() => service.NotifyNewOrderPlacedAsync(orderDto));
        Assert.Null(ex1);

        var ex2 = await Record.ExceptionAsync(() => service.NotifyPaymentConfirmedAsync(1, 99, "ORD-FAIL", 50m, DateTime.UtcNow));
        Assert.Null(ex2);

        var ex3 = await Record.ExceptionAsync(() => service.NotifyOrderStatusUpdatedAsync(99, "ORD-FAIL", "Ready", DateTime.UtcNow));
        Assert.Null(ex3);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SECTION 8: CUSTOMER TRACKING REST ENDPOINT (AUDIT-002)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TrackOrder_WithValidTrackingToken_ReturnsOrderDetailsWithoutAuth()
    {
        // 1. Arrange an order in test database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();

        var table = new CanteenTable { CanteenId = 1, TableNumber = 99, QRToken = "qr_track_99", IsAvailable = true };
        db.Tables.Add(table);
        await db.SaveChangesAsync();

        var token = "token_customer_secret_12345";
        var order = new Order
        {
            CanteenId = 1,
            TableId = table.TableId,
            OrderNumber = "ORD-TRACK-01",
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = 120m,
            TrackingToken = token,
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // 2. Act: Call public REST endpoint
        var response = await _client.GetAsync($"/api/orders/track/{token}");

        // 3. Assert: 200 OK with correct order
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(orderDto);
        Assert.Equal(order.OrderId, orderDto.OrderId);
        Assert.Equal("ORD-TRACK-01", orderDto.OrderNumber);
        Assert.Equal(token, orderDto.TrackingToken);
    }

    [Fact]
    public async Task TrackOrder_WithInvalidToken_Returns404NotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/track/non_existent_token_random_9999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TEST FAILS & DOUBLES
    // ─────────────────────────────────────────────────────────────────────────────

    private class RecordingNotificationService : IOrderNotificationService
    {
        public List<OrderDto> PlacedOrders { get; } = new();
        public List<(int CanteenId, int OrderId, string OrderNumber, decimal Amount, DateTime PaidAt)> ConfirmedPayments { get; } = new();
        public List<(int OrderId, string OrderNumber, string NewStatus, DateTime UpdatedAt)> StatusUpdates { get; } = new();

        public Task NotifyNewOrderPlacedAsync(OrderDto order)
        {
            PlacedOrders.Add(order);
            return Task.CompletedTask;
        }

        public Task NotifyPaymentConfirmedAsync(int canteenId, int orderId, string orderNumber, decimal amount, DateTime paidAt)
        {
            ConfirmedPayments.Add((canteenId, orderId, orderNumber, amount, paidAt));
            return Task.CompletedTask;
        }

        public Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt)
        {
            StatusUpdates.Add((orderId, orderNumber, newStatus, updatedAt));
            return Task.CompletedTask;
        }
    }

    private class TestHubCallerContext : HubCallerContext
    {
        private readonly ClaimsPrincipal? _user;
        private readonly string _connectionId;

        public TestHubCallerContext(string connectionId, ClaimsPrincipal? user)
        {
            _connectionId = connectionId;
            _user = user;
        }

        public override string ConnectionId => _connectionId;
        public override string? UserIdentifier => _user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        public override ClaimsPrincipal? User => _user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private class TestGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = new();
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private class TestClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> SentMessages { get; } = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private class TestHubClients : IHubClients
    {
        public Dictionary<string, TestClientProxy> GroupProxies { get; } = new();
        public TestClientProxy AllClients { get; } = new();

        public IClientProxy All => AllClients;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => AllClients;
        public IClientProxy Client(string connectionId) => AllClients;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => AllClients;
        public IClientProxy Group(string groupName)
        {
            if (!GroupProxies.TryGetValue(groupName, out var proxy))
            {
                proxy = new TestClientProxy();
                GroupProxies[groupName] = proxy;
            }
            return proxy;
        }
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => AllClients;
        public IClientProxy User(string userId) => AllClients;
        public IClientProxy Users(IReadOnlyList<string> userIds) => AllClients;
    }

    private class TestHubContext : IHubContext<OrderHub>
    {
        public TestHubClients ClientsMock { get; } = new();
        public TestGroupManager GroupsMock { get; } = new();

        public IHubClients Clients => ClientsMock;
        public IGroupManager Groups => GroupsMock;
    }

    private class FaultyClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated SignalR network connection drop.");
        }
    }

    private class FaultyHubClients : IHubClients
    {
        private readonly FaultyClientProxy _proxy = new();

        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy Group(string groupName) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) => _proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private class FaultyHubContext : IHubContext<OrderHub>
    {
        public IHubClients Clients { get; } = new FaultyHubClients();
        public IGroupManager Groups { get; } = new TestGroupManager();
    }
}
