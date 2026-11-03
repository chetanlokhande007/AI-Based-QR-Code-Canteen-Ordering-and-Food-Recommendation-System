using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using CanteenManagementSystem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Canteen.Tests;

public class PaymentDashboardTests
{
    private CanteenDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "PaymentDashboardDb_" + Guid.NewGuid().ToString())
            .Options;

        return new CanteenDbContext(options);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsZeroMetrics_WhenNoPaymentsExist()
    {
        using var context = CreateInMemoryContext();
        var service = new PaymentDashboardService(context, NullLogger<PaymentDashboardService>.Instance);

        var summary = await service.GetDashboardSummaryAsync(1, "Last 7 Days");

        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalAmount);
        Assert.Equal(0, summary.TotalCount);
        Assert.Equal(0, summary.SuccessfulAmount);
        Assert.Equal(0, summary.SuccessfulCount);
        Assert.Equal(0m, summary.SuccessfulPercentage);
        Assert.Equal(0, summary.FailedAmount);
        Assert.Equal(0m, summary.FailedPercentage);
        Assert.Equal(0, summary.RefundedAmount);
        Assert.Equal(0m, summary.RefundedPercentage);
        Assert.Equal("Last 7 Days", summary.Period);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_CalculatesAggregatesAndPercentages_Correctly()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 10;
        var now = DateTime.UtcNow;

        // Add successful payment
        context.Payments.Add(new Payment
        {
            PaymentId = 1,
            CanteenId = canteenId,
            OrderId = 101,
            Amount = 24260m,
            Status = "Paid",
            PaymentMethod = "Online",
            RazorpayPaymentId = "pay_Qk7a2s91",
            CreatedAt = now.AddDays(-1),
            PaidAt = now.AddDays(-1)
        });

        // Add failed payment
        context.Payments.Add(new Payment
        {
            PaymentId = 2,
            CanteenId = canteenId,
            OrderId = 102,
            Amount = 480m,
            Status = "Failed",
            PaymentMethod = "Online",
            RazorpayPaymentId = "pay_fail_01",
            CreatedAt = now.AddDays(-2)
        });

        // Add refunded record
        context.Refunds.Add(new Refund
        {
            RefundId = 1,
            CanteenId = canteenId,
            OrderId = 103,
            PaymentId = 3,
            Amount = 120m,
            Status = "Succeeded",
            CreatedAt = now.AddDays(-1)
        });

        await context.SaveChangesAsync();

        var service = new PaymentDashboardService(context, NullLogger<PaymentDashboardService>.Instance);
        var summary = await service.GetDashboardSummaryAsync(canteenId, "Last 7 Days");

        Assert.NotNull(summary);
        Assert.Equal(24860m, summary.TotalAmount);
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(24260m, summary.SuccessfulAmount);
        Assert.Equal(1, summary.SuccessfulCount);
        Assert.Equal(97.6m, summary.SuccessfulPercentage); // 24260 / 24860 = 97.58% -> 97.6%
        Assert.Equal(480m, summary.FailedAmount);
        Assert.Equal(1.9m, summary.FailedPercentage); // 480 / 24860 = 1.93% -> 1.9%
        Assert.Equal(120m, summary.RefundedAmount);
        Assert.Equal(0.5m, summary.RefundedPercentage); // 120 / 24860 = 0.48% -> 0.5%
    }

    [Fact]
    public async Task GetTransactionsAsync_ReturnsPagedResultsWithSearchAndStatusFilter()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 5;
        var table = new CanteenTable { TableId = 1, CanteenId = canteenId, TableNumber = 4 };
        context.Tables.Add(table);

        var order1 = new Order
        {
            OrderId = 1,
            CanteenId = canteenId,
            OrderNumber = "ORD-1058",
            TableId = 1,
            Table = table,
            TotalAmount = 180m,
            Status = "Completed",
            PaymentStatus = "Paid"
        };
        var order2 = new Order
        {
            OrderId = 2,
            CanteenId = canteenId,
            OrderNumber = "ORD-1059",
            TableId = 1,
            Table = table,
            TotalAmount = 90m,
            Status = "Cancelled",
            PaymentStatus = "Failed"
        };
        context.Orders.AddRange(order1, order2);

        context.Payments.AddRange(
            new Payment
            {
                PaymentId = 1,
                CanteenId = canteenId,
                OrderId = 1,
                Order = order1,
                Amount = 180m,
                Status = "Paid",
                PaymentMethod = "Online",
                RazorpayPaymentId = "pay_Qk7a2s",
                CreatedAt = DateTime.UtcNow
            },
            new Payment
            {
                PaymentId = 2,
                CanteenId = canteenId,
                OrderId = 2,
                Order = order2,
                Amount = 90m,
                Status = "Failed",
                PaymentMethod = "Online",
                RazorpayPaymentId = "pay_Qk6z9m",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
        await context.SaveChangesAsync();

        var service = new PaymentDashboardService(context, NullLogger<PaymentDashboardService>.Instance);

        // Search for ORD-1058
        var searchResult = await service.GetTransactionsAsync(canteenId, 1, 10, "ORD-1058", null, null, "All");
        Assert.Single(searchResult.Items);
        Assert.Equal("ORD-1058", searchResult.Items[0].OrderNumber);
        Assert.Equal("Success", searchResult.Items[0].Status);

        // Filter by failed
        var failedResult = await service.GetTransactionsAsync(canteenId, 1, 10, null, "Failed", null, "All");
        Assert.Single(failedResult.Items);
        Assert.Equal("ORD-1059", failedResult.Items[0].OrderNumber);
        Assert.Equal("Failed", failedResult.Items[0].Status);
    }

    [Fact]
    public async Task GetSettlementSummaryAsync_CalculatesAvailableBalanceAndMasksAccount()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 8;

        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = canteenId,
            Name = "Metro Cafeteria",
            BankName = "HDFC Bank",
            AccountNumber = "123456789012",
            IfscCode = "HDFC0001234",
            BankAccountHolder = "Metro Foods LLP",
            AccountType = "Current",
            UpiId = "metro@hdfcbank"
        };
        context.Canteens.Add(canteen);

        context.Orders.Add(new Order
        {
            OrderId = 50,
            CanteenId = canteenId,
            TotalAmount = 25000m,
            PlatformFeeAmount = 500m,
            CanteenEarningsAmount = 24500m,
            PaymentStatus = "Paid"
        });

        context.Refunds.Add(new Refund
        {
            RefundId = 20,
            CanteenId = canteenId,
            Amount = 1660m,
            Status = "Succeeded"
        });

        await context.SaveChangesAsync();

        var service = new PaymentDashboardService(context, NullLogger<PaymentDashboardService>.Instance);
        var settlement = await service.GetSettlementSummaryAsync(canteenId);

        Assert.NotNull(settlement);
        Assert.Equal(25000m, settlement.GrossSales);
        Assert.Equal(500m, settlement.PlatformFeesDeducted);
        Assert.Equal(1660m, settlement.TotalRefundsDeducted);
        Assert.Equal(22840m, settlement.NetSettlementAmount);
        Assert.Equal(22840m, settlement.AvailableForSettlement);
        Assert.True(settlement.IsBankConfigured);
        Assert.Equal("••••9012", settlement.AccountNumberMasked);
        Assert.Equal("Ready for Payout", settlement.SettlementStatus);
    }

    [Fact]
    public async Task UpdatePaymentSettingsAsync_SavesNewBankAndPolicyDetails()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 15;

        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = canteenId,
            Name = "Campus Hub"
        };
        context.Canteens.Add(canteen);
        await context.SaveChangesAsync();

        var service = new PaymentDashboardService(context, NullLogger<PaymentDashboardService>.Instance);

        var updateDto = new UpdatePaymentSettingsDto
        {
            BankName = "State Bank of India",
            AccountNumber = "987654321098",
            IfscCode = "sbin0004321",
            BankAccountHolder = "Campus Treats",
            AccountType = "Current",
            UpiId = "campus@sbi",
            FeeAbsorptionPolicy = "CanteenAbsorbs",
            TaxPercentage = 5.0m
        };

        var result = await service.UpdatePaymentSettingsAsync(canteenId, updateDto);

        Assert.Equal("State Bank of India", result.BankName);
        Assert.Equal("987654321098", result.AccountNumber);
        Assert.Equal("SBIN0004321", result.IfscCode); // uppercase normalized
        Assert.Equal("CanteenAbsorbs", result.FeeAbsorptionPolicy);
        Assert.Equal(5.0m, result.TaxPercentage);
    }

    [Fact]
    public async Task ExportStatementCsvAsync_GeneratesNonEmptyCsvBytes()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 22;

        var table = new CanteenTable { TableId = 22, CanteenId = canteenId, TableNumber = 1 };
        context.Tables.Add(table);

        var order = new Order
        {
            OrderId = 999,
            CanteenId = canteenId,
            TableId = 22,
            Table = table,
            OrderNumber = "ORD-9999",
            TotalAmount = 150m,
            PaymentStatus = "Paid"
        };
        context.Orders.Add(order);

        context.Payments.Add(new Payment
        {
            PaymentId = 99,
            CanteenId = canteenId,
            OrderId = 999,
            Order = order,
            Amount = 150m,
            Status = "Paid",
            PaymentMethod = "Online",
            RazorpayPaymentId = "pay_test_csv",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();

        var service = new PaymentDashboardService(context, NullLogger<PaymentDashboardService>.Instance);
        var csvBytes = await service.ExportStatementCsvAsync(canteenId, "Last 7 Days");

        Assert.NotEmpty(csvBytes);
        var csvString = Encoding.UTF8.GetString(csvBytes);
        Assert.Contains("Payment ID,Order Number", csvString);
        Assert.Contains("pay_test_csv", csvString);
    }

    [Fact]
    public async Task ReconcilePaymentsAsync_MarksCancelledOrderPendingPaymentsAsFailed()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 33;

        var cancelledOrder = new Order
        {
            OrderId = 88,
            CanteenId = canteenId,
            Status = "Cancelled",
            PaymentStatus = "Pending"
        };
        context.Orders.Add(cancelledOrder);

        context.Payments.Add(new Payment
        {
            PaymentId = 55,
            CanteenId = canteenId,
            OrderId = 88,
            Order = cancelledOrder,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30) // older than 15 mins
        });
        await context.SaveChangesAsync();

        var service = new PaymentDashboardService(context, NullLogger<PaymentDashboardService>.Instance);
        var result = await service.ReconcilePaymentsAsync(canteenId);

        Assert.Equal(1, result.TotalChecked);
        Assert.Equal(1, result.Reconciled);

        var payment = await context.Payments.FindAsync(55);
        Assert.Equal("Failed", payment!.Status);
        Assert.Contains("cancelled", payment.FailureReason, StringComparison.OrdinalIgnoreCase);
    }
}

