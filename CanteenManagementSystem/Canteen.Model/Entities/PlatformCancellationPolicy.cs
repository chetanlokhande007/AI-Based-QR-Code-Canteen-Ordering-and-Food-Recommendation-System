using System;

namespace Canteen.Model.Entities;

public class PlatformCancellationPolicy
{
    public int PolicyId { get; set; }

    public int PolicyVersion { get; set; } = 1;

    // Platform-wide customer cancellation toggle
    public bool AllowCustomerCancellation { get; set; } = true;

    // Cancellation permitted before food preparation starts
    public bool AllowCancellationBeforePreparation { get; set; } = true;

    // Strict prohibition of ordinary cancellation once kitchen changes status to InPreparation
    public bool StrictlyProhibitCancellationAfterPreparation { get; set; } = true;

    // Max window in minutes for customer cancellation (0 = unlimited until kitchen starts)
    public int PrePreparationCutoffMinutes { get; set; } = 5;

    // "RefundFoodSubtotalOnly" (platform fee retained) or "FullRefund"
    public string CustomerCancellationRefundMode { get; set; } = "RefundFoodSubtotalOnly";

    // When the canteen cancels because they cannot fulfill, 100% full refund (food + fee) is issued
    public bool RefundPlatformFeeOnCanteenCancellation { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UpdatedByOwnerId { get; set; }
}

