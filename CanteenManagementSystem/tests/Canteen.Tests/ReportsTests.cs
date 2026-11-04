using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs.Reports;
using Canteen.Model.Entities;
using CanteenManagementSystem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Canteen.Tests;

public class ReportsTests
{
    private CanteenDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "ReportsDb_" + Guid.NewGuid().ToString())
            .Options;

        return new CanteenDbContext(options);
    }

    private async Task SeedSampleCanteenData(CanteenDbContext context, int canteenId)
    {
        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = canteenId,
            OwnerId = 1,
            Name = "Campus Central Canteen",
            Slug = "campus-central",
            Location = "Main Block",
            TaxPercentage = 5.0m,
            IsActive = true,
            TenantStatus = Canteen.Model.Enums.TenantStatus.Active
        };
        context.Canteens.Add(canteen);

        var table1 = new CanteenTable { TableId = 1, CanteenId = canteenId, TableNumber = 101, Capacity = 4 };
        var table2 = new CanteenTable { TableId = 2, CanteenId = canteenId, TableNumber = 102, Capacity = 2 };
        context.Tables.AddRange(table1, table2);

        var cat1 = new Category { CategoryId = 1, CanteenId = canteenId, Name = "Beverages" };
        var cat2 = new Category { CategoryId = 2, CanteenId = canteenId, Name = "Meals" };
        context.Categories.AddRange(cat1, cat2);

        var p1 = new Product { ProductId = 1, CanteenId = canteenId, CategoryId = 1, Name = "Masala Chai", Price = 20.00m, StockQuantity = 100, IsAvailable = true };
        var p2 = new Product { ProductId = 2, CanteenId = canteenId, CategoryId = 2, Name = "Thali Deluxe", Price = 150.00m, StockQuantity = 25, IsAvailable = true };
        var p3 = new Product { ProductId = 3, CanteenId = canteenId, CategoryId = 2, Name = "Paneer Roll", Price = 80.00m, StockQuantity = 0, IsAvailable = false };
        context.Products.AddRange(p1, p2, p3);

        var now = DateTime.UtcNow;

        // Order 1: Completed
        var order1 = new Order
        {
            OrderId = 1,
            CanteenId = canteenId,
            TableId = 1,
            OrderNumber = "ORD-001",
            Status = "Completed",
            PaymentStatus = "Paid",
            SubtotalAmount = 190.00m, // 2x Chai + 1x Thali
            DiscountAmount = 0m,
            TaxAmount = 9.50m,
            PlatformFeeAmount = 5.00m,
            TotalAmount = 204.50m,
            CanteenEarningsAmount = 199.50m,
            CreatedAt = now.AddHours(-2)
        };
        order1.Items.Add(new OrderItem { OrderItemId = 1, OrderId = 1, ProductId = 1, Quantity = 2, UnitPrice = 20.00m });
        order1.Items.Add(new OrderItem { OrderItemId = 2, OrderId = 1, ProductId = 2, Quantity = 1, UnitPrice = 150.00m });

        // Order 2: Cancelled with Refund
        var order2 = new Order
        {
            OrderId = 2,
            CanteenId = canteenId,
            TableId = 2,
            OrderNumber = "ORD-002",
            Status = "Cancelled",
            PaymentStatus = "Refunded",
            CancellationReason = "Customer Changed Mind",
            SubtotalAmount = 80.00m,
            DiscountAmount = 0m,
            TaxAmount = 4.00m,
            PlatformFeeAmount = 5.00m,
            TotalAmount = 89.00m,
            CreatedAt = now.AddHours(-1)
        };

        context.Orders.AddRange(order1, order2);

        // Payments
        var pay1 = new Payment
        {
            PaymentId = 1,
            OrderId = 1,
            CanteenId = canteenId,
            Amount = 204.50m,
            PaymentMethod = "Online",
            Status = "Paid",
            RazorpayPaymentId = "pay_mock_123",
            PaidAt = now.AddHours(-2),
            CreatedAt = now.AddHours(-2)
        };
        var pay2 = new Payment
        {
            PaymentId = 2,
            OrderId = 2,
            CanteenId = canteenId,
            Amount = 89.00m,
            PaymentMethod = "Online",
            Status = "Paid",
            PaidAt = now.AddHours(-1),
            CreatedAt = now.AddHours(-1)
        };
        context.Payments.AddRange(pay1, pay2);

        // Refund for Order 2
        var refund1 = new Refund
        {
            RefundId = 1,
            OrderId = 2,
            PaymentId = 2,
            CanteenId = canteenId,
            Amount = 89.00m,
            Status = "Succeeded",
            Reason = "Customer Cancellation",
            InitiatedByRole = "Customer",
            CreatedAt = now.AddMinutes(-30)
        };
        context.Refunds.Add(refund1);

        // Inventory & Wastage
        var ing1 = new InventoryIngredient
        {
            IngredientId = 1,
            CanteenId = canteenId,
            Name = "Milk",
            Category = "Dairy",
            BaseUnit = "L",
            CurrentStock = 5.0m,
            MinimumStockLevel = 10.0m, // Low stock!
            CostPerBaseUnit = 60.00m
        };
        context.InventoryIngredients.Add(ing1);

        var wastage1 = new IngredientWastage
        {
            WastageId = 1,
            CanteenId = canteenId,
            IngredientId = 1,
            Quantity = 1.0m,
            Reason = "Spoiled",
            EstimatedCost = 60.00m,
            CreatedAt = now.AddHours(-3)
        };
        context.IngredientWastages.Add(wastage1);

        // Staff & Attendance
        var staff1 = new StaffMember
        {
            StaffId = 1,
            CanteenId = canteenId,
            EmployeeId = "EMP-01",
            FullName = "Ramesh Kumar",
            Role = "Chef",
            Shift = "Morning (6:00 AM - 2:00 PM)",
            Status = "Active",
            Salary = 25000.00m,
            JoinedOn = now.AddMonths(-6)
        };
        context.StaffMembers.Add(staff1);

        var att1 = new StaffAttendance
        {
            AttendanceId = 1,
            StaffId = 1,
            CanteenId = canteenId,
            Date = now.Date,
            CheckInTime = now.Date.AddHours(6),
            CheckOutTime = now.Date.AddHours(14),
            Status = "Present"
        };
        context.StaffAttendances.Add(att1);

        // Coupon
        var coupon1 = new Coupon
        {
            CouponId = 1,
            CanteenId = canteenId,
            Code = "WELCOME50",
            Title = "Welcome Offer",
            DiscountType = "FixedAmount",
            DiscountValue = 50.00m,
            IsActive = true
        };
        context.Coupons.Add(coupon1);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetOverviewReportAsync_ReturnsEmptyState_WhenNoDataExists()
    {
        using var context = CreateInMemoryContext();
        var repo = new ReportsRepository(context);
        var filter = new ReportFilterDto { CanteenId = 999, Period = "this_month" };

        var report = await repo.GetOverviewReportAsync(filter);

        Assert.NotNull(report);
        Assert.Equal(0m, report.GrossRevenue);
        Assert.Equal(0m, report.NetRevenue);
        Assert.Equal(0, report.TotalOrders);
        Assert.Equal(0, report.CompletedOrders);
        Assert.NotNull(report.RevenueTrend);
        Assert.NotNull(report.OrderStatusBreakdown);
    }

    [Fact]
    public async Task GetOverviewReportAsync_ComputesFinancialAndOperationalKPIs_Correctly()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);

        var filter = new ReportFilterDto { CanteenId = 1, Period = "today" };
        var report = await repo.GetOverviewReportAsync(filter);

        Assert.NotNull(report);
        Assert.Equal("Campus Central Canteen", report.CanteenName);
        Assert.Equal(204.50m, report.GrossRevenue);
        Assert.Equal(89.00m, report.TotalRefunds);
        Assert.Equal(115.50m, report.NetRevenue); // 204.50 - 89.00
        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(1, report.CompletedOrders);
        Assert.Equal(1, report.CancelledOrders);
        Assert.Equal(2, report.ActiveProductsCount); // Chai and Thali are active
        Assert.Equal(2, report.TopSellingProducts.Count); // Chai (sold 2) and Thali (sold 1)
    }

    [Fact]
    public async Task GetSalesReportAsync_CalculatesBreakdownsAndDeductions()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);

        var filter = new ReportFilterDto { CanteenId = 1, Period = "today" };
        var report = await repo.GetSalesReportAsync(filter);

        Assert.NotNull(report);
        Assert.Equal(204.50m, report.GrossSales);
        Assert.Equal(89.00m, report.TotalRefunds);
        Assert.Equal(115.50m, report.NetSales);
        Assert.Equal(1, report.SuccessfulOrdersCount);
        Assert.NotEmpty(report.CategorySales);
        Assert.Equal(2, report.Transactions.Items.Count);
    }

    [Fact]
    public async Task GetOrderReportAsync_CalculatesFulfillmentAndCancellationRates()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);

        var filter = new ReportFilterDto { CanteenId = 1, Period = "today" };
        var report = await repo.GetOrderReportAsync(filter);

        Assert.NotNull(report);
        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(1, report.CompletedOrders);
        Assert.Equal(1, report.CancelledOrders);
        Assert.Equal(50.0m, report.FulfillmentRate);
        Assert.Equal(50.0m, report.CancellationRate);
        Assert.Single(report.CancellationReasons);
        Assert.Equal("Customer Changed Mind", report.CancellationReasons[0].Reason);
    }

    [Fact]
    public async Task GetProductReportAsync_IdentifiesTopSellersAndOutOfStock()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);

        var filter = new ReportFilterDto { CanteenId = 1, Period = "today" };
        var report = await repo.GetProductReportAsync(filter);

        Assert.NotNull(report);
        Assert.Equal(3, report.TotalProductsInCatalog);
        Assert.Equal(2, report.ActiveProductsCount);
        Assert.Equal(1, report.OutOfStockCount); // Paneer roll stock = 0
        Assert.Equal(3, report.TotalUnitsSold); // 2 Chai + 1 Thali
        Assert.Equal(190.00m, report.TotalRevenueGenerated);
        Assert.NotEmpty(report.TopSellingProducts);
        Assert.Equal("Masala Chai", report.TopSellingProducts[0].Name);
    }

    [Fact]
    public async Task GetInventoryReportAsync_FlagsLowStockAndWastageCosts()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);

        var filter = new ReportFilterDto { CanteenId = 1, Period = "today" };
        var report = await repo.GetInventoryReportAsync(filter);

        Assert.NotNull(report);
        Assert.Equal(1, report.TotalIngredientsTracked);
        Assert.Equal(300.00m, report.TotalInventoryValuation); // 5L * 60
        Assert.Equal(1, report.LowStockAlertsCount);
        Assert.Equal(60.00m, report.TotalWastageCost);
        Assert.Single(report.LowStockItems);
        Assert.Equal("Milk", report.LowStockItems[0].Name);
        Assert.Equal(5.0m, report.LowStockItems[0].DeficitQuantity);
    }

    [Fact]
    public async Task GetStaffAndAttendanceReports_TracksPayrollAndAttendanceRate()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);

        var filter = new ReportFilterDto { CanteenId = 1, Period = "today" };
        var staffReport = await repo.GetStaffReportAsync(filter);
        var attendanceReport = await repo.GetAttendanceReportAsync(filter);

        Assert.NotNull(staffReport);
        Assert.Equal(1, staffReport.TotalStaffCount);
        Assert.Equal(25000.00m, staffReport.TotalMonthlyPayroll);
        Assert.Equal(100.0m, staffReport.OverallAttendanceRate);

        Assert.NotNull(attendanceReport);
        Assert.Equal(1, attendanceReport.TotalPresent);
        Assert.Equal(0, attendanceReport.TotalAbsent);
        Assert.Equal(100.0m, attendanceReport.OverallAttendanceRate);
    }

    [Fact]
    public async Task GetPeakHourReportAsync_IdentifiesTrafficPatterns()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);

        var filter = new ReportFilterDto { CanteenId = 1, Period = "today" };
        var report = await repo.GetPeakHourReportAsync(filter);

        Assert.NotNull(report);
        Assert.Equal(24, report.HourlyTraffic.Count);
        Assert.Equal(7, report.DayOfWeekTraffic.Count);
        Assert.NotEmpty(report.BusiestHourLabel);
    }

    [Fact]
    public async Task ExportReportAsync_GeneratesValidCsvAndExcelBytes()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);
        var service = new ReportsService(repo);

        // Test CSV export
        var csvReq = new ExportReportRequestDto { CanteenId = 1, ReportType = "sales", Format = "csv", Period = "today" };
        var csvResult = await service.ExportReportAsync(csvReq);

        Assert.NotNull(csvResult);
        Assert.Equal("text/csv", csvResult.ContentType);
        Assert.EndsWith(".csv", csvResult.FileName);
        Assert.NotEmpty(csvResult.FileBytes);

        var csvText = Encoding.UTF8.GetString(csvResult.FileBytes);
        Assert.Contains("Order #", csvText);
        Assert.Contains("ORD-001", csvText);

        // Test Excel export
        var excelReq = new ExportReportRequestDto { CanteenId = 1, ReportType = "overview", Format = "excel", Period = "today" };
        var excelResult = await service.ExportReportAsync(excelReq);

        Assert.NotNull(excelResult);
        Assert.Equal("application/vnd.ms-excel", excelResult.ContentType);
        Assert.EndsWith(".xls", excelResult.FileName);
        Assert.NotEmpty(excelResult.FileBytes);

        var xmlText = Encoding.UTF8.GetString(excelResult.FileBytes);
        Assert.Contains("<?xml version=\"1.0\"", xmlText);
        Assert.Contains("Workbook", xmlText);
        Assert.Contains("Gross Revenue", xmlText);
    }

    [Fact]
    public async Task ReportsController_EnforcesAuthorization()
    {
        using var context = CreateInMemoryContext();
        await SeedSampleCanteenData(context, 1);
        var repo = new ReportsRepository(context);
        var service = new ReportsService(repo);

        var canteenRepo = new CanteenRepository(context);
        var mockCanteenService = new CanteenService(canteenRepo, null!, null!, null!, null!);

        var controller = new ReportsController(service, mockCanteenService);

        // Simulate unauthorized user (no claims)
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetOverviewReport(new ReportFilterDto { CanteenId = 0 });
        Assert.IsType<BadRequestObjectResult>(result);

        // Simulate authenticated CanteenOwner for canteen 1
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "CanteenOwner"),
            new Claim("CanteenId", "1")
        }, "TestAuth"));

        controller.ControllerContext.HttpContext.User = user;

        // Valid canteen access
        var okResult = await controller.GetOverviewReport(new ReportFilterDto { CanteenId = 1 });
        Assert.IsType<OkObjectResult>(okResult);

        // Forbidden canteen access (trying to access canteen 2)
        var forbidResult = await controller.GetOverviewReport(new ReportFilterDto { CanteenId = 2 });
        Assert.IsType<ForbidResult>(forbidResult);
    }
}
