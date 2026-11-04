using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class CouponReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Core Metrics
    public int TotalActiveCoupons { get; set; }
    public int TotalRedemptionsCount { get; set; }
    public decimal TotalDiscountGiven { get; set; }
    public decimal TotalOrderSalesWithDiscount { get; set; }
    public decimal AverageDiscountPerRedemption { get; set; }
    public decimal PromotionalRoiRatio { get; set; } // Sales / Discount

    // Coupon Campaign Performance
    public List<CouponCampaignPerformanceDto> Campaigns { get; set; } = new();

    // Paginated Detailed Redemption Records
    public ReportPagedResult<CouponRedemptionReportItemDto> Redemptions { get; set; } = new();
}

public class CouponCampaignPerformanceDto
{
    public int CouponId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public int RedemptionsCount { get; set; }
    public decimal TotalDiscountGiven { get; set; }
    public decimal TotalSalesGenerated { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class CouponRedemptionReportItemDto
{
    public int RedemptionId { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerIdentifier { get; set; } = string.Empty;
    public decimal DiscountAmountApplied { get; set; }
    public decimal OrderTotal { get; set; }
    public DateTime RedeemedAt { get; set; }
}

