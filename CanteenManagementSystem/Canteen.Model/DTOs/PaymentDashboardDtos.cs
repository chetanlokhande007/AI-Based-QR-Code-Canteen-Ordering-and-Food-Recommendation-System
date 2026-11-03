using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs;

public class PaymentDashboardSummaryDto
{
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public decimal SuccessfulAmount { get; set; }
    public int SuccessfulCount { get; set; }
    public decimal SuccessfulPercentage { get; set; }
    public decimal FailedAmount { get; set; }
    public int FailedCount { get; set; }
    public decimal FailedPercentage { get; set; }
    public decimal RefundedAmount { get; set; }
    public int RefundedCount { get; set; }
    public decimal RefundedPercentage { get; set; }
    public decimal? ComparisonPercentage { get; set; }
    public string ComparisonLabel { get; set; } = "vs previous period";
    public string Period { get; set; } = "Last 7 Days";
}

public class PaymentTransactionDto
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = "Customer";
    public string CustomerInitials { get; set; } = "CU";
    public string? CustomerPhone { get; set; }
    public string? TableNumber { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string PaymentMethod { get; set; } = "Razorpay";
    public string Status { get; set; } = "Success";
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class PaymentPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class CanteenSettlementSummaryDto
{
    public decimal AvailableForSettlement { get; set; }
    public decimal GrossSales { get; set; }
    public decimal PlatformFeesDeducted { get; set; }
    public decimal TotalRefundsDeducted { get; set; }
    public decimal NetSettlementAmount { get; set; }
    public string SettlementStatus { get; set; } = "Ready for Payout";
    public string? BankName { get; set; }
    public string? AccountNumberMasked { get; set; }
    public string? IfscCode { get; set; }
    public string? AccountHolder { get; set; }
    public string? AccountType { get; set; }
    public string? UpiId { get; set; }
    public bool IsBankConfigured { get; set; }
    public DateTime? NextSettlementDate { get; set; }
    public string PayoutSchedule { get; set; } = "Daily (Auto T+1)";
}

public class PaymentMethodConfigDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string BadgeText { get; set; } = "Active";
}

public class CanteenPaymentSettingsDto
{
    public int CanteenId { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? BankAccountHolder { get; set; }
    public string? AccountType { get; set; }
    public string? UpiId { get; set; }
    public string FeeAbsorptionPolicy { get; set; } = "CustomerPays";
    public decimal TaxPercentage { get; set; }
}

public class UpdatePaymentSettingsDto
{
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? BankAccountHolder { get; set; }
    public string? AccountType { get; set; }
    public string? UpiId { get; set; }
    public string? FeeAbsorptionPolicy { get; set; }
    public decimal? TaxPercentage { get; set; }
}

public class PaymentReconciliationResultDto
{
    public int TotalChecked { get; set; }
    public int Reconciled { get; set; }
    public int DiscrepanciesFound { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ReconciledAt { get; set; } = DateTime.UtcNow;
}

