using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Canteen.Tests;

public class OrderCancellationTests
{
    private class DummyNotificationService : IOrderNotificationService
    {
        public Task NotifyNewOrderPlacedAsync(OrderDto order) => Task.CompletedTask;
        public Task NotifyPaymentConfirmedAsync(int canteenId, int orderId, string orderNumber, decimal amount, DateTime paidAt) => Task.CompletedTask;
        public Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt) => Task.CompletedTask;
    }

    private class DummyPaymentService : IPaymentService
    {
        public Task<PaymentOrderResponseDto> CreatePaymentOrderAsync(int orderId) =>
            Task.FromResult(new PaymentOrderResponseDto { RazorpayOrderId = "order_mock_123", KeyId = "mock_key" });

        public Task<PaymentDto> VerifyPaymentAsync(VerifyPaymentDto dto) =>
            Task.FromResult(new PaymentDto { Status = "Paid", PaymentMethod = "Online" });

        public Task<bool> ProcessWebhookAsync(string rawBody, string signature) => Task.FromResult(true);

        public Task<PaymentDto?> GetPaymentByOrderIdAsync(int canteenId, int orderId) => Task.FromResult<PaymentDto?>(null);

        public Task<PaymentDto> CreateCounterPaymentAsync(int orderId) =>
            Task.FromResult(new PaymentDto { Status = "Pending", PaymentMethod = "Counter" });

        public Task<PaymentDto> ConfirmCounterPaymentAsync(int canteenId, int orderId) =>
            Task.FromResult(new PaymentDto { Status = "Paid", PaymentMethod = "Counter" });

        public Task<RefundResultDto> RefundPaymentAsync(Payment payment, decimal amount, string reason) =>
            Task.FromResult(new RefundResultDto { Success = true, GatewayRefundId = "rfnd_mock_123", Status = "Succeeded", IsSimulated = true });
    }

    private CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "OrderCancellationTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    private async Task<(
        Canteen.Model.Entities.Canteen canteen,
        CanteenTable table,
        Product product,
        Order order,
        Payment payment
    )> SeedTestDataAsync(CanteenDbContext context)
    {
        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = 1,
            Name = "Campus Bistro",
            Slug = "campus-bistro",
            IsActive = true,
            FeeAbsorptionPolicy = "CustomerPays",
            TaxPercentage = 5
        };
        context.Canteens.Add(canteen);

        var table = new CanteenTable
        {
            TableId = 1,
            CanteenId = 1,
            TableNumber = 10,
            QRToken = "test_qr_token_123",
            IsAvailable = true
        };
        context.Tables.Add(table);

        var category = new Category
        {
            CategoryId = 1,
            CanteenId = 1,
            Name = "Main Course"
        };
        context.Categories.Add(category);

        var product = new Product
        {
            ProductId = 1,
            CanteenId = 1,
            CategoryId = 1,
            Name = "Paneer Biryani",
            Price = 200m,
            StockQuantity = 20,
            IsAvailable = true
        };
        context.Products.Add(product);

        var order = new Order
        {
            OrderId = 1,
            CanteenId = 1,
            TableId = 1,
            OrderNumber = "ORD-20261102-1001",
            Status = "Pending",
            PaymentStatus = "Paid",
            SubtotalAmount = 200m,
            TaxAmount = 10m,
            PlatformFeeAmount = 5m,
            PlatformFeePolicy = "CustomerPays",
            TotalAmount = 215m,
            CanteenEarningsAmount = 210m,
            TrackingToken = "token_cancellation_abc123",
            AppliedPolicyVersion = 1,
            CreatedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);

        var payment = new Payment
        {
            PaymentId = 1,
            OrderId = 1,
            CanteenId = 1,
            Amount = 215m,
            Currency = "INR",
            PaymentMethod = "Online",
            Status = "Paid",
            RazorpayPaymentId = "pay_mock_12345",
            RazorpayOrderId = "order_mock_12345"
        };
        context.Payments.Add(payment);

        await context.SaveChangesAsync();
        return (canteen, table, product, order, payment);
    }

    [Fact]
    public async Task OrderCancellationRepository_AddAndRetrieve_WorksCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var (canteen, _, _, order, _) = await SeedTestDataAsync(context);
        var repo = new OrderCancellationRepository(context);

        var cancellation = new OrderCancellation
        {
            OrderId = order.OrderId,
            CanteenId = canteen.CanteenId,
            InitiatedByRole = "Customer",
            ActorOwnerId = null,
            Reason = "Customer changed mind",
            PreviousOrderStatus = "Pending",
            FoodRefundAmount = 210m,
            PlatformFeeRefundAmount = 0m,
            TotalRefundAmount = 210m,
            StockRestored = true,
            PlatformPolicyVersion = 1,
            CancelledAt = DateTime.UtcNow
        };

        var saved = await repo.AddAsync(cancellation);
        Assert.True(saved.CancellationId > 0);

        var byId = await repo.GetByIdAsync(saved.CancellationId);
        Assert.NotNull(byId);
        Assert.Equal("Customer", byId!.InitiatedByRole);
        Assert.Equal(210m, byId.TotalRefundAmount);

        var byOrderId = await repo.GetByOrderIdAsync(order.OrderId);
        Assert.NotNull(byOrderId);
        Assert.Equal(order.OrderId, byOrderId!.OrderId);

        var byCanteen = await repo.GetAllByCanteenAsync(canteen.CanteenId);
        Assert.Single(byCanteen);
    }

    [Fact]
    public async Task CustomerCancellation_RecordsAuditAndRefundsFoodSubtotalOnly()
    {
        using var context = CreateInMemoryDbContext();
        var (canteen, _, _, order, payment) = await SeedTestDataAsync(context);

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var paymentRepo = new PaymentRepository(context);
        var cancellationRepo = new OrderCancellationRepository(context);
        var refundRepo = new RefundRepository(context);
        var paymentService = new DummyPaymentService();
        var notificationService = new DummyNotificationService();
        var refundService = new RefundService(refundRepo, orderRepo, paymentRepo, paymentService, notificationService, NullLogger<RefundService>.Instance);

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            paymentRepo,
            notificationService,
            null,
            null,
            cancellationRepo,
            refundService);

        var result = await orderService.CancelOrderByTrackingTokenAsync(order.TrackingToken);

        Assert.NotNull(result);
        Assert.Equal("Cancelled", result!.Status);
        Assert.Equal("Refunded", result.PaymentStatus);
        Assert.Equal(210m, result.RefundedAmount); // 215 - 5 platform fee

        // Verify OrderCancellation repository audit record
        var cancellationAudit = await cancellationRepo.GetByOrderIdAsync(order.OrderId);
        Assert.NotNull(cancellationAudit);
        Assert.Equal("Customer", cancellationAudit!.InitiatedByRole);
        Assert.Equal(210m, cancellationAudit.FoodRefundAmount);
        Assert.Equal(0m, cancellationAudit.PlatformFeeRefundAmount);
        Assert.Equal(210m, cancellationAudit.TotalRefundAmount);
        Assert.True(cancellationAudit.StockRestored);

        // Verify Refund record in RefundRepository
        var refundRecord = await refundRepo.GetByOrderIdAsync(order.OrderId);
        Assert.NotNull(refundRecord);
        Assert.Equal(210m, refundRecord!.Amount);
        Assert.Equal("Succeeded", refundRecord.Status);
    }

    [Fact]
    public async Task CanteenCancellation_RecordsAuditAndRefunds100Percent()
    {
        using var context = CreateInMemoryDbContext();
        var (canteen, _, _, order, payment) = await SeedTestDataAsync(context);

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var paymentRepo = new PaymentRepository(context);
        var cancellationRepo = new OrderCancellationRepository(context);
        var refundRepo = new RefundRepository(context);
        var paymentService = new DummyPaymentService();
        var notificationService = new DummyNotificationService();
        var refundService = new RefundService(refundRepo, orderRepo, paymentRepo, paymentService, notificationService, NullLogger<RefundService>.Instance);

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            paymentRepo,
            notificationService,
            null,
            null,
            cancellationRepo,
            refundService);

        var result = await orderService.CancelOrderFromCanteenAsync(canteen.CanteenId, order.OrderId, "Kitchen stove malfunction", 42);

        Assert.NotNull(result);
        Assert.Equal("Cancelled", result!.Status);
        Assert.Equal("Refunded", result.PaymentStatus);
        Assert.Equal(215m, result.RefundedAmount); // 100% full refund including platform fee

        // Verify OrderCancellation repository audit record
        var cancellationAudit = await cancellationRepo.GetByOrderIdAsync(order.OrderId);
        Assert.NotNull(cancellationAudit);
        Assert.Equal("CanteenOwner", cancellationAudit!.InitiatedByRole);
        Assert.Equal(42, cancellationAudit.ActorOwnerId);
        Assert.Equal("Kitchen stove malfunction", cancellationAudit.Reason);
        Assert.Equal(215m, cancellationAudit.TotalRefundAmount);

        // Verify 100% Refund in RefundRepository
        var refundRecord = await refundRepo.GetByOrderIdAsync(order.OrderId);
        Assert.NotNull(refundRecord);
        Assert.Equal(215m, refundRecord!.Amount);
        Assert.Equal("Succeeded", refundRecord.Status);
    }
}
