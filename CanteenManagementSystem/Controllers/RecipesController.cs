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
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipeService;
    private readonly ICanteenService _canteenService;

    public RecipesController(IRecipeService recipeService, ICanteenService canteenService)
    {
        _recipeService = recipeService;
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

    /// <summary>Get all recipes for a specific canteen.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RecipeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllRecipes(int canteenId, [FromQuery] bool activeOnly = false)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var recipes = await _recipeService.GetAllRecipesAsync(canteenId, activeOnly);
        return Ok(recipes);
    }

    /// <summary>Get a specific recipe by recipe ID.</summary>
    [HttpGet("{recipeId:int}")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRecipeById(int canteenId, int recipeId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var recipe = await _recipeService.GetRecipeByIdAsync(canteenId, recipeId);
        if (recipe == null)
        {
            return NotFound(new { message = $"Recipe with ID {recipeId} was not found." });
        }

        return Ok(recipe);
    }

    /// <summary>Get a recipe by its associated Product ID.</summary>
    [HttpGet("product/{productId:int}")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRecipeByProductId(int canteenId, int productId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var recipe = await _recipeService.GetRecipeByProductIdAsync(canteenId, productId);
        if (recipe == null)
        {
            return NotFound(new { message = $"No recipe found for product ID {productId}." });
        }

        return Ok(recipe);
    }

    /// <summary>Get recipe costing and profitability summary for all products in the canteen.</summary>
    [HttpGet("costing-summary")]
    [ProducesResponseType(typeof(IEnumerable<RecipeCostingSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCostingSummary(int canteenId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var summaries = await _recipeService.GetCostingSummariesAsync(canteenId);
        return Ok(summaries);
    }

    /// <summary>Get live portion availability for a dish based on raw ingredient stock.</summary>
    [HttpGet("product/{productId:int}/availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RecipeAvailabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPortionAvailability(int canteenId, int productId)
    {
        if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen not found or inactive." });
        }

        var availability = await _recipeService.GetPortionAvailabilityAsync(canteenId, productId);
        if (availability == null)
        {
            return NotFound(new { message = $"No recipe defined for product ID {productId}." });
        }

        return Ok(availability);
    }

    /// <summary>Create a new Bill of Materials recipe for a product.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRecipe(int canteenId, [FromBody] CreateRecipeDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        try
        {
            var created = await _recipeService.CreateRecipeAsync(canteenId, dto);
            return CreatedAtAction(nameof(GetRecipeById), new { canteenId, recipeId = created.RecipeId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Update an existing recipe and its ingredient quantities.</summary>
    [HttpPut("{recipeId:int}")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRecipe(int canteenId, int recipeId, [FromBody] UpdateRecipeDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        try
        {
            var updated = await _recipeService.UpdateRecipeAsync(canteenId, recipeId, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Delete a recipe.</summary>
    [HttpDelete("{recipeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteRecipe(int canteenId, int recipeId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var deleted = await _recipeService.DeleteRecipeAsync(canteenId, recipeId);
        if (!deleted)
        {
            return NotFound(new { message = $"Recipe with ID {recipeId} was not found." });
        }

        return NoContent();
    }
}

