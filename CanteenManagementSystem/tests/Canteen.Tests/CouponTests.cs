using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Canteen.Tests;

public class CouponTests
{
    private CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "CouponTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    [Fact]
    public async Task ValidateCoupon_PercentageDiscount_CalculatesCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "SAVE20",
            Title = "20% Off",
            DiscountType = "Percentage",
            DiscountValue = 20.00m,
            MinOrderAmount = 100.00m,
            MaxDiscountAmount = null,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "SAVE20",
            FoodSubtotal = 250.00m
        });

        Assert.True(result.IsValid);
        Assert.Equal(50.00m, result.DiscountAmount); // 20% of 250 = 50
        Assert.Equal(200.00m, result.NetFoodSubtotal);
    }

    [Fact]
    public async Task ValidateCoupon_PercentageDiscount_EnforcesMaxDiscountCap()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "MEGA50",
            Title = "50% Off up to ₹60",
            DiscountType = "Percentage",
            DiscountValue = 50.00m,
            MinOrderAmount = 100.00m,
            MaxDiscountAmount = 60.00m, // Capped at ₹60
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "MEGA50",
            FoodSubtotal = 300.00m // 50% would be 150, but capped at 60
        });

        Assert.True(result.IsValid);
        Assert.Equal(60.00m, result.DiscountAmount);
        Assert.Equal(240.00m, result.NetFoodSubtotal);
    }

    [Fact]
    public async Task ValidateCoupon_FixedAmount_CalculatesCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "FLAT30",
            Title = "Flat ₹30 Off",
            DiscountType = "FixedAmount",
            DiscountValue = 30.00m,
            MinOrderAmount = 100.00m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "FLAT30",
            FoodSubtotal = 150.00m
        });

        Assert.True(result.IsValid);
        Assert.Equal(30.00m, result.DiscountAmount);
        Assert.Equal(120.00m, result.NetFoodSubtotal);
    }

    [Fact]
    public async Task ValidateCoupon_FixedAmount_DoesNotExceedFoodSubtotal()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "FLAT100",
            Title = "Flat ₹100 Off",
            DiscountType = "FixedAmount",
            DiscountValue = 100.00m,
            MinOrderAmount = 50.00m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "FLAT100",
            FoodSubtotal = 80.00m // Less than discount
        });

        Assert.True(result.IsValid);
        Assert.Equal(80.00m, result.DiscountAmount); // Capped at subtotal
        Assert.Equal(0.00m, result.NetFoodSubtotal);
    }

    [Fact]
    public async Task ValidateCoupon_ExpiredCoupon_ReturnsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "EXPIRED",
            Title = "Expired Promo",
            DiscountType = "FixedAmount",
            DiscountValue = 20.00m,
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "EXPIRED",
            FoodSubtotal = 200.00m
        });

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_FutureCoupon_ReturnsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "FUTURE",
            Title = "Future Promo",
            DiscountType = "FixedAmount",
            DiscountValue = 20.00m,
            StartDate = DateTime.UtcNow.AddDays(5), // Starts in 5 days
            EndDate = DateTime.UtcNow.AddDays(15),
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "FUTURE",
            FoodSubtotal = 200.00m
        });

        Assert.False(result.IsValid);
        Assert.Contains("not started", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_InactiveCoupon_ReturnsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "PAUSED",
            Title = "Paused Promo",
            DiscountType = "Percentage",
            DiscountValue = 15.00m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = false // Deactivated by owner
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "PAUSED",
            FoodSubtotal = 200.00m
        });

        Assert.False(result.IsValid);
        Assert.Contains("inactive", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_SubtotalBelowMinimum_ReturnsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "MIN200",
            Title = "₹50 Off on ₹200",
            DiscountType = "FixedAmount",
            DiscountValue = 50.00m,
            MinOrderAmount = 200.00m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "MIN200",
            FoodSubtotal = 150.00m // ₹50 short
        });

        Assert.False(result.IsValid);
        Assert.Contains("Add ₹50.00 more", result.Message);
    }

    [Fact]
    public async Task ValidateCoupon_TotalUsageLimitReached_ReturnsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "FLASH50",
            Title = "First 50 Orders",
            DiscountType = "Percentage",
            DiscountValue = 30.00m,
            TotalUsageLimit = 50,
            TimesUsed = 50, // Limit reached
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "FLASH50",
            FoodSubtotal = 200.00m
        });

        Assert.False(result.IsValid);
        Assert.Contains("redemption limit has been reached", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_PerCustomerUsageLimitReached_ReturnsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        var coupon = await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "ONCEONLY",
            Title = "One Time Welcome Offer",
            DiscountType = "Percentage",
            DiscountValue = 25.00m,
            UsageLimitPerCustomer = 1, // Only 1 use per customer
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        // Record a past redemption for phone 9876543210
        await repo.AddRedemptionAsync(new CouponRedemption
        {
            CouponId = coupon.CouponId,
            OrderId = 101,
            CanteenId = 1,
            CustomerIdentifier = "9876543210",
            DiscountAmountApplied = 50.00m,
            RedeemedAt = DateTime.UtcNow.AddDays(-1)
        });

        var result = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "ONCEONLY",
            FoodSubtotal = 200.00m,
            CustomerIdentifier = "9876543210"
        });

        Assert.False(result.IsValid);
        Assert.Contains("maximum allowed number of times", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_WrongCanteenId_ReturnsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        // Coupon belongs to Canteen 1
        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = 1,
            Code = "CANTEEN1ONLY",
            Title = "Canteen 1 Promo",
            DiscountType = "Percentage",
            DiscountValue = 10.00m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        // Tried to apply at Canteen 2
        var result = await service.ValidateCouponAsync(2, new ValidateCouponRequestDto
        {
            Code = "CANTEEN1ONLY",
            FoodSubtotal = 200.00m
        });

        Assert.False(result.IsValid);
        Assert.Contains("invalid or does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_PlatformWideCoupon_AppliesToAnyCanteen()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new CouponRepository(context);
        var service = new CouponService(repo);

        // Platform-wide coupon (CanteenId == null)
        await repo.AddCouponAsync(new Coupon
        {
            CanteenId = null,
            Code = "GLOBALFESTIVE",
            Title = "Festival ₹40 Off",
            DiscountType = "FixedAmount",
            DiscountValue = 40.00m,
            MinOrderAmount = 150.00m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        });

        // Test applying at Canteen 1
        var result1 = await service.ValidateCouponAsync(1, new ValidateCouponRequestDto
        {
            Code = "GLOBALFESTIVE",
            FoodSubtotal = 200.00m
        });
        Assert.True(result1.IsValid);
        Assert.Equal(40.00m, result1.DiscountAmount);

        // Test applying at Canteen 2
        var result2 = await service.ValidateCouponAsync(2, new ValidateCouponRequestDto
        {
            Code = "GLOBALFESTIVE",
            FoodSubtotal = 180.00m
        });
        Assert.True(result2.IsValid);
        Assert.Equal(40.00m, result2.DiscountAmount);
    }

    [Fact]
    public async Task CreateOrder_WithValidCoupon_AppliesDiscountAndRecordsRedemption()
    {
        using var context = CreateInMemoryDbContext();

        // 1. Seed Canteen, Table, Product
        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = 1,
            Name = "Tech Park Canteen",
            TaxPercentage = 5.00m,
            FeeAbsorptionPolicy = "CustomerPays",
            IsActive = true
        };
        context.Canteens.Add(canteen);

        var table = new CanteenTable
        {
            TableId = 1,
            CanteenId = 1,
            TableNumber = 1,
            QRToken = "valid-qr-token-123",
            IsAvailable = true
        };
        context.Tables.Add(table);

        var product = new Product
        {
            ProductId = 1,
            CanteenId = 1,
            Name = "Veg Biryani",
            Price = 200.00m,
            IsAvailable = true,
            StockQuantity = 50
        };
        context.Products.Add(product);

        // Seed Coupon
        var coupon = new Coupon
        {
            CouponId = 1,
            CanteenId = 1,
            Code = "SAVE50",
            Title = "Flat ₹50 Off",
            DiscountType = "FixedAmount",
            DiscountValue = 50.00m,
            MinOrderAmount = 150.00m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true,
            TimesUsed = 0
        };
        context.Coupons.Add(coupon);
        await context.SaveChangesAsync();

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var couponRepo = new CouponRepository(context);
        var couponService = new CouponService(couponRepo);
        var notificationService = new FakeNotificationService();

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            null,
            notificationService,
            null,
            null,
            null,
            null,
            couponService,
            couponRepo);

        var dto = new CreateOrderDto
        {
            TableId = 1,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 1, Quantity = 1 }
            },
            CouponCode = "SAVE50",
            CustomerPhone = "9988776655"
        };

        var orderDto = await orderService.CreateAsync(dto, "valid-qr-token-123");

        Assert.NotNull(orderDto);
        Assert.Equal(200.00m, orderDto.SubtotalAmount);
        Assert.Equal(50.00m, orderDto.DiscountAmount);
        Assert.Equal("SAVE50", orderDto.CouponCode);

        // Net food = 150.00. Tax = 5% of 150 = 7.50. Platform fee = 5.00. Total = 162.50
        Assert.Equal(7.50m, orderDto.TaxAmount);
        Assert.Equal(5.00m, orderDto.PlatformFeeAmount);
        Assert.Equal(162.50m, orderDto.TotalAmount);

        // Verify coupon TimesUsed incremented in DB
        var updatedCoupon = await couponRepo.GetByIdAsync(1);
        Assert.NotNull(updatedCoupon);
        Assert.Equal(1, updatedCoupon.TimesUsed);

        // Verify CouponRedemption record was persisted
        var redemptions = await context.CouponRedemptions.ToListAsync();
        Assert.Single(redemptions);
        Assert.Equal("9988776655", redemptions[0].CustomerIdentifier);
        Assert.Equal(50.00m, redemptions[0].DiscountAmountApplied);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidCoupon_ThrowsAndDoesNotDeductStock()
    {
        using var context = CreateInMemoryDbContext();

        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = 1,
            Name = "Tech Park Canteen",
            IsActive = true
        };
        context.Canteens.Add(canteen);

        var table = new CanteenTable
        {
            TableId = 1,
            CanteenId = 1,
            TableNumber = 1,
            QRToken = "valid-qr-token-123",
            IsAvailable = true
        };
        context.Tables.Add(table);

        var product = new Product
        {
            ProductId = 1,
            CanteenId = 1,
            Name = "Paneer Tikka",
            Price = 180.00m,
            IsAvailable = true,
            StockQuantity = 20
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var couponRepo = new CouponRepository(context);
        var couponService = new CouponService(couponRepo);
        var notificationService = new FakeNotificationService();

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            null,
            notificationService,
            null,
            null,
            null,
            null,
            couponService,
            couponRepo);

        var dto = new CreateOrderDto
        {
            TableId = 1,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 1, Quantity = 2 }
            },
            CouponCode = "NONEXISTENT_CODE"
        };

        await Assert.ThrowsAsync<InvalidOrderException>(() =>
            orderService.CreateAsync(dto, "valid-qr-token-123"));

        // Stock was not permanently deducted
        var checkProduct = await productRepo.GetByIdAsync(1, 1);
        Assert.NotNull(checkProduct);
        Assert.Equal(20, checkProduct.StockQuantity);
    }

    private class FakeNotificationService : IOrderNotificationService
    {
        public Task NotifyNewOrderPlacedAsync(OrderDto order) => Task.CompletedTask;
        public Task NotifyPaymentConfirmedAsync(int canteenId, int orderId, string orderNumber, decimal amount, DateTime paidAt) => Task.CompletedTask;
        public Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt) => Task.CompletedTask;
    }
}
