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
[Authorize]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ICanteenService _canteenService;

    public InventoryController(IInventoryService inventoryService, ICanteenService canteenService)
    {
        _inventoryService = inventoryService;
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

    /// <summary>Get all raw inventory ingredients for a specific canteen.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IngredientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllIngredients(int canteenId, [FromQuery] bool activeOnly = false)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to view inventory for this canteen." });
        }

        var list = await _inventoryService.GetAllIngredientsAsync(canteenId, activeOnly);
        return Ok(list);
    }

    /// <summary>Get single ingredient details by ID.</summary>
    [HttpGet("{ingredientId:int}")]
    [ProducesResponseType(typeof(IngredientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIngredient(int canteenId, int ingredientId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to access this canteen's inventory." });
        }

        var ingredient = await _inventoryService.GetIngredientByIdAsync(canteenId, ingredientId);
        if (ingredient == null)
        {
            return NotFound(new { message = $"Ingredient #{ingredientId} was not found in this canteen." });
        }

        return Ok(ingredient);
    }

    /// <summary>Create a new raw ingredient for this canteen.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IngredientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateIngredient(int canteenId, [FromBody] CreateIngredientDto dto)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to modify this canteen's inventory." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var ownerId = User.GetOwnerId();
            var created = await _inventoryService.CreateIngredientAsync(canteenId, dto, ownerId);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update an existing ingredient's metadata, reorder levels, or cost.</summary>
    [HttpPut("{ingredientId:int}")]
    [ProducesResponseType(typeof(IngredientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateIngredient(int canteenId, int ingredientId, [FromBody] UpdateIngredientDto dto)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to modify this canteen's inventory." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updated = await _inventoryService.UpdateIngredientAsync(canteenId, ingredientId, dto);
            if (updated == null)
            {
                return NotFound(new { message = $"Ingredient #{ingredientId} was not found in this canteen." });
            }

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Adjust stock or log an ingredient purchase with an auditable transaction.</summary>
    [HttpPost("{ingredientId:int}/adjust")]
    [ProducesResponseType(typeof(IngredientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustStock(int canteenId, int ingredientId, [FromBody] AdjustStockDto dto)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to adjust stock for this canteen." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var ownerId = User.GetOwnerId();
            var updated = await _inventoryService.AdjustStockAsync(canteenId, ingredientId, dto, ownerId);
            if (updated == null)
            {
                return NotFound(new { message = $"Ingredient #{ingredientId} was not found in this canteen." });
            }

            return Ok(updated);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Record kitchen wastage (expired, spoiled, prep waste, burnt) and write down stock.</summary>
    [HttpPost("{ingredientId:int}/wastage")]
    [ProducesResponseType(typeof(IngredientWastageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordWastage(int canteenId, int ingredientId, [FromBody] RecordWastageDto dto)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to record wastage for this canteen." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var ownerId = User.GetOwnerId();
            var created = await _inventoryService.RecordWastageAsync(canteenId, ingredientId, dto, ownerId);
            if (created == null)
            {
                return NotFound(new { message = $"Ingredient #{ingredientId} was not found in this canteen." });
            }

            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get historical stock movement transactions.</summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(IEnumerable<StockTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTransactions(int canteenId, [FromQuery] int? ingredientId = null, [FromQuery] int limit = 50)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to view transactions for this canteen." });
        }

        var list = await _inventoryService.GetTransactionsAsync(canteenId, ingredientId, limit);
        return Ok(list);
    }

    /// <summary>Get historical wastage records.</summary>
    [HttpGet("wastages")]
    [ProducesResponseType(typeof(IEnumerable<IngredientWastageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWastages(int canteenId, [FromQuery] int? ingredientId = null, [FromQuery] int limit = 50)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to view wastage records for this canteen." });
        }

        var list = await _inventoryService.GetWastagesAsync(canteenId, ingredientId, limit);
        return Ok(list);
    }

    /// <summary>Get high-level inventory dashboard KPI metrics and low-stock alerts.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(InventoryDashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboardSummary(int canteenId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to view this canteen's inventory dashboard." });
        }

        var summary = await _inventoryService.GetDashboardSummaryAsync(canteenId);
        return Ok(summary);
    }
}

