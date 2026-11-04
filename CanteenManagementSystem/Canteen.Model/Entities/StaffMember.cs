using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities;

public class StaffMember
{
    public int StaffId { get; set; }

    public int CanteenId { get; set; }

    public string EmployeeId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    // Chef, Kitchen Staff, Counter Staff, Order Manager, Inventory Staff, Cashier, Cleaner
    public string Role { get; set; } = "Kitchen Staff";

    // Morning (6:00 AM - 2:00 PM), Evening (2:00 PM - 10:00 PM), Night (10:00 PM - 6:00 AM), Full Day (9:00 AM - 9:00 PM)
    public string Shift { get; set; } = "Morning (6:00 AM - 2:00 PM)";

    // Active, On Leave, Inactive
    public string Status { get; set; } = "Active";

    public DateTime JoinedOn { get; set; } = DateTime.UtcNow;

    // Profile photo stored in database (URL or data URI)
    public string? ProfilePhotoUrl { get; set; }

    public string? EmergencyContact { get; set; }

    public string? Address { get; set; }

    public decimal? Salary { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Canteen Canteen { get; set; } = null!;

    public ICollection<StaffAttendance> Attendances { get; set; } = new List<StaffAttendance>();

    public ICollection<StaffActivityLog> ActivityLogs { get; set; } = new List<StaffActivityLog>();
}

