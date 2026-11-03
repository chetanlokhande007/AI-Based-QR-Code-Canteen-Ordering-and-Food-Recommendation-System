using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using CanteenManagementSystem.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Produces("application/json")]
public class TvDevicesController : ControllerBase
{
    private readonly ITvDeviceService _tvDeviceService;
    private readonly ICanteenService _canteenService;
    private readonly IHubContext<OrderHub>? _hubContext;

    public TvDevicesController(
        ITvDeviceService tvDeviceService,
        ICanteenService canteenService,
        IHubContext<OrderHub>? hubContext = null)
    {
        _tvDeviceService = tvDeviceService;
        _canteenService = canteenService;
        _hubContext = hubContext;
    }

    private async Task<bool> IsAuthorizedAsync(int canteenId)
    {
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
    /// Owner generates a temporary 6-digit numeric pairing code for a new Smart TV.
    /// </summary>
    [HttpPost("api/canteens/{canteenId:int}/tv-devices/pair-request")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(TvPairingSessionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePairingRequest(int canteenId, [FromBody] CreateTvPairingRequestDto? dto)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        try
        {
            var session = await _tvDeviceService.CreatePairingSessionAsync(canteenId, dto ?? new CreateTvPairingRequestDto());
            return Ok(session);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Smart TV submits the 6-digit pairing PIN to register and receive its device token.
    /// </summary>
    [HttpPost("api/tv/pair/claim")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TvClaimResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClaimPairingCode([FromBody] ClaimTvPairingDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _tvDeviceService.ClaimPairingCodeAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Smart TV fetches station-scoped orders using its verified device token header (X-Device-Token).
    /// </summary>
    [HttpGet("api/tv/display")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CanteenTvDisplayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTvDisplayByToken([FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return Unauthorized(new { message = "Missing 'X-Device-Token' header." });
        }

        var device = await _tvDeviceService.ValidateDeviceTokenAsync(deviceToken);
        if (device == null || !device.IsActive)
        {
            return Unauthorized(new { message = "Invalid, expired, or revoked device token." });
        }

        var data = await _tvDeviceService.GetStationDisplayDataAsync(device.CanteenId, device.StationScope, device.DeviceName);
        return Ok(data);
    }

    /// <summary>
    /// Lists all paired Smart TVs for a canteen.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/tv-devices")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(IEnumerable<TvDeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDevicesByCanteen(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var devices = await _tvDeviceService.GetDevicesByCanteenAsync(canteenId);
        return Ok(devices);
    }

    /// <summary>
    /// Updates a Smart TV's display name or station scope.
    /// </summary>
    [HttpPut("api/canteens/{canteenId:int}/tv-devices/{deviceId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(TvDeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDevice(int canteenId, int deviceId, [FromBody] UpdateTvDeviceDto dto)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var updated = await _tvDeviceService.UpdateDeviceAsync(canteenId, deviceId, dto);
        return updated == null ? NotFound(new { message = "TV device not found." }) : Ok(updated);
    }

    /// <summary>
    /// Revokes a Smart TV immediately, blocking further requests and notifying the device.
    /// </summary>
    [HttpDelete("api/canteens/{canteenId:int}/tv-devices/{deviceId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeDevice(int canteenId, int deviceId)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var success = await _tvDeviceService.RevokeDeviceAsync(canteenId, deviceId);
        if (!success)
        {
            return NotFound(new { message = "TV device not found." });
        }

        // Notify TV group that device was revoked
        if (_hubContext != null)
        {
            await _hubContext.Clients.Group($"canteen_tv_{canteenId}").SendAsync("TvDeviceRevoked", new { TvDeviceId = deviceId, CanteenId = canteenId });
        }

        return Ok(new { message = "Smart TV device successfully revoked." });
    }
}
