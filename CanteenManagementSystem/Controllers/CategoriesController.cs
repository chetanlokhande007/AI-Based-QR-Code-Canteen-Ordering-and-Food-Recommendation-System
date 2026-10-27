using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/canteens/{canteenId:int}/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service)
    {
        _service = service;
    }

    /// <summary>Get all categories for a canteen.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(int canteenId)
        => Ok(await _service.GetAllByCanteenAsync(canteenId));

    /// <summary>Get a category by ID.</summary>
    [HttpGet("{categoryId:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int canteenId, int categoryId)
    {
        var dto = await _service.GetByIdAsync(canteenId, categoryId);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Create a new category for a canteen (Owner only).</summary>
    [HttpPost]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(int canteenId, [FromBody] CreateCategoryDto dto)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.CanteenId = canteenId;
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { canteenId, categoryId = created.CategoryId }, created);
    }

    /// <summary>Update a category (Owner only).</summary>
    [HttpPut("{categoryId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(int canteenId, int categoryId, [FromBody] UpdateCategoryDto dto)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
        {
            return Forbid();
        }

        var updated = await _service.UpdateAsync(canteenId, categoryId, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Delete a category (Owner only).</summary>
    [HttpDelete("{categoryId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int canteenId, int categoryId)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
        {
            return Forbid();
        }

        try
        {
            var deleted = await _service.DeleteAsync(canteenId, categoryId);
            return deleted ? NoContent() : NotFound();
        }
        catch (CategoryNotEmptyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
