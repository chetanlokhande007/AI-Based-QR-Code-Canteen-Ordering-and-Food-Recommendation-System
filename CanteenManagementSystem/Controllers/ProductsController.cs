using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    /// <summary>Get all products for a canteen.</summary>
    [HttpGet("canteen/{canteenId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCanteen(int canteenId, [FromQuery] bool availableOnly = false)
        => Ok(await _service.GetAllByCanteenAsync(canteenId, availableOnly));

    /// <summary>Get all products for a specific category within a canteen.</summary>
    [HttpGet("canteen/{canteenId:int}/category/{categoryId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCategory(int canteenId, int categoryId, [FromQuery] bool availableOnly = false)
        => Ok(await _service.GetByCategoryAsync(canteenId, categoryId, availableOnly));

    /// <summary>Get a product by ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await _service.GetByIdAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Create a new product (Owner only).</summary>
    [HttpPost]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.CanteenId = ownerCanteenId.Value;
        try
        {
            var created = await _service.CreateAsync(ownerCanteenId.Value, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.ProductId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update an existing product (Owner only).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await _service.UpdateAsync(ownerCanteenId.Value, id, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Delete a product by ID (Owner only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(int id)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null)
        {
            return Forbid();
        }

        var deleted = await _service.DeleteAsync(ownerCanteenId.Value, id);
        return deleted ? NoContent() : NotFound();
    }
}
