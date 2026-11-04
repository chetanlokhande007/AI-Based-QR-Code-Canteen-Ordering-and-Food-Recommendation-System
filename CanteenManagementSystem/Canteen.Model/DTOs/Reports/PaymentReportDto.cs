using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class PaymentReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Core Metrics
    public decimal TotalGrossCollections { get; set; }
    public decimal OnlineCollections { get; set; }
    public decimal CounterCashCollections { get; set; }
    public decimal TotalRefundedAmount { get; set; }
    public decimal NetSettlementAmount { get; set; } // Collections - Refunds
    public int TotalTransactions { get; set; }
    public int SuccessfulPaymentsCount { get; set; }
    public int FailedPaymentsCount { get; set; }
    public int RefundsCount { get; set; }
    public decimal PaymentSuccessRate { get; set; }

    // Gateway & Method Distribution
    public List<PaymentMethodLedgerSummaryDto> MethodsSummary { get; set; } = new();

    // Refunds Analysis
    public List<RefundBreakdownDto> RefundsByRole { get; set; } = new();

    // Paginated Detailed Payment Ledger
    public ReportPagedResult<PaymentLedgerItemDto> Payments { get; set; } = new();
}

public class PaymentMethodLedgerSummaryDto
{
    public string Method { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetCollected { get; set; }
    public decimal SharePercentage { get; set; }
}

public class RefundBreakdownDto
{
    public string InitiatedByRole { get; set; } = string.Empty;
    public int RefundCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
}

public class PaymentLedgerItemDto
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RazorpayPaymentId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal RefundedAmount { get; set; }
    public string RefundStatus { get; set; } = "None"; // "None", "Partial", "Full"
}

