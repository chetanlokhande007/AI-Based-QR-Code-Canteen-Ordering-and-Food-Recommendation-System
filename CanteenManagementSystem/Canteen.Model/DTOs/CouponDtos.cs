using System;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class CouponDto
{
    public int CouponId { get; set; }

    public int? CanteenId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DiscountType { get; set; } = "Percentage"; // "Percentage" or "FixedAmount"

    public decimal DiscountValue { get; set; }

    public decimal MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public int UsageLimitPerCustomer { get; set; }

    public int? TotalUsageLimit { get; set; }

    public int TimesUsed { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow > EndDate;

    public bool IsAvailable => IsActive && DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate
        && (!TotalUsageLimit.HasValue || TimesUsed < TotalUsageLimit.Value);
}

public class CreateCouponDto
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Coupon code may only contain alphanumeric characters, hyphens, and underscores.")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^(Percentage|FixedAmount)$", ErrorMessage = "DiscountType must be 'Percentage' or 'FixedAmount'.")]
    public string DiscountType { get; set; } = "Percentage";

    [Required]
    [Range(0.01, 100000.00, ErrorMessage = "Discount value must be greater than 0.")]
    public decimal DiscountValue { get; set; }

    [Range(0, 100000.00, ErrorMessage = "MinOrderAmount must be non-negative.")]
    public decimal MinOrderAmount { get; set; } = 0.00m;

    [Range(0, 100000.00, ErrorMessage = "MaxDiscountAmount must be non-negative.")]
    public decimal? MaxDiscountAmount { get; set; }

    [Range(1, 1000, ErrorMessage = "UsageLimitPerCustomer must be at least 1.")]
    public int UsageLimitPerCustomer { get; set; } = 1;

    [Range(1, 1000000, ErrorMessage = "TotalUsageLimit must be at least 1 if set.")]
    public int? TotalUsageLimit { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(30);

    public bool IsActive { get; set; } = true;
}

public class UpdateCouponDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^(Percentage|FixedAmount)$", ErrorMessage = "DiscountType must be 'Percentage' or 'FixedAmount'.")]
    public string DiscountType { get; set; } = "Percentage";

    [Required]
    [Range(0.01, 100000.00, ErrorMessage = "Discount value must be greater than 0.")]
    public decimal DiscountValue { get; set; }

    [Range(0, 100000.00, ErrorMessage = "MinOrderAmount must be non-negative.")]
    public decimal MinOrderAmount { get; set; } = 0.00m;

    [Range(0, 100000.00, ErrorMessage = "MaxDiscountAmount must be non-negative.")]
    public decimal? MaxDiscountAmount { get; set; }

    [Range(1, 1000, ErrorMessage = "UsageLimitPerCustomer must be at least 1.")]
    public int UsageLimitPerCustomer { get; set; } = 1;

    [Range(1, 1000000, ErrorMessage = "TotalUsageLimit must be at least 1 if set.")]
    public int? TotalUsageLimit { get; set; }

    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; }
}

public class ValidateCouponRequestDto
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Range(0.01, 100000.00)]
    public decimal FoodSubtotal { get; set; }

    public string? CustomerIdentifier { get; set; }
}

public class ValidateCouponResponseDto
{
    public bool IsValid { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Title { get; set; }

    public string? DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal NetFoodSubtotal { get; set; }

    public decimal MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }
}

public class PublicCouponDto
{
    public int CouponId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DiscountType { get; set; } = "Percentage";

    public decimal DiscountValue { get; set; }

    public decimal MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public DateTime EndDate { get; set; }
}

public class CouponRedemptionReportDto
{
    public int TotalCoupons { get; set; }

    public int ActiveCoupons { get; set; }

    public int TotalRedemptions { get; set; }

    public decimal TotalDiscountGiven { get; set; }

    public decimal TotalSalesWithDiscount { get; set; }
}

