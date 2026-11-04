using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class OverviewReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Core Financial & Operational KPIs
    public decimal GrossRevenue { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalRefunds { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int ActiveProductsCount { get; set; }

    // Comparison vs Previous Baseline Period (%)
    public decimal GrossRevenueGrowthPercent { get; set; }
    public decimal NetRevenueGrowthPercent { get; set; }
    public decimal OrdersGrowthPercent { get; set; }
    public decimal AverageOrderValueGrowthPercent { get; set; }

    // Chart Time-Series Data
    public List<ReportTimeSeriesPointDto> RevenueTrend { get; set; } = new();

    // Distribution Breakdowns
    public List<ReportDistributionDto> OrderStatusBreakdown { get; set; } = new();
    public List<ReportDistributionDto> PaymentMethodBreakdown { get; set; } = new();
    public List<TopProductSummaryDto> TopSellingProducts { get; set; } = new();
}

public class ReportTimeSeriesPointDto
{
    public string DateLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal GrossSales { get; set; }
    public decimal NetSales { get; set; }
    public int OrderCount { get; set; }
}

public class ReportDistributionDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
    public decimal TotalValue { get; set; }
}

public class TopProductSummaryDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal TotalSales { get; set; }
}

