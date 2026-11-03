using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities;

public class Coupon
{
    public int CouponId { get; set; }

    /// <summary>
    /// If null, this coupon is a platform-wide campaign managed by Super Admin.
    /// If set, the coupon is strictly scoped to this authorized canteen.
    /// </summary>
    public int? CanteenId { get; set; }

    /// <summary>
    /// Unique promotional code (e.g., "WELCOME20", "FLAT50", "FESTIVE100").
    /// Normalized to uppercase.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Discount type: "Percentage" or "FixedAmount".
    /// </summary>
    public string DiscountType { get; set; } = "Percentage";

    /// <summary>
    /// Discount value (e.g. 20.00 for 20%, or 50.00 for ₹50.00 flat discount).
    /// </summary>
    public decimal DiscountValue { get; set; }

    /// <summary>
    /// Minimum food subtotal required before the coupon can be applied (e.g., ₹100.00).
    /// </summary>
    public decimal MinOrderAmount { get; set; } = 0.00m;

    /// <summary>
    /// Maximum cap on discount for percentage-based coupons (e.g. 20% off up to max ₹60.00).
    /// Null or 0 means uncapped.
    /// </summary>
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>
    /// Maximum times a single customer (identified by phone number/user) can redeem this coupon.
    /// Default is 1.
    /// </summary>
    public int UsageLimitPerCustomer { get; set; } = 1;

    /// <summary>
    /// Global total redemption cap across all customers (e.g. first 200 redemptions).
    /// Null means unlimited.
    /// </summary>
    public int? TotalUsageLimit { get; set; }

    /// <summary>
    /// Current count of successful order redemptions.
    /// </summary>
    public int TimesUsed { get; set; } = 0;

    /// <summary>
    /// Start date and time from when this coupon becomes active (UTC).
    /// </summary>
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiry date and time when this coupon ceases to be valid (UTC).
    /// </summary>
    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(30);

    /// <summary>
    /// Status switch to activate/deactivate the coupon instantly.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Canteen? Canteen { get; set; }

    public ICollection<CouponRedemption> Redemptions { get; set; } = new List<CouponRedemption>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

