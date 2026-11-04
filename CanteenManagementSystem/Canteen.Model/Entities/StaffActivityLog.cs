using System;

namespace Canteen.Model.Entities;

public class StaffActivityLog
{
    public int ActivityId { get; set; }

    public int StaffId { get; set; }

    public int CanteenId { get; set; }

    public string Action { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? Details { get; set; }

    // Navigation
    public StaffMember StaffMember { get; set; } = null!;

    public Canteen Canteen { get; set; } = null!;
}

