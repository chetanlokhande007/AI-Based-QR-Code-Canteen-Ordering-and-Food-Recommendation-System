using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Produces("application/json")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;
    private readonly ICanteenService _canteenService;

    public StaffController(IStaffService staffService, ICanteenService canteenService)
    {
        _staffService = staffService;
        _canteenService = canteenService;
    }

    private async Task<bool> IsAuthorizedAsync(int canteenId)
    {
        if (User.IsInRole("PlatformAdmin")) return true;

        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return false;

        var activeCanteenId = User.GetCanteenId();
        if (!activeCanteenId.HasValue || activeCanteenId.Value != canteenId)
        {
            return false;
        }

        return await _canteenService.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, canteenId);
    }

    /// <summary>
    /// Retrieves summary metrics for the Staff Management dashboard KPI cards.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/staff/metrics")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(StaffSummaryMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummaryMetrics(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var metrics = await _staffService.GetSummaryMetricsAsync(canteenId);
        return Ok(metrics);
    }

    /// <summary>
    /// Retrieves server-paginated, filtered staff members for a canteen.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/staff")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(StaffPagedResult<StaffMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedStaff(
        int canteenId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        [FromQuery] string? shift = null)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var result = await _staffService.GetPagedStaffAsync(canteenId, page, pageSize, search, role, status, shift);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single staff member by ID.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/staff/{staffId:int}")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStaffById(int canteenId, int staffId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        try
        {
            var staff = await _staffService.GetStaffByIdAsync(canteenId, staffId);
            return Ok(staff);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates and onboards a new staff member for the canteen.
    /// </summary>
    [HttpPost("api/canteens/{canteenId:int}/staff")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateStaff(int canteenId, [FromBody] CreateStaffMemberDto dto)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await _staffService.CreateStaffAsync(canteenId, dto);
            return CreatedAtAction(nameof(GetStaffById), new { canteenId, staffId = created.StaffId }, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing staff member's profile, role, shift, or photo.
    /// </summary>
    [HttpPut("api/canteens/{canteenId:int}/staff/{staffId:int}")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStaff(int canteenId, int staffId, [FromBody] UpdateStaffMemberDto dto)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await _staffService.UpdateStaffAsync(canteenId, staffId, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a staff member from the canteen roster.
    /// </summary>
    [HttpDelete("api/canteens/{canteenId:int}/staff/{staffId:int}")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStaff(int canteenId, int staffId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var deleted = await _staffService.DeleteStaffAsync(canteenId, staffId);
        return deleted ? NoContent() : NotFound(new { message = "Staff member not found." });
    }

    /// <summary>
    /// Retrieves predefined canteen staff roles and permission configurations.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/staff/roles")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(List<StaffRoleConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoleConfigs(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var configs = await _staffService.GetRoleConfigsAsync(canteenId);
        return Ok(configs);
    }

    /// <summary>
    /// Retrieves attendance records for a specific date.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/staff/attendance")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(List<StaffAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAttendance(int canteenId, [FromQuery] DateTime? date = null)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var logs = await _staffService.GetAttendanceLogsAsync(canteenId, date);
        return Ok(logs);
    }

    /// <summary>
    /// Records or updates check-in / check-out attendance for a staff member.
    /// </summary>
    [HttpPost("api/canteens/{canteenId:int}/staff/attendance")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(StaffAttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAttendance(int canteenId, [FromBody] MarkAttendanceDto dto)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var record = await _staffService.MarkAttendanceAsync(canteenId, dto);
            return Ok(record);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves recent staff audit activity logs.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/staff/activity-logs")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(List<StaffActivityLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActivityLogs(int canteenId, [FromQuery] int limit = 50)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var logs = await _staffService.GetActivityLogsAsync(canteenId, limit);
        return Ok(logs);
    }

    /// <summary>
    /// Batch imports staff members.
    /// </summary>
    [HttpPost("api/canteens/{canteenId:int}/staff/import")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(ImportStaffResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ImportStaff(int canteenId, [FromBody] List<CreateStaffMemberDto> dtos)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        if (dtos == null || dtos.Count == 0)
        {
            return BadRequest(new { message = "No staff member records provided for import." });
        }

        var result = await _staffService.ImportStaffBatchAsync(canteenId, dtos);
        return Ok(result);
    }
}

