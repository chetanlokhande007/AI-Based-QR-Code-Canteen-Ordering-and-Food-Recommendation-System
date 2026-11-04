using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Canteen.BAL.Services;

public class StaffService : IStaffService
{
    private readonly IStaffRepository _staffRepository;
    private readonly ICanteenRepository _canteenRepository;
    private readonly ILogger<StaffService> _logger;

    public StaffService(
        IStaffRepository staffRepository,
        ICanteenRepository canteenRepository,
        ILogger<StaffService> logger)
    {
        _staffRepository = staffRepository;
        _canteenRepository = canteenRepository;
        _logger = logger;
    }

    public async Task<StaffSummaryMetricsDto> GetSummaryMetricsAsync(int canteenId)
    {
        return await _staffRepository.GetSummaryMetricsAsync(canteenId);
    }

    public async Task<StaffPagedResult<StaffMemberDto>> GetPagedStaffAsync(
        int canteenId,
        int page,
        int pageSize,
        string? search,
        string? role,
        string? status,
        string? shift)
    {
        var result = await _staffRepository.GetPagedStaffAsync(
            canteenId, page, pageSize, search, role, status, shift);

        var mapped = result.Items.Select(s => MapToDto(s)).ToList();

        return new StaffPagedResult<StaffMemberDto>
        {
            Items = mapped,
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<StaffMemberDto> GetStaffByIdAsync(int canteenId, int staffId)
    {
        var staff = await _staffRepository.GetStaffByIdAsync(canteenId, staffId);
        if (staff == null)
            throw new KeyNotFoundException($"Staff member with ID {staffId} not found in canteen {canteenId}.");

        return MapToDto(staff);
    }

    public async Task<StaffMemberDto> CreateStaffAsync(int canteenId, CreateStaffMemberDto dto)
    {
        var canteen = await _canteenRepository.GetByIdAsync(canteenId);
        if (canteen == null)
            throw new KeyNotFoundException($"Canteen with ID {canteenId} not found.");

        var employeeId = await _staffRepository.GenerateNextEmployeeIdAsync(canteenId);

        var member = new StaffMember
        {
            CanteenId = canteenId,
            EmployeeId = employeeId,
            FullName = dto.FullName.Trim(),
            Email = dto.Email?.Trim() ?? string.Empty,
            MobileNumber = dto.MobileNumber.Trim(),
            Role = dto.Role.Trim(),
            Shift = dto.Shift.Trim(),
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status.Trim(),
            JoinedOn = dto.JoinedOn.HasValue 
                ? DateTime.SpecifyKind(dto.JoinedOn.Value.Date, DateTimeKind.Utc) 
                : DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            ProfilePhotoUrl = dto.ProfilePhotoUrl,
            EmergencyContact = dto.EmergencyContact?.Trim(),
            Address = dto.Address?.Trim(),
            Salary = dto.Salary,
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _staffRepository.AddStaffAsync(member);

        await _staffRepository.AddActivityLogAsync(new StaffActivityLog
        {
            CanteenId = canteenId,
            StaffId = created.StaffId,
            Action = "Staff Created",
            Timestamp = DateTime.UtcNow,
            Details = $"Onboarded {created.FullName} as {created.Role} ({created.EmployeeId})."
        });

        return MapToDto(created);
    }

    public async Task<StaffMemberDto> UpdateStaffAsync(int canteenId, int staffId, UpdateStaffMemberDto dto)
    {
        var existing = await _staffRepository.GetStaffByIdAsync(canteenId, staffId);
        if (existing == null)
            throw new KeyNotFoundException($"Staff member with ID {staffId} not found.");

        if (dto.FullName != null) existing.FullName = dto.FullName.Trim();
        if (dto.Email != null) existing.Email = dto.Email.Trim();
        if (dto.MobileNumber != null) existing.MobileNumber = dto.MobileNumber.Trim();
        if (dto.Role != null) existing.Role = dto.Role.Trim();
        if (dto.Shift != null) existing.Shift = dto.Shift.Trim();
        if (dto.Status != null) existing.Status = dto.Status.Trim();
        if (dto.ProfilePhotoUrl != null) existing.ProfilePhotoUrl = dto.ProfilePhotoUrl;
        if (dto.EmergencyContact != null) existing.EmergencyContact = dto.EmergencyContact.Trim();
        if (dto.Address != null) existing.Address = dto.Address.Trim();
        if (dto.Salary.HasValue) existing.Salary = dto.Salary.Value;
        if (dto.Notes != null) existing.Notes = dto.Notes.Trim();

        var updated = await _staffRepository.UpdateStaffAsync(existing);

        await _staffRepository.AddActivityLogAsync(new StaffActivityLog
        {
            CanteenId = canteenId,
            StaffId = updated.StaffId,
            Action = "Profile Updated",
            Timestamp = DateTime.UtcNow,
            Details = $"Updated profile records for {updated.FullName} ({updated.EmployeeId})."
        });

        return MapToDto(updated);
    }

    public async Task<bool> DeleteStaffAsync(int canteenId, int staffId)
    {
        var staff = await _staffRepository.GetStaffByIdAsync(canteenId, staffId);
        if (staff == null) return false;

        var name = staff.FullName;
        var empId = staff.EmployeeId;

        var deleted = await _staffRepository.DeleteStaffAsync(canteenId, staffId);
        if (deleted)
        {
            await _staffRepository.AddActivityLogAsync(new StaffActivityLog
            {
                CanteenId = canteenId,
                StaffId = staffId,
                Action = "Staff Removed",
                Timestamp = DateTime.UtcNow,
                Details = $"Removed {name} ({empId}) from staff registry."
            });
        }

        return deleted;
    }

    public async Task<List<StaffRoleConfigDto>> GetRoleConfigsAsync(int canteenId)
    {
        return await _staffRepository.GetRoleConfigsAsync(canteenId);
    }

    public async Task<List<StaffAttendanceDto>> GetAttendanceLogsAsync(int canteenId, DateTime? date)
    {
        var rawDate = date ?? DateTime.UtcNow;
        var targetDate = DateTime.SpecifyKind(rawDate.Date, DateTimeKind.Utc);
        var allStaff = await _staffRepository.GetAllStaffAsync(canteenId);
        var logs = await _staffRepository.GetAttendanceLogsAsync(canteenId, targetDate);
        var logMap = logs.ToDictionary(l => l.StaffId, l => l);

        var result = new List<StaffAttendanceDto>();

        foreach (var staff in allStaff)
        {
            if (logMap.TryGetValue(staff.StaffId, out var att))
            {
                result.Add(new StaffAttendanceDto
                {
                    AttendanceId = att.AttendanceId,
                    StaffId = att.StaffId,
                    StaffName = staff.FullName,
                    EmployeeId = staff.EmployeeId,
                    Role = staff.Role,
                    Shift = staff.Shift,
                    Date = att.Date,
                    CheckInTime = att.CheckInTime,
                    CheckOutTime = att.CheckOutTime,
                    Status = att.Status,
                    Notes = att.Notes
                });
            }
            else
            {
                string initialStatus = string.Equals(staff.Status, "On Leave", StringComparison.OrdinalIgnoreCase)
                    ? "On Leave"
                    : (string.Equals(staff.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Not Clocked In");

                result.Add(new StaffAttendanceDto
                {
                    AttendanceId = 0,
                    StaffId = staff.StaffId,
                    StaffName = staff.FullName,
                    EmployeeId = staff.EmployeeId,
                    Role = staff.Role,
                    Shift = staff.Shift,
                    Date = targetDate,
                    CheckInTime = null,
                    CheckOutTime = null,
                    Status = initialStatus,
                    Notes = null
                });
            }
        }

        return result;
    }

    public async Task<StaffAttendanceDto> MarkAttendanceAsync(int canteenId, MarkAttendanceDto dto)
    {
        var staff = await _staffRepository.GetStaffByIdAsync(canteenId, dto.StaffId);
        if (staff == null)
            throw new KeyNotFoundException($"Staff member with ID {dto.StaffId} was not found.");

        var rawDate = dto.Date ?? DateTime.UtcNow;
        var targetDate = DateTime.SpecifyKind(rawDate.Date, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        string status;
        DateTime? checkIn = null;
        DateTime? checkOut = null;

        if (string.Equals(dto.Action, "CheckOut", StringComparison.OrdinalIgnoreCase))
        {
            checkOut = now;
            status = !string.IsNullOrWhiteSpace(dto.Status) && dto.Status != "Present" ? dto.Status : "Completed";
        }
        else if (string.Equals(dto.Action, "MarkAbsent", StringComparison.OrdinalIgnoreCase))
        {
            status = "Absent";
        }
        else if (string.Equals(dto.Action, "MarkLeave", StringComparison.OrdinalIgnoreCase))
        {
            status = "On Leave";
        }
        else
        {
            checkIn = now;
            status = string.IsNullOrWhiteSpace(dto.Status) || dto.Status == "Not Clocked In" ? "Present" : dto.Status;
        }

        var attendance = new StaffAttendance
        {
            CanteenId = canteenId,
            StaffId = dto.StaffId,
            Date = targetDate,
            CheckInTime = checkIn,
            CheckOutTime = checkOut,
            Status = status,
            Notes = dto.Notes
        };

        var saved = await _staffRepository.RecordAttendanceAsync(attendance);

        string actionName = dto.Action switch
        {
            "CheckOut" => "Clock Out",
            "MarkAbsent" => "Marked Absent",
            "MarkLeave" => "Marked Leave",
            _ => "Clock In"
        };

        await _staffRepository.AddActivityLogAsync(new StaffActivityLog
        {
            CanteenId = canteenId,
            StaffId = staff.StaffId,
            Action = actionName,
            Timestamp = now,
            Details = $"{staff.FullName} ({staff.EmployeeId}) {actionName.ToLower()} on {targetDate:yyyy-MM-dd} at {now:HH:mm} UTC."
        });

        return new StaffAttendanceDto
        {
            AttendanceId = saved.AttendanceId,
            StaffId = saved.StaffId,
            StaffName = staff.FullName,
            EmployeeId = staff.EmployeeId,
            Role = staff.Role,
            Shift = staff.Shift,
            Date = saved.Date,
            CheckInTime = saved.CheckInTime,
            CheckOutTime = saved.CheckOutTime,
            Status = saved.Status,
            Notes = saved.Notes
        };
    }

    public async Task<List<StaffActivityLogDto>> GetActivityLogsAsync(int canteenId, int limit = 50)
    {
        var logs = await _staffRepository.GetActivityLogsAsync(canteenId, limit);
        return logs.Select(l => new StaffActivityLogDto
        {
            ActivityId = l.ActivityId,
            StaffId = l.StaffId,
            StaffName = l.StaffMember?.FullName ?? "Staff Member",
            EmployeeId = l.StaffMember?.EmployeeId ?? string.Empty,
            Action = l.Action,
            Timestamp = l.Timestamp,
            Details = l.Details
        }).ToList();
    }

    public async Task<ImportStaffResultDto> ImportStaffBatchAsync(int canteenId, List<CreateStaffMemberDto> dtos)
    {
        return await _staffRepository.ImportStaffBatchAsync(canteenId, dtos);
    }

    private static StaffMemberDto MapToDto(StaffMember s)
    {
        return new StaffMemberDto
        {
            StaffId = s.StaffId,
            CanteenId = s.CanteenId,
            EmployeeId = s.EmployeeId,
            FullName = s.FullName,
            Email = s.Email,
            MobileNumber = s.MobileNumber,
            Role = s.Role,
            Shift = s.Shift,
            Status = s.Status,
            JoinedOn = s.JoinedOn,
            ProfilePhotoUrl = s.ProfilePhotoUrl,
            Initials = ComputeInitials(s.FullName),
            EmergencyContact = s.EmergencyContact,
            Address = s.Address,
            Salary = s.Salary,
            Notes = s.Notes,
            CreatedAt = s.CreatedAt
        };
    }

    private static string ComputeInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "ST";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        }
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}

