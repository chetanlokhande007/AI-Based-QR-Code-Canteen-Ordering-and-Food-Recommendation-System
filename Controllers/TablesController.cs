using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/canteens/{canteenId:int}/tables")]
[Produces("application/json")]
public class TablesController : ControllerBase
{
    private readonly ICanteenTableService _service;

    public TablesController(ICanteenTableService service)
    {
        _service = service;
    }

    /// <summary>Get all tables for a canteen.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CanteenTableDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(int canteenId)
        => Ok(await _service.GetAllByCanteenAsync(canteenId));

    /// <summary>Get a table by ID.</summary>
    [HttpGet("{tableId:int}")]
    [ProducesResponseType(typeof(CanteenTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int canteenId, int tableId)
    {
        var dto = await _service.GetByIdAsync(canteenId, tableId);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Lookup a table by its QR token.</summary>
    [HttpGet("qr/{token}")]
    [ProducesResponseType(typeof(CanteenTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByQR(int canteenId, string token)
    {
        var dto = await _service.GetByQRTokenAsync(token);
        if (dto is null || dto.CanteenId != canteenId)
        {
            return NotFound();
        }
        return Ok(dto);
    }

    /// <summary>Create a new table for a canteen (Owner only).</summary>
    [HttpPost]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(CanteenTableDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int canteenId, [FromBody] CreateCanteenTableDto dto)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.CanteenId = canteenId;
        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { canteenId, tableId = created.TableId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Update a table (Owner only).</summary>
    [HttpPut("{tableId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(CanteenTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int canteenId, int tableId, [FromBody] UpdateCanteenTableDto dto)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await _service.UpdateAsync(canteenId, tableId, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Delete a table (Owner only).</summary>
    [HttpDelete("{tableId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(int canteenId, int tableId)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
        {
            return Forbid();
        }

        var deleted = await _service.DeleteAsync(canteenId, tableId);
        return deleted ? NoContent() : NotFound();
    }
}
