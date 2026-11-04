using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class StaffRepository : IStaffRepository
{
    private readonly CanteenDbContext _context;

    public StaffRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<StaffMember> AddStaffAsync(StaffMember staff)
    {
        _context.StaffMembers.Add(staff);
        await _context.SaveChangesAsync();
        return staff;
    }

    public async Task<StaffMember?> GetStaffByIdAsync(int canteenId, int staffId)
    {
        return await _context.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CanteenId == canteenId && s.StaffId == staffId);
    }

    public async Task<StaffMember?> GetStaffByEmployeeIdAsync(int canteenId, string employeeId)
    {
        return await _context.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CanteenId == canteenId && s.EmployeeId == employeeId);
    }

    public async Task<StaffMember> UpdateStaffAsync(StaffMember staff)
    {
        staff.UpdatedAt = DateTime.UtcNow;
        var tracked = _context.StaffMembers.Local.FirstOrDefault(e => e.StaffId == staff.StaffId);
        if (tracked != null && !ReferenceEquals(tracked, staff))
        {
            _context.Entry(tracked).CurrentValues.SetValues(staff);
            await _context.SaveChangesAsync();
            return tracked;
        }

        _context.StaffMembers.Update(staff);
        await _context.SaveChangesAsync();
        return staff;
    }

    public async Task<bool> DeleteStaffAsync(int canteenId, int staffId)
    {
        var staff = await _context.StaffMembers
            .FirstOrDefaultAsync(s => s.CanteenId == canteenId && s.StaffId == staffId);

        if (staff == null) return false;

        _context.StaffMembers.Remove(staff);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<StaffPagedResult<StaffMember>> GetPagedStaffAsync(
        int canteenId,
        int page,
        int pageSize,
        string? search,
        string? role,
        string? status,
        string? shift)
    {
        page = Math.Max(1, page);
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _context.StaffMembers
            .AsNoTracking()
            .Where(s => s.CanteenId == canteenId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.FullName.ToLower().Contains(s) ||
                m.EmployeeId.ToLower().Contains(s) ||
                m.MobileNumber.ToLower().Contains(s) ||
                m.Email.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "All", StringComparison.OrdinalIgnoreCase))
        {
            var r = role.Trim().ToLowerInvariant();
            query = query.Where(m => m.Role.ToLower() == r);
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            var st = status.Trim().ToLowerInvariant();
            query = query.Where(m => m.Status.ToLower() == st);
        }

        if (!string.IsNullOrWhiteSpace(shift) && !string.Equals(shift, "All", StringComparison.OrdinalIgnoreCase))
        {
            var sh = shift.Trim().ToLowerInvariant();
            query = query.Where(m => m.Shift.ToLower().Contains(sh));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(m => m.EmployeeId)
            .ThenBy(m => m.StaffId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new StaffPagedResult<StaffMember>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<StaffSummaryMetricsDto> GetSummaryMetricsAsync(int canteenId)
    {
        var staffList = await _context.StaffMembers
            .AsNoTracking()
            .Where(s => s.CanteenId == canteenId)
            .ToListAsync();

        int total = staffList.Count;
        int active = staffList.Count(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase));
        int onLeave = staffList.Count(s => string.Equals(s.Status, "On Leave", StringComparison.OrdinalIgnoreCase));
        int inactive = staffList.Count(s => string.Equals(s.Status, "Inactive", StringComparison.OrdinalIgnoreCase));

        decimal activePct = total > 0 ? Math.Round((active / (decimal)total) * 100m, 1) : 0m;
        decimal onLeavePct = total > 0 ? Math.Round((onLeave / (decimal)total) * 100m, 1) : 0m;
        decimal inactivePct = total > 0 ? Math.Round((inactive / (decimal)total) * 100m, 1) : 0m;

        var lastMonthCutoff = DateTime.UtcNow.AddDays(-30);
        int recentJoiners = staffList.Count(s => s.JoinedOn >= lastMonthCutoff || s.CreatedAt >= lastMonthCutoff);

        return new StaffSummaryMetricsDto
        {
            TotalStaff = total,
            ActiveStaff = active,
            ActivePercentage = activePct,
            OnLeaveStaff = onLeave,
            OnLeavePercentage = onLeavePct,
            InactiveStaff = inactive,
            InactivePercentage = inactivePct,
            MonthlyGrowth = recentJoiners
        };
    }

    public async Task<List<StaffMember>> GetAllStaffAsync(int canteenId)
    {
        return await _context.StaffMembers
            .AsNoTracking()
            .Where(s => s.CanteenId == canteenId)
            .OrderBy(s => s.FullName)
            .ToListAsync();
    }

    public async Task<string> GenerateNextEmployeeIdAsync(int canteenId)
    {
        var count = await _context.StaffMembers
            .Where(s => s.CanteenId == canteenId)
            .CountAsync();

        return $"EMP{(count + 1):D3}";
    }

    public async Task<List<StaffRoleConfigDto>> GetRoleConfigsAsync(int canteenId)
    {
        var staffRoles = await _context.StaffMembers
            .AsNoTracking()
            .Where(s => s.CanteenId == canteenId)
            .GroupBy(s => s.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Role, g => g.Count);

        var roles = new List<StaffRoleConfigDto>
        {
            new()
            {
                RoleId = "chef",
                RoleName = "Chef",
                Description = "Kitchen cooking management, food preparation recipes and kitchen display order status updating.",
                Permissions = new List<string> { "View Kitchen Display", "Update Cooking Status", "View Recipe Costing", "Manage Ingredient Usage" },
                StaffCount = staffRoles.GetValueOrDefault("Chef", 0)
            },
            new()
            {
                RoleId = "kitchen_staff",
                RoleName = "Kitchen Staff",
                Description = "Food preparation, cooking support, ticket order ready bumping and station hygiene.",
                Permissions = new List<string> { "View Kitchen Display", "Bump Ready Orders", "Call Token Bell" },
                StaffCount = staffRoles.GetValueOrDefault("Kitchen Staff", 0)
            },
            new()
            {
                RoleId = "counter_staff",
                RoleName = "Counter Staff",
                Description = "Customer order verification, cash counter collection, and pickup token handover.",
                Permissions = new List<string> { "Confirm Cash Payments", "Deliver Pickup Orders", "View Table Orders", "Customer Lookup" },
                StaffCount = staffRoles.GetValueOrDefault("Counter Staff", 0)
            },
            new()
            {
                RoleId = "order_manager",
                RoleName = "Order Manager",
                Description = "Oversees live orders flow, issues manual refunds, and manages cancellations.",
                Permissions = new List<string> { "View All Live Orders", "Override Order Status", "Process Order Cancellation", "Apply Discounts" },
                StaffCount = staffRoles.GetValueOrDefault("Order Manager", 0)
            },
            new()
            {
                RoleId = "inventory_staff",
                RoleName = "Inventory Staff",
                Description = "Tracks stock levels, logs wastage, receives supplier raw goods, and records adjustments.",
                Permissions = new List<string> { "View Stock Levels", "Stock In/Out", "Log Ingredient Wastage", "Low-Stock Alerts" },
                StaffCount = staffRoles.GetValueOrDefault("Inventory Staff", 0)
            }
        };

        return roles;
    }

    public async Task<List<StaffAttendance>> GetAttendanceLogsAsync(int canteenId, DateTime? date)
    {
        var rawDate = date ?? DateTime.UtcNow;
        var targetDate = DateTime.SpecifyKind(rawDate.Date, DateTimeKind.Utc);
        var nextDay = targetDate.AddDays(1);
        return await _context.StaffAttendances
            .AsNoTracking()
            .Include(a => a.StaffMember)
            .Where(a => a.CanteenId == canteenId && a.Date >= targetDate && a.Date < nextDay)
            .OrderByDescending(a => a.CheckInTime)
            .ToListAsync();
    }

    public async Task<StaffAttendance> RecordAttendanceAsync(StaffAttendance attendance)
    {
        var targetDate = DateTime.SpecifyKind(attendance.Date.Date, DateTimeKind.Utc);
        var nextDay = targetDate.AddDays(1);

        attendance.Date = targetDate;
        if (attendance.CheckInTime.HasValue)
            attendance.CheckInTime = DateTime.SpecifyKind(attendance.CheckInTime.Value, DateTimeKind.Utc);
        if (attendance.CheckOutTime.HasValue)
            attendance.CheckOutTime = DateTime.SpecifyKind(attendance.CheckOutTime.Value, DateTimeKind.Utc);

        var existing = await _context.StaffAttendances
            .FirstOrDefaultAsync(a => a.CanteenId == attendance.CanteenId &&
                                      a.StaffId == attendance.StaffId &&
                                      a.Date >= targetDate && a.Date < nextDay);

        if (existing != null)
        {
            if (attendance.CheckInTime.HasValue) existing.CheckInTime = attendance.CheckInTime;
            if (attendance.CheckOutTime.HasValue) existing.CheckOutTime = attendance.CheckOutTime;
            if (!string.IsNullOrWhiteSpace(attendance.Status)) existing.Status = attendance.Status;
            if (!string.IsNullOrWhiteSpace(attendance.Notes)) existing.Notes = attendance.Notes;
            await _context.SaveChangesAsync();
            return existing;
        }

        _context.StaffAttendances.Add(attendance);
        await _context.SaveChangesAsync();
        return attendance;
    }

    public async Task<List<StaffActivityLog>> GetActivityLogsAsync(int canteenId, int limit)
    {
        limit = limit is < 1 or > 100 ? 50 : limit;
        return await _context.StaffActivityLogs
            .AsNoTracking()
            .Include(l => l.StaffMember)
            .Where(l => l.CanteenId == canteenId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task AddActivityLogAsync(StaffActivityLog log)
    {
        _context.StaffActivityLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<ImportStaffResultDto> ImportStaffBatchAsync(int canteenId, List<CreateStaffMemberDto> dtos)
    {
        int imported = 0;
        int failed = 0;

        foreach (var dto in dtos)
        {
            try
            {
                var empId = await GenerateNextEmployeeIdAsync(canteenId);
                var member = new StaffMember
                {
                    CanteenId = canteenId,
                    EmployeeId = empId,
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
                    Notes = dto.Notes?.Trim()
                };

                _context.StaffMembers.Add(member);
                await _context.SaveChangesAsync();
                imported++;
            }
            catch
            {
                failed++;
            }
        }

        return new ImportStaffResultDto
        {
            TotalImported = imported,
            Failed = failed,
            Message = $"Batch import completed: {imported} imported, {failed} failed."
        };
    }
}

