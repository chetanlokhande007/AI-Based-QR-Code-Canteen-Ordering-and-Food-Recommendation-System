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
    private readonly IQrCodeService _qrCodeService;
    private readonly ICanteenService _canteenService;

    public TablesController(
        ICanteenTableService service,
        IQrCodeService qrCodeService,
        ICanteenService canteenService)
    {
        _service = service;
        _qrCodeService = qrCodeService;
        _canteenService = canteenService;
    }

    private async Task<bool> IsAuthorizedAsync(int canteenId)
    {
        if (User.IsInRole("PlatformAdmin")) return true;

        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return false;

        var activeCanteenId = User.GetCanteenId();
        if (!activeCanteenId.HasValue || activeCanteenId.Value != canteenId)
        {
            return false;
        }

        return await _canteenService.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, canteenId);
    }

    /// <summary>Get all tables for a canteen.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CanteenTableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(int canteenId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!await IsAuthorizedAsync(canteenId))
            {
                return Forbid();
            }
        }
        else if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen is inactive, suspended, or does not exist." });
        }
        return Ok(await _service.GetAllByCanteenAsync(canteenId));
    }

    /// <summary>Get a table by ID.</summary>
    [HttpGet("{tableId:int}")]
    [ProducesResponseType(typeof(CanteenTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int canteenId, int tableId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!await IsAuthorizedAsync(canteenId))
            {
                return Forbid();
            }
        }
        else if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen is inactive, suspended, or does not exist." });
        }
        var dto = await _service.GetByIdAsync(canteenId, tableId);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Lookup a table by its QR token.</summary>
    [HttpGet("qr/{token}")]
    [ProducesResponseType(typeof(CanteenTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByQR(int canteenId, string token)
    {
        if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen is inactive, suspended, or does not exist." });
        }
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
        if (!await IsAuthorizedAsync(canteenId))
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
        if (!await IsAuthorizedAsync(canteenId))
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
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var deleted = await _service.DeleteAsync(canteenId, tableId);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Download or view the QR code PNG image for a table.</summary>
    [HttpGet("{tableId:int}/qr-image")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQrImage(int canteenId, int tableId, [FromQuery] int pixelsPerModule = 10)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!await IsAuthorizedAsync(canteenId))
            {
                return Forbid();
            }
        }
        else if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen is inactive, suspended, or does not exist." });
        }

        var table = await _service.GetByIdAsync(canteenId, tableId);
        if (table is null)
        {
            return NotFound();
        }

        var menuUrl = _qrCodeService.GenerateTableMenuUrl(canteenId, tableId, table.QRToken);
        var pngBytes = _qrCodeService.GenerateQrCodePng(menuUrl, Math.Clamp(pixelsPerModule, 5, 30));

        return File(pngBytes, "image/png", $"table-{tableId}-qr.png");
    }

    /// <summary>Get the customer menu URL encoded in the table's QR code.</summary>
    [HttpGet("{tableId:int}/qr-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQrUrl(int canteenId, int tableId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!await IsAuthorizedAsync(canteenId))
            {
                return Forbid();
            }
        }
        else if (!await _canteenService.IsCanteenActiveAsync(canteenId))
        {
            return NotFound(new { message = "Canteen is inactive, suspended, or does not exist." });
        }

        var table = await _service.GetByIdAsync(canteenId, tableId);
        if (table is null)
        {
            return NotFound();
        }

        var menuUrl = _qrCodeService.GenerateTableMenuUrl(canteenId, tableId, table.QRToken);
        return Ok(new
        {
            tableId = table.TableId,
            canteenId = table.CanteenId,
            tableNumber = table.TableNumber,
            qrToken = table.QRToken,
            menuUrl
        });
    }

    /// <summary>Regenerate a table's QR token (Owner only).</summary>
    [HttpPost("{tableId:int}/regenerate-qr")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(CanteenTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateQr(int canteenId, int tableId)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var updated = await _service.RegenerateQrTokenAsync(canteenId, tableId);
        return updated is null ? NotFound() : Ok(updated);
    }
}
