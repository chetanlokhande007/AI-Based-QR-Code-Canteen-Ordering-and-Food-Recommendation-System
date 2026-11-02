using System;

namespace Canteen.Model.Entities;

public class CanteenCancellationPolicy
{
    public int CanteenPolicyId { get; set; }

    public int CanteenId { get; set; }

    // Platform policy version baseline enforced at time of configuration
    public int AppliedPlatformPolicyVersion { get; set; } = 1;

    // Canteen can optionally disable customer cancellation if platform allows choice
    public bool EnableCustomerCancellation { get; set; } = true;

    // Canteen custom cutoff (must be <= platform PrePreparationCutoffMinutes)
    public int CustomCutoffMinutes { get; set; } = 5;

    // JSON array of predefined reasons staff can select (e.g. ["Out of stock", "Kitchen busy"])
    public string PredefinedCancellationReasonsJson { get; set; } = "[\"Item out of stock\",\"Kitchen overload\",\"Equipment breakdown\",\"Closing early\"]";

    // Informational notice displayed to customers on this canteen's menu
    public string CustomPolicyNotice { get; set; } = "Cancellations are accepted before cooking begins.";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Canteen Canteen { get; set; } = null!;
}

