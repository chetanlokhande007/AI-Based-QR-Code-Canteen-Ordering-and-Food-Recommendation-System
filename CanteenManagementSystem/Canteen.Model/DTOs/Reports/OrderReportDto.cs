using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class OrderReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Aggregate Order Metrics
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int InProgressOrders { get; set; } // Pending, Preparing, Ready
    public decimal FulfillmentRate { get; set; } // % completed
    public decimal CancellationRate { get; set; } // % cancelled
    public decimal TotalOrderValue { get; set; }
    public decimal CancelledValue { get; set; }

    // Status Breakdown
    public List<ReportDistributionDto> StatusBreakdown { get; set; } = new();

    // Cancellation Reasons Breakdown
    public List<CancellationReasonStatDto> CancellationReasons { get; set; } = new();

    // Order Volume Timeline
    public List<OrderTimelinePointDto> Timeline { get; set; } = new();

    // Paginated Detailed Order Records
    public ReportPagedResult<OrderDetailReportItemDto> Orders { get; set; } = new();
}

public class CancellationReasonStatDto
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
    public decimal TotalRefundedAmount { get; set; }
}

public class OrderTimelinePointDto
{
    public string DateLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
}

public class OrderDetailReportItemDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

