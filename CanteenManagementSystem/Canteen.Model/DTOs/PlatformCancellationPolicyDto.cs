using System;

namespace Canteen.Model.DTOs;

public class PlatformCancellationPolicyDto
{
    public int PolicyId { get; set; }
    public int PolicyVersion { get; set; } = 1;
    public bool AllowCustomerCancellation { get; set; } = true;
    public bool AllowCancellationBeforePreparation { get; set; } = true;
    public bool StrictlyProhibitCancellationAfterPreparation { get; set; } = true;
    public int PrePreparationCutoffMinutes { get; set; } = 5;
    public string CustomerCancellationRefundMode { get; set; } = "RefundFoodSubtotalOnly";
    public bool RefundPlatformFeeOnCanteenCancellation { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdatePlatformCancellationPolicyDto
{
    public bool AllowCustomerCancellation { get; set; } = true;
    public bool AllowCancellationBeforePreparation { get; set; } = true;
    public bool StrictlyProhibitCancellationAfterPreparation { get; set; } = true;
    public int PrePreparationCutoffMinutes { get; set; } = 5;
    public string CustomerCancellationRefundMode { get; set; } = "RefundFoodSubtotalOnly";
    public bool RefundPlatformFeeOnCanteenCancellation { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

