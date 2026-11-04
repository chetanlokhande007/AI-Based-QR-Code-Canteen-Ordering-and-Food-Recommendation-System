using System;

namespace Canteen.Model.Entities;

public class StaffAttendance
{
    public int AttendanceId { get; set; }

    public int StaffId { get; set; }

    public int CanteenId { get; set; }

    public DateTime Date { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

    public DateTime? CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    // Present, Absent, Half Day, On Leave
    public string Status { get; set; } = "Present";

    public string? Notes { get; set; }

    // Navigation
    public StaffMember StaffMember { get; set; } = null!;

    public Canteen Canteen { get; set; } = null!;
}

