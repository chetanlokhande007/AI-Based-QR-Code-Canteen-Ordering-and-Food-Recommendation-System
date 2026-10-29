using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Enums;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CanteensController : ControllerBase
{
    private readonly ICanteenService _service;
    private readonly IAuthService _authService;

    public CanteensController(ICanteenService service, IAuthService authService)
    {
        _service = service;
        _authService = authService;
    }

    /// <summary>Get all canteens (Platform Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "PlatformAdmin")]
    [ProducesResponseType(typeof(IEnumerable<CanteenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Get public display information for a canteen by ID (Customer access).</summary>
    [HttpGet("{id:int}/public-info")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicCanteenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicInfo(int id)
    {
        var dto = await _service.GetByIdAsync(id);
        if (dto is null || !dto.IsActive || dto.TenantStatus != TenantStatus.Active)
        {
            return NotFound();
        }

        return Ok(new PublicCanteenDto
        {
            CanteenId = dto.CanteenId,
            Name = dto.Name,
            Slug = dto.Slug,
            Location = dto.Location,
            CanteenType = dto.CanteenType,
            Description = dto.Description,
            AddressLine1 = dto.AddressLine1,
            City = dto.City,
            State = dto.State,
            PostalCode = dto.PostalCode,
            OperatingHoursJson = dto.OperatingHoursJson,
            ImageUrl = dto.ImageUrl,
            IsActive = dto.IsActive
        });
    }

    /// <summary>Get public display information for a canteen by URL slug (Customer access, e.g. /c/deshmuk-catten).</summary>
    [HttpGet("by-slug/{slug}/public-info")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicCanteenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicInfoBySlug(string slug)
    {
        var dto = await _service.GetBySlugAsync(slug);
        if (dto is null || !dto.IsActive || dto.TenantStatus != TenantStatus.Active)
        {
            return NotFound();
        }

        return Ok(new PublicCanteenDto
        {
            CanteenId = dto.CanteenId,
            Name = dto.Name,
            Slug = dto.Slug,
            Location = dto.Location,
            CanteenType = dto.CanteenType,
            Description = dto.Description,
            AddressLine1 = dto.AddressLine1,
            City = dto.City,
            State = dto.State,
            PostalCode = dto.PostalCode,
            OperatingHoursJson = dto.OperatingHoursJson,
            ImageUrl = dto.ImageUrl,
            IsActive = dto.IsActive
        });
    }

    /// <summary>Get all canteens owned by the authenticated owner with live summary metrics.</summary>
    [HttpGet("my")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(IEnumerable<CanteenSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyCanteens()
    {
        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return Unauthorized();

        var summaries = await _service.GetOwnerCanteensAsync(ownerId.Value);
        return Ok(summaries);
    }

    /// <summary>Get a canteen by ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CanteenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await _service.GetByIdAsync(id);
        if (dto is null) return NotFound();

        if (!User.IsInRole("PlatformAdmin") && dto.TenantStatus != TenantStatus.Active)
        {
            return NotFound();
        }

        // Mask confidential bank & settlement details for non-owners and non-admins
        var ownerId = User.GetOwnerId();
        var isOwner = ownerId.HasValue && dto.OwnerId == ownerId.Value;
        if (!User.IsInRole("PlatformAdmin") && !isOwner)
        {
            dto.BankAccountHolder = null;
            dto.BankName = null;
            dto.AccountNumber = null;
            dto.IfscCode = null;
            dto.AccountType = null;
            dto.UpiId = null;
        }

        return Ok(dto);
    }

    /// <summary>Create a new canteen (Owner only).</summary>
    [HttpPost]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(CanteenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCanteenDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ownerId = User.GetOwnerId();
        try
        {
            var created = await _service.CreateAsync(dto, ownerId);
            if (ownerId.HasValue)
            {
                created.Token = await _authService.GenerateTokenForOwnerAsync(ownerId.Value);
            }
            return CreatedAtAction(nameof(GetById), new { id = created.CanteenId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Update an existing canteen (Owner only).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(CanteenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCanteenDto dto)
    {
        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return Unauthorized();

        var isAuthorized = await _service.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, id);
        if (!isAuthorized)
        {
            return Forbid();
        }

        var updated = await _service.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Delete a canteen by ID (Owner only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(int id)
    {
        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return Unauthorized();

        var isAuthorized = await _service.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, id);
        if (!isAuthorized)
        {
            return Forbid();
        }

        var deleted = await _service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
