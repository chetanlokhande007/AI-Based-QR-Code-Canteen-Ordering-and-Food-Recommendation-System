using System;

namespace Canteen.Model.DTOs;

public class RefundDto
{
    public int RefundId { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int PaymentId { get; set; }
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string RefundMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? GatewayRefundId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string InitiatedByRole { get; set; } = string.Empty;
    public string? FailureMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class ProcessRefundRequestDto
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RefundResultDto
{
    public bool Success { get; set; }
    public string? GatewayRefundId { get; set; }
    public string Status { get; set; } = "Succeeded";
    public string? FailureReason { get; set; }
    public bool IsSimulated { get; set; }
}

public class RefundMetricsSummaryDto
{
    public decimal TotalRefundAmount { get; set; }
    public int TotalRefundsCount { get; set; }
    public int PendingCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
}
