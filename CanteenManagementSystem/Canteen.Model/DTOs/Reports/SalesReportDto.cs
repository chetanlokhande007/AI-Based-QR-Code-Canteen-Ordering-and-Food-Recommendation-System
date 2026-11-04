using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class SalesReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Aggregate Financials
    public decimal GrossSales { get; set; }
    public decimal NetSales { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalTaxCollected { get; set; }
    public decimal TotalPlatformFees { get; set; }
    public decimal TotalCanteenEarnings { get; set; }
    public decimal TotalRefunds { get; set; }
    public int SuccessfulOrdersCount { get; set; }
    public decimal AverageOrderValue { get; set; }

    // Period-over-Period Growth
    public decimal GrossSalesGrowthPercent { get; set; }
    public decimal NetSalesGrowthPercent { get; set; }
    public decimal OrdersGrowthPercent { get; set; }

    // Trend Visualizations
    public List<SalesDailyTimelinePointDto> DailyTimeline { get; set; } = new();

    // Payment Methods Breakdown
    public List<SalesPaymentMethodBreakdownDto> PaymentMethodSummary { get; set; } = new();

    // Category Sales Performance
    public List<SalesCategoryBreakdownDto> CategorySales { get; set; } = new();

    // Paginated Detailed Transaction Ledger
    public ReportPagedResult<SaleTransactionDetailDto> Transactions { get; set; } = new();
}

public class SalesDailyTimelinePointDto
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public decimal GrossSales { get; set; }
    public decimal NetSales { get; set; }
    public int OrderCount { get; set; }
    public decimal TaxCollected { get; set; }
    public decimal DiscountsGiven { get; set; }
    public decimal RefundsIssued { get; set; }
}

public class SalesPaymentMethodBreakdownDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public int TransactionsCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal SharePercentage { get; set; }
}

public class SalesCategoryBreakdownDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal TotalSales { get; set; }
    public decimal SharePercentage { get; set; }
}

public class SaleTransactionDetailDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

