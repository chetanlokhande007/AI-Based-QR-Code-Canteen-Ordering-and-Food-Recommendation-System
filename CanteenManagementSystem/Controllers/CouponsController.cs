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
[Route("api/canteens/{canteenId:int}/[controller]")]
[Produces("application/json")]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _couponService;
    private readonly ICanteenService _canteenService;

    public CouponsController(ICouponService couponService, ICanteenService canteenService)
    {
        _couponService = couponService;
        _canteenService = canteenService;
    }

    private async Task<bool> IsCallerAuthorizedForCanteenAsync(int canteenId)
    {
        if (User.IsInRole("PlatformAdmin"))
        {
            return true;
        }

        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return false;

        var tokenCanteenId = User.GetCanteenId();
        if (tokenCanteenId.HasValue && tokenCanteenId.Value == canteenId)
        {
            return true;
        }

        return await _canteenService.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, canteenId);
    }

    /// <summary>
    /// Public endpoint for customers to discover active offers and promo codes for this canteen.
    /// </summary>
    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<PublicCouponDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveCoupons([FromRoute] int canteenId)
    {
        var activeCoupons = await _couponService.GetActiveCouponsForCustomerAsync(canteenId);
        return Ok(activeCoupons);
    }

    /// <summary>
    /// Public endpoint to validate a promo code and calculate discount preview during cart checkout.
    /// </summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ValidateCouponResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateCoupon(
        [FromRoute] int canteenId,
        [FromBody] ValidateCouponRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Coupon code is required." });

        var result = await _couponService.ValidateCouponAsync(canteenId, request);
        return Ok(result);
    }

    /// <summary>
    /// Canteen Owner / Admin endpoint to list all configured coupons.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<CouponDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllCoupons([FromRoute] int canteenId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
            return Forbid();

        var coupons = await _couponService.GetCouponsForCanteenAsync(canteenId);
        return Ok(coupons);
    }

    /// <summary>
    /// Canteen Owner / Admin endpoint to get a single coupon by ID.
    /// </summary>
    [HttpGet("{couponId:int}")]
    [Authorize]
    [ProducesResponseType(typeof(CouponDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCouponById([FromRoute] int canteenId, [FromRoute] int couponId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
            return Forbid();

        try
        {
            var coupon = await _couponService.GetCouponByIdAsync(canteenId, couponId);
            return Ok(coupon);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Canteen Owner / Admin endpoint to create a new coupon for this canteen.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CouponDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCoupon(
        [FromRoute] int canteenId,
        [FromBody] CreateCouponDto dto)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _couponService.CreateCouponAsync(canteenId, dto);
            return CreatedAtAction(nameof(GetCouponById), new { canteenId, couponId = created.CouponId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Canteen Owner / Admin endpoint to update an existing coupon.
    /// </summary>
    [HttpPut("{couponId:int}")]
    [Authorize]
    [ProducesResponseType(typeof(CouponDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCoupon(
        [FromRoute] int canteenId,
        [FromRoute] int couponId,
        [FromBody] UpdateCouponDto dto)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _couponService.UpdateCouponAsync(canteenId, couponId, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Canteen Owner / Admin endpoint to toggle a coupon's active/inactive status instantly.
    /// </summary>
    [HttpPatch("{couponId:int}/toggle")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleCouponStatus([FromRoute] int canteenId, [FromRoute] int couponId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
            return Forbid();

        try
        {
            var isActive = await _couponService.ToggleCouponStatusAsync(canteenId, couponId);
            return Ok(new { couponId, isActive, message = $"Coupon status changed to {(isActive ? "Active" : "Inactive")}." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Canteen Owner / Admin endpoint to delete a coupon.
    /// </summary>
    [HttpDelete("{couponId:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCoupon([FromRoute] int canteenId, [FromRoute] int couponId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
            return Forbid();

        var success = await _couponService.DeleteCouponAsync(canteenId, couponId);
        if (!success)
            return NotFound(new { message = $"Coupon #{couponId} not found." });

        return Ok(new { message = "Coupon deleted successfully." });
    }

    /// <summary>
    /// Canteen Owner / Admin endpoint to get a redemption summary report.
    /// </summary>
    [HttpGet("reports/redemptions")]
    [Authorize]
    [ProducesResponseType(typeof(CouponRedemptionReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRedemptionReport([FromRoute] int canteenId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
            return Forbid();

        var report = await _couponService.GetRedemptionReportAsync(canteenId);
        return Ok(report);
    }
}
