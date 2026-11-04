using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class StaffSummaryMetricsDto
{
    public int TotalStaff { get; set; }
    public int ActiveStaff { get; set; }
    public decimal ActivePercentage { get; set; }
    public int OnLeaveStaff { get; set; }
    public decimal OnLeavePercentage { get; set; }
    public int InactiveStaff { get; set; }
    public decimal InactivePercentage { get; set; }
    public int MonthlyGrowth { get; set; } = 0;
}

public class StaffMemberDto
{
    public int StaffId { get; set; }
    public int CanteenId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime JoinedOn { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string Initials { get; set; } = string.Empty;
    public string? EmergencyContact { get; set; }
    public string? Address { get; set; }
    public decimal? Salary { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStaffMemberDto
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Mobile Number is required.")]
    [RegularExpression(@"^[0-9+\s\-]{10,15}$", ErrorMessage = "Please provide a valid mobile number (10-15 digits).")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = "Kitchen Staff";

    [Required(ErrorMessage = "Shift is required.")]
    public string Shift { get; set; } = "Morning (6:00 AM - 2:00 PM)";

    public string Status { get; set; } = "Active";

    public DateTime? JoinedOn { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? EmergencyContact { get; set; }

    public string? Address { get; set; }

    public decimal? Salary { get; set; }

    public string? Notes { get; set; }
}

public class UpdateStaffMemberDto
{
    [StringLength(100, MinimumLength = 2)]
    public string? FullName { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [RegularExpression(@"^[0-9+\s\-]{10,15}$")]
    public string? MobileNumber { get; set; }

    public string? Role { get; set; }

    public string? Shift { get; set; }

    public string? Status { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? EmergencyContact { get; set; }

    public string? Address { get; set; }

    public decimal? Salary { get; set; }

    public string? Notes { get; set; }
}

public class StaffPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class StaffRoleConfigDto
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public int StaffCount { get; set; }
}

public class StaffAttendanceDto
{
    public int AttendanceId { get; set; }
    public int StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string Status { get; set; } = "Present";
    public string? Notes { get; set; }
}

public class MarkAttendanceDto
{
    [Required]
    public int StaffId { get; set; }

    public DateTime? Date { get; set; }

    public string Status { get; set; } = "Present";

    public string Action { get; set; } = "CheckIn"; // "CheckIn", "CheckOut", "MarkAbsent", "MarkLeave"

    public string? Notes { get; set; }
}

public class StaffActivityLogDto
{
    public int ActivityId { get; set; }
    public int StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}

public class ImportStaffResultDto
{
    public int TotalImported { get; set; }
    public int Failed { get; set; }
    public string Message { get; set; } = string.Empty;
}

