using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class StaffReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Summary Staff Metrics
    public int TotalStaffCount { get; set; }
    public int ActiveStaffCount { get; set; }
    public int OnLeaveStaffCount { get; set; }
    public int InactiveStaffCount { get; set; }
    public decimal TotalMonthlyPayroll { get; set; }
    public decimal OverallAttendanceRate { get; set; } // %

    // Role & Shift Breakdown
    public List<ReportDistributionDto> RoleBreakdown { get; set; } = new();
    public List<ReportDistributionDto> ShiftBreakdown { get; set; } = new();

    // Attendance Summary
    public StaffAttendanceSummaryDto AttendanceSummary { get; set; } = new();

    // Paginated Detailed Staff Directory
    public ReportPagedResult<StaffMemberReportItemDto> StaffMembers { get; set; } = new();
}

public class StaffAttendanceSummaryDto
{
    public int TotalAttendanceLogs { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int HalfDayCount { get; set; }
    public int OnLeaveCount { get; set; }
    public decimal AttendancePercentage { get; set; }
    public List<DailyAttendanceTrendDto> DailyTrend { get; set; } = new();
}

public class DailyAttendanceTrendDto
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int HalfDayCount { get; set; }
    public int OnLeaveCount { get; set; }
}

public class StaffMemberReportItemDto
{
    public int StaffId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime JoinedOn { get; set; }
    public decimal? Salary { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public decimal AttendanceRate { get; set; }
}

public class StaffAttendanceReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public int TotalWorkDays { get; set; }
    public int TotalPresent { get; set; }
    public int TotalAbsent { get; set; }
    public int TotalHalfDay { get; set; }
    public int TotalOnLeave { get; set; }
    public decimal OverallAttendanceRate { get; set; }

    public List<DailyAttendanceTrendDto> DailyRecords { get; set; } = new();
    public ReportPagedResult<AttendanceRecordDetailDto> LogEntries { get; set; } = new();
}

public class AttendanceRecordDetailDto
{
    public int AttendanceId { get; set; }
    public int StaffId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string Status { get; set; } = "Present";
    public string? Notes { get; set; }
}

