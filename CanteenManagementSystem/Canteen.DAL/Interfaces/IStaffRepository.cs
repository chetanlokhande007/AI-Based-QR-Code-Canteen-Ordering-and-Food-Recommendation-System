using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IStaffRepository
{
    Task<StaffMember> AddStaffAsync(StaffMember staff);

    Task<StaffMember?> GetStaffByIdAsync(int canteenId, int staffId);

    Task<StaffMember?> GetStaffByEmployeeIdAsync(int canteenId, string employeeId);

    Task<StaffMember> UpdateStaffAsync(StaffMember staff);

    Task<bool> DeleteStaffAsync(int canteenId, int staffId);

    Task<StaffPagedResult<StaffMember>> GetPagedStaffAsync(
        int canteenId,
        int page,
        int pageSize,
        string? search,
        string? role,
        string? status,
        string? shift);

    Task<StaffSummaryMetricsDto> GetSummaryMetricsAsync(int canteenId);

    Task<List<StaffMember>> GetAllStaffAsync(int canteenId);

    Task<string> GenerateNextEmployeeIdAsync(int canteenId);

    Task<List<StaffRoleConfigDto>> GetRoleConfigsAsync(int canteenId);

    Task<List<StaffAttendance>> GetAttendanceLogsAsync(int canteenId, DateTime? date);

    Task<StaffAttendance> RecordAttendanceAsync(StaffAttendance attendance);

    Task<List<StaffActivityLog>> GetActivityLogsAsync(int canteenId, int limit);

    Task AddActivityLogAsync(StaffActivityLog log);

    Task<ImportStaffResultDto> ImportStaffBatchAsync(int canteenId, List<CreateStaffMemberDto> dtos);
}

