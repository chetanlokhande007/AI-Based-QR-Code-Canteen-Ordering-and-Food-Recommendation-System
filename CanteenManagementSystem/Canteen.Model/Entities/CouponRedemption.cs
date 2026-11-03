using System;

namespace Canteen.Model.Entities;

public class CouponRedemption
{
    public int RedemptionId { get; set; }

    public int CouponId { get; set; }

    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    /// <summary>
    /// Identifier for the customer who redeemed the coupon (e.g. phone number or account ID).
    /// </summary>
    public string CustomerIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Exact financial discount amount subtracted from the order's food subtotal.
    /// </summary>
    public decimal DiscountAmountApplied { get; set; }

    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Coupon Coupon { get; set; } = null!;

    public Order Order { get; set; } = null!;

    public Canteen Canteen { get; set; } = null!;
}

