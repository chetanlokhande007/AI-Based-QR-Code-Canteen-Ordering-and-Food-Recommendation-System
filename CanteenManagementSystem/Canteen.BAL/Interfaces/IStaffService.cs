using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IStaffService
{
    Task<StaffSummaryMetricsDto> GetSummaryMetricsAsync(int canteenId);

    Task<StaffPagedResult<StaffMemberDto>> GetPagedStaffAsync(
        int canteenId,
        int page,
        int pageSize,
        string? search,
        string? role,
        string? status,
        string? shift);

    Task<StaffMemberDto> GetStaffByIdAsync(int canteenId, int staffId);

    Task<StaffMemberDto> CreateStaffAsync(int canteenId, CreateStaffMemberDto dto);

    Task<StaffMemberDto> UpdateStaffAsync(int canteenId, int staffId, UpdateStaffMemberDto dto);

    Task<bool> DeleteStaffAsync(int canteenId, int staffId);

    Task<List<StaffRoleConfigDto>> GetRoleConfigsAsync(int canteenId);

    Task<List<StaffAttendanceDto>> GetAttendanceLogsAsync(int canteenId, DateTime? date);

    Task<StaffAttendanceDto> MarkAttendanceAsync(int canteenId, MarkAttendanceDto dto);

    Task<List<StaffActivityLogDto>> GetActivityLogsAsync(int canteenId, int limit = 50);

    Task<ImportStaffResultDto> ImportStaffBatchAsync(int canteenId, List<CreateStaffMemberDto> dtos);
}

