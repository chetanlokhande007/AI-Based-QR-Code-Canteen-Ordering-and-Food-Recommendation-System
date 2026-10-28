using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "PlatformAdmin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly ICanteenService _canteenService;

    public AdminController(ICanteenService canteenService)
    {
        _canteenService = canteenService;
    }

    /// <summary>Get platform-wide overview metrics (Platform Admin only).</summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(PlatformAdminMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await _canteenService.GetPlatformMetricsAsync();
        return Ok(metrics);
    }

    /// <summary>Get all registered canteens with owner details (Platform Admin only).</summary>
    [HttpGet("canteens")]
    [ProducesResponseType(typeof(IEnumerable<AdminCanteenItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCanteens()
    {
        var canteens = await _canteenService.GetAdminCanteensAsync();
        return Ok(canteens);
    }

    /// <summary>Activate or suspend a canteen (Platform Admin only).</summary>
    [HttpPatch("canteens/{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateCanteenStatusDto dto)
    {
        var success = await _canteenService.UpdateCanteenStatusAsync(id, dto.IsActive, dto.TenantStatus);
        if (!success)
        {
            return NotFound(new { message = $"Canteen with ID {id} was not found." });
        }

        var isSuspended = dto.TenantStatus == TenantStatus.Suspended || (dto.TenantStatus == null && dto.IsActive == false);
        var effectiveTenantStatus = dto.TenantStatus ?? (isSuspended ? TenantStatus.Suspended : TenantStatus.Active);
        var effectiveIsActive = dto.IsActive ?? !isSuspended;

        return Ok(new
        {
            canteenId = id,
            isActive = effectiveIsActive,
            tenantStatus = effectiveTenantStatus.ToString(),
            message = $"Canteen #{id} status has been successfully updated to {effectiveTenantStatus}."
        });
    }
}
