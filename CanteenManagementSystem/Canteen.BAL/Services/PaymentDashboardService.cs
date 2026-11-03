using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Canteen.BAL.Services;

public class PaymentDashboardService : IPaymentDashboardService
{
    private readonly CanteenDbContext _context;
    private readonly ILogger<PaymentDashboardService> _logger;

    public PaymentDashboardService(CanteenDbContext context, ILogger<PaymentDashboardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentDashboardSummaryDto> GetDashboardSummaryAsync(int canteenId, string? dateRange)
    {
        var (fromDate, toDate, prevFromDate, prevToDate, periodLabel) = ResolveDateRanges(dateRange);

        // Fetch payments in current window
        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.CanteenId == canteenId && p.CreatedAt >= fromDate && p.CreatedAt <= toDate)
            .ToListAsync();

        // Fetch refunds in current window
        var refunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == canteenId && r.CreatedAt >= fromDate && r.CreatedAt <= toDate && r.Status == "Succeeded")
            .ToListAsync();

        var successfulPayments = payments.Where(p => string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(p.Status, "Success", StringComparison.OrdinalIgnoreCase)).ToList();
        var failedPayments = payments.Where(p => string.Equals(p.Status, "Failed", StringComparison.OrdinalIgnoreCase)).ToList();

        decimal successfulAmount = successfulPayments.Sum(p => p.Amount);
        int successfulCount = successfulPayments.Count;

        decimal failedAmount = failedPayments.Sum(p => p.Amount);
        int failedCount = failedPayments.Count;

        decimal refundedAmount = refunds.Sum(r => r.Amount);
        int refundedCount = refunds.Count;

        decimal totalAmount = successfulAmount + failedAmount + refundedAmount;
        int totalCount = successfulCount + failedCount + refundedCount;

        decimal successfulPct = totalAmount > 0 ? Math.Round((successfulAmount / totalAmount) * 100m, 1) : 0m;
        decimal failedPct = totalAmount > 0 ? Math.Round((failedAmount / totalAmount) * 100m, 1) : 0m;
        decimal refundedPct = totalAmount > 0 ? Math.Round((refundedAmount / totalAmount) * 100m, 1) : 0m;

        // Fetch previous window for trend comparison
        var prevPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.CanteenId == canteenId && p.CreatedAt >= prevFromDate && p.CreatedAt <= prevToDate)
            .ToListAsync();

        var prevRefunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == canteenId && r.CreatedAt >= prevFromDate && r.CreatedAt <= prevToDate && r.Status == "Succeeded")
            .ToListAsync();

        decimal prevTotalAmount = prevPayments.Where(p => string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                                                          string.Equals(p.Status, "Success", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount)
                                  + prevPayments.Where(p => string.Equals(p.Status, "Failed", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount)
                                  + prevRefunds.Sum(r => r.Amount);

        decimal? comparisonPercentage = null;
        if (prevTotalAmount > 0)
        {
            comparisonPercentage = Math.Round(((totalAmount - prevTotalAmount) / prevTotalAmount) * 100m, 1);
        }
        else if (totalAmount > 0)
        {
            comparisonPercentage = 100m;
        }

        string comparisonLabel = periodLabel switch
        {
            "Today" => "vs yesterday",
            "Yesterday" => "vs day before",
            "Last 7 Days" => "vs previous 7 days",
            "Last 30 Days" => "vs previous 30 days",
            "This Month" => "vs previous month",
            _ => "vs previous period"
        };

        return new PaymentDashboardSummaryDto
        {
            TotalAmount = totalAmount,
            TotalCount = totalCount,
            SuccessfulAmount = successfulAmount,
            SuccessfulCount = successfulCount,
            SuccessfulPercentage = successfulPct,
            FailedAmount = failedAmount,
            FailedCount = failedCount,
            FailedPercentage = failedPct,
            RefundedAmount = refundedAmount,
            RefundedCount = refundedCount,
            RefundedPercentage = refundedPct,
            ComparisonPercentage = comparisonPercentage,
            ComparisonLabel = comparisonLabel,
            Period = periodLabel
        };
    }

    public async Task<PaymentPagedResult<PaymentTransactionDto>> GetTransactionsAsync(
        int canteenId,
        int page,
        int pageSize,
        string? search,
        string? status,
        string? paymentMethod,
        string? dateRange)
    {
        page = Math.Max(1, page);
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Order)
                .ThenInclude(o => o.Table)
            .Where(p => p.CanteenId == canteenId);

        if (!string.IsNullOrWhiteSpace(dateRange) && !string.Equals(dateRange, "All", StringComparison.OrdinalIgnoreCase))
        {
            var (fromDate, toDate, _, _, _) = ResolveDateRanges(dateRange);
            query = query.Where(p => p.CreatedAt >= fromDate && p.CreatedAt <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            var normStatus = status.Trim().ToLowerInvariant();
            if (normStatus is "success" or "paid")
            {
                query = query.Where(p => p.Status.ToLower() == "paid" || p.Status.ToLower() == "success");
            }
            else
            {
                query = query.Where(p => p.Status.ToLower() == normStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod) && !string.Equals(paymentMethod, "All", StringComparison.OrdinalIgnoreCase))
        {
            var normMethod = paymentMethod.Trim().ToLowerInvariant();
            if (normMethod.Contains("cash") || normMethod.Contains("counter"))
            {
                query = query.Where(p => p.PaymentMethod.ToLower() == "counter" || p.PaymentMethod.ToLower().Contains("cash"));
            }
            else
            {
                query = query.Where(p => p.PaymentMethod.ToLower().Contains(normMethod));
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                (p.Order != null && p.Order.OrderNumber.ToLower().Contains(s)) ||
                (p.RazorpayPaymentId != null && p.RazorpayPaymentId.ToLower().Contains(s)) ||
                (p.RazorpayOrderId != null && p.RazorpayOrderId.ToLower().Contains(s)) ||
                p.PaymentMethod.ToLower().Contains(s) ||
                (p.Order != null && p.Order.Table != null && p.Order.Table.TableNumber.ToString().Contains(s)));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.PaymentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var mapped = items.Select(p => MapToTransactionDto(p)).ToList();

        return new PaymentPagedResult<PaymentTransactionDto>
        {
            Items = mapped,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CanteenSettlementSummaryDto> GetSettlementSummaryAsync(int canteenId)
    {
        var canteen = await _context.Canteens
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CanteenId == canteenId);

        var paidOrders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CanteenId == canteenId && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Success"))
            .ToListAsync();

        var succeededRefunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == canteenId && r.Status == "Succeeded")
            .ToListAsync();

        decimal grossSales = paidOrders.Sum(o => o.TotalAmount);
        decimal platformFees = paidOrders.Sum(o => o.PlatformFeeAmount);
        decimal refundsDeducted = succeededRefunds.Sum(r => r.Amount);
        decimal canteenEarnings = paidOrders.Sum(o => o.CanteenEarningsAmount);
        decimal netSettlement = Math.Max(0, canteenEarnings - refundsDeducted);

        bool isConfigured = canteen != null && (!string.IsNullOrWhiteSpace(canteen.AccountNumber) || !string.IsNullOrWhiteSpace(canteen.UpiId));
        string maskedAccount = canteen?.AccountNumber != null && canteen.AccountNumber.Length > 4
            ? $"••••{canteen.AccountNumber[^4..]}"
            : (canteen?.AccountNumber ?? "Not Set");

        string status = netSettlement > 0
            ? (isConfigured ? "Ready for Payout" : "Bank Required")
            : "Settled";

        return new CanteenSettlementSummaryDto
        {
            AvailableForSettlement = netSettlement,
            GrossSales = grossSales,
            PlatformFeesDeducted = platformFees,
            TotalRefundsDeducted = refundsDeducted,
            NetSettlementAmount = netSettlement,
            SettlementStatus = status,
            BankName = canteen?.BankName,
            AccountNumberMasked = maskedAccount,
            IfscCode = canteen?.IfscCode,
            AccountHolder = canteen?.BankAccountHolder,
            AccountType = canteen?.AccountType ?? "Current",
            UpiId = canteen?.UpiId,
            IsBankConfigured = isConfigured,
            NextSettlementDate = DateTime.UtcNow.Date.AddDays(1),
            PayoutSchedule = "Daily (Auto T+1)"
        };
    }

    public Task<List<PaymentMethodConfigDto>> GetPaymentMethodsAsync(int canteenId)
    {
        var list = new List<PaymentMethodConfigDto>
        {
            new()
            {
                Id = "razorpay",
                Name = "Razorpay",
                Subtitle = "Online payments",
                Provider = "Razorpay Gateway",
                IsActive = true,
                BadgeText = "Active"
            },
            new()
            {
                Id = "upi",
                Name = "UPI",
                Subtitle = "GPay, PhonePe, Paytm etc.",
                Provider = "NPCI / Unified Payments",
                IsActive = true,
                BadgeText = "Active"
            },
            new()
            {
                Id = "cards",
                Name = "Cards",
                Subtitle = "Credit & Debit Cards",
                Provider = "Visa, Mastercard, RuPay",
                IsActive = true,
                BadgeText = "Active"
            },
            new()
            {
                Id = "counter",
                Name = "Pay at Counter",
                Subtitle = "Cash at collection counter",
                Provider = "Cash / On-premise",
                IsActive = true,
                BadgeText = "Active"
            }
        };

        return Task.FromResult(list);
    }

    public async Task<CanteenPaymentSettingsDto?> GetPaymentSettingsAsync(int canteenId)
    {
        var canteen = await _context.Canteens
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CanteenId == canteenId);

        if (canteen == null) return null;

        return new CanteenPaymentSettingsDto
        {
            CanteenId = canteen.CanteenId,
            BankName = canteen.BankName,
            AccountNumber = canteen.AccountNumber,
            IfscCode = canteen.IfscCode,
            BankAccountHolder = canteen.BankAccountHolder,
            AccountType = canteen.AccountType ?? "Current",
            UpiId = canteen.UpiId,
            FeeAbsorptionPolicy = canteen.FeeAbsorptionPolicy ?? "CustomerPays",
            TaxPercentage = canteen.TaxPercentage
        };
    }

    public async Task<CanteenPaymentSettingsDto> UpdatePaymentSettingsAsync(int canteenId, UpdatePaymentSettingsDto dto)
    {
        var canteen = await _context.Canteens.FirstOrDefaultAsync(c => c.CanteenId == canteenId);
        if (canteen == null)
            throw new KeyNotFoundException($"Canteen with ID {canteenId} was not found.");

        if (dto.BankName != null) canteen.BankName = dto.BankName.Trim();
        if (dto.AccountNumber != null) canteen.AccountNumber = dto.AccountNumber.Trim();
        if (dto.IfscCode != null) canteen.IfscCode = dto.IfscCode.Trim().ToUpperInvariant();
        if (dto.BankAccountHolder != null) canteen.BankAccountHolder = dto.BankAccountHolder.Trim();
        if (dto.AccountType != null) canteen.AccountType = dto.AccountType.Trim();
        if (dto.UpiId != null) canteen.UpiId = dto.UpiId.Trim();
        if (!string.IsNullOrWhiteSpace(dto.FeeAbsorptionPolicy)) canteen.FeeAbsorptionPolicy = dto.FeeAbsorptionPolicy.Trim();
        if (dto.TaxPercentage.HasValue) canteen.TaxPercentage = Math.Max(0, dto.TaxPercentage.Value);

        await _context.SaveChangesAsync();

        return new CanteenPaymentSettingsDto
        {
            CanteenId = canteen.CanteenId,
            BankName = canteen.BankName,
            AccountNumber = canteen.AccountNumber,
            IfscCode = canteen.IfscCode,
            BankAccountHolder = canteen.BankAccountHolder,
            AccountType = canteen.AccountType,
            UpiId = canteen.UpiId,
            FeeAbsorptionPolicy = canteen.FeeAbsorptionPolicy,
            TaxPercentage = canteen.TaxPercentage
        };
    }

    public async Task<byte[]> ExportStatementCsvAsync(int canteenId, string? dateRange)
    {
        var (fromDate, toDate, _, _, periodLabel) = ResolveDateRanges(dateRange);

        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Order)
                .ThenInclude(o => o.Table)
            .Where(p => p.CanteenId == canteenId && p.CreatedAt >= fromDate && p.CreatedAt <= toDate)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Payment ID,Order Number,Transaction ID,Customer / Table,Amount (INR),Payment Method,Status,Created At (UTC),Paid At (UTC)");

        foreach (var p in payments)
        {
            var dto = MapToTransactionDto(p);
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",\"{2}\",\"{3}\",{4:F2},\"{5}\",\"{6}\",\"{7:yyyy-MM-dd HH:mm:ss}\",\"{8}\"",
                dto.PaymentId,
                EscapeCsv(dto.OrderNumber),
                EscapeCsv(dto.TransactionId),
                EscapeCsv(dto.CustomerName),
                dto.Amount,
                EscapeCsv(dto.PaymentMethod),
                EscapeCsv(dto.Status),
                dto.CreatedAt,
                dto.PaidAt.HasValue ? dto.PaidAt.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<PaymentReconciliationResultDto> ReconcilePaymentsAsync(int canteenId)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var pendingPayments = await _context.Payments
            .Include(p => p.Order)
            .Where(p => p.CanteenId == canteenId && p.Status == "Pending" && p.CreatedAt <= cutoff)
            .ToListAsync();

        int checkedCount = pendingPayments.Count;
        int reconciledCount = 0;
        int discrepancies = 0;

        foreach (var payment in pendingPayments)
        {
            if (payment.Order != null && string.Equals(payment.Order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                payment.Status = "Failed";
                payment.FailureReason = "Order was cancelled prior to payment completion.";
                reconciledCount++;
            }
            else
            {
                discrepancies++;
            }
        }

        if (reconciledCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return new PaymentReconciliationResultDto
        {
            TotalChecked = checkedCount,
            Reconciled = reconciledCount,
            DiscrepanciesFound = discrepancies,
            Message = checkedCount == 0
                ? "All transactions are fully synchronized with gateway."
                : $"Reconciliation complete. Checked {checkedCount} transactions, updated {reconciledCount} records, {discrepancies} awaiting customer action."
        };
    }
    

    private static PaymentTransactionDto MapToTransactionDto(Payment p)
    {
        string status = p.Status switch
        {
            "Paid" => "Success",
            "Success" => "Success",
            "Failed" => "Failed",
            "Refunded" => "Refunded",
            _ => "Pending"
        };

        string tableLabel = p.Order?.Table != null ? $"Table {p.Order.Table.TableNumber}" : "Online Order";
        string customerName = !string.IsNullOrWhiteSpace(tableLabel) ? $"{tableLabel} Guest" : "Customer";
        string customerInitials = p.Order?.Table != null ? $"T{p.Order.Table.TableNumber}" : "CU";

        string method = p.PaymentMethod switch
        {
            "Online" => "Razorpay",
            "Counter" => "Cash",
            _ => p.PaymentMethod
        };

        string txnId = !string.IsNullOrWhiteSpace(p.RazorpayPaymentId)
            ? p.RazorpayPaymentId
            : (!string.IsNullOrWhiteSpace(p.RazorpayOrderId) ? p.RazorpayOrderId : $"txn_{p.PaymentId:D6}");

        return new PaymentTransactionDto
        {
            PaymentId = p.PaymentId,
            OrderId = p.OrderId,
            OrderNumber = p.Order?.OrderNumber ?? $"#ORD-{p.OrderId}",
            TransactionId = txnId,
            CustomerName = customerName,
            CustomerInitials = customerInitials,
            TableNumber = p.Order?.Table != null ? p.Order.Table.TableNumber.ToString() : null,
            Amount = p.Amount,
            Currency = p.Currency ?? "INR",
            PaymentMethod = method,
            Status = status,
            FailureReason = p.FailureReason,
            CreatedAt = p.CreatedAt,
            PaidAt = p.PaidAt
        };
    }

    private static (DateTime fromDate, DateTime toDate, DateTime prevFromDate, DateTime prevToDate, string periodLabel) ResolveDateRanges(string? dateRange)
    {
        var now = DateTime.UtcNow;
        var range = dateRange?.Trim() ?? "Last 7 Days";
        var bufferToDate = now.AddMinutes(5);

        switch (range.ToLowerInvariant())
        {
            case "all":
            {
                return (DateTime.MinValue, DateTime.MaxValue, DateTime.MinValue, DateTime.MinValue, "All");
            }
            case "today":
            {
                var startToday = now.Date;
                var startYesterday = startToday.AddDays(-1);
                return (startToday, bufferToDate, startYesterday, startToday, "Today");
            }
            case "yesterday":
            {
                var startYesterday = now.Date.AddDays(-1);
                var endYesterday = now.Date;
                var startDayBefore = startYesterday.AddDays(-1);
                return (startYesterday, endYesterday, startDayBefore, startYesterday, "Yesterday");
            }
            case "last 30 days":
            {
                var from = now.AddDays(-30);
                var prevFrom = now.AddDays(-60);
                return (from, bufferToDate, prevFrom, from, "Last 30 Days");
            }
            case "this month":
            {
                var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var prevFrom = from.AddMonths(-1);
                return (from, bufferToDate, prevFrom, from, "This Month");
            }
            case "last 7 days":
            default:
            {
                var from = now.AddDays(-7);
                var prevFrom = now.AddDays(-14);
                return (from, bufferToDate, prevFrom, from, "Last 7 Days");
            }
        }
    }

    private static string EscapeCsv(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("\"", "\"\"");
    }
}
