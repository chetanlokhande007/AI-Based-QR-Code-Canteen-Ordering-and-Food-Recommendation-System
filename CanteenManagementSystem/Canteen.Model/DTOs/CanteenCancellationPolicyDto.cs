using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs;

public class CanteenCancellationPolicyDto
{
    public int CanteenPolicyId { get; set; }
    public int CanteenId { get; set; }
    public int AppliedPlatformPolicyVersion { get; set; }
    public bool EnableCustomerCancellation { get; set; } = true;
    public int CustomCutoffMinutes { get; set; } = 5;
    public List<string> PredefinedCancellationReasons { get; set; } = new();
    public string CustomPolicyNotice { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    // Platform constraints visible to owner
    public bool PlatformAllowsCustomerCancellation { get; set; } = true;
    public int PlatformMaxCutoffMinutes { get; set; } = 5;
    public bool PlatformProhibitsPostPreparation { get; set; } = true;
}

public class UpdateCanteenCancellationPolicyDto
{
    public bool EnableCustomerCancellation { get; set; } = true;
    public int CustomCutoffMinutes { get; set; } = 5;
    public List<string> PredefinedCancellationReasons { get; set; } = new();
    public string CustomPolicyNotice { get; set; } = string.Empty;
}

