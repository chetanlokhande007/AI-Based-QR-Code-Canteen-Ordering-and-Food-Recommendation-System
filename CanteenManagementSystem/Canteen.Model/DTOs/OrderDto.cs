using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs;

public class OrderDto
{
    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    public int TableId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    // Marketplace breakdown
    public decimal SubtotalAmount { get; set; }

    // Promotional Coupon Discount
    public decimal DiscountAmount { get; set; } = 0.00m;

    public string? CouponCode { get; set; }

    public int? CouponId { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal PlatformFeeAmount { get; set; } = 5.00m;

    public string PlatformFeePolicy { get; set; } = "CustomerPays";

    public decimal TotalAmount { get; set; }

    public decimal CanteenEarningsAmount { get; set; }

    public string TrackingToken { get; set; } = string.Empty;

    public decimal? RefundedAmount { get; set; }

    public string? CancellationNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal { get; set; }
}
