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
public class RecommendationsController : ControllerBase
{
    private readonly IFoodRecommendationService _recommendationService;
    private readonly ICanteenService _canteenService;

    public RecommendationsController(
        IFoodRecommendationService recommendationService,
        ICanteenService canteenService)
    {
        _recommendationService = recommendationService;
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

    /// <summary>Get AI-ranked menu recommendations for customer browsing (Time-of-day, Popularity, Value).</summary>
    [HttpGet("menu")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<FoodRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMenuRecommendations(
        int canteenId,
        [FromQuery] string? foodType = null,
        [FromQuery] int limit = 6)
    {
        if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen not found or inactive." });
        }

        var recommendations = await _recommendationService.GetMenuRecommendationsAsync(canteenId, foodType, limit);
        return Ok(recommendations);
    }

    /// <summary>Get smart cart add-on recommendations based on basket association mining (Frequently Bought Together).</summary>
    [HttpPost("cart-addons")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CartAddonRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCartAddons(
        int canteenId,
        [FromBody] CartAddonRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen not found or inactive." });
        }

        var addons = await _recommendationService.GetCartAddonsAsync(canteenId, request);
        return Ok(addons);
    }

    /// <summary>Record customer interaction events with recommended dishes (Privacy-preserving telemetry).</summary>
    [HttpPost("events")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordEvent(
        int canteenId,
        [FromBody] RecordRecommendationEventDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = await _recommendationService.RecordEventAsync(canteenId, dto);
        return success ? Ok(new { success = true }) : BadRequest(new { message = "Failed to record event." });
    }

    /// <summary>Get recommendation performance metrics (CTR, Conversion Rate, Top Converting Dishes) for Canteen Owner.</summary>
    [HttpGet("insights")]
    [Authorize]
    [ProducesResponseType(typeof(RecommendationInsightsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInsights(
        int canteenId,
        [FromQuery] int daysBack = 30)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var insights = await _recommendationService.GetInsightsAsync(canteenId, daysBack);
        return Ok(insights);
    }
}

