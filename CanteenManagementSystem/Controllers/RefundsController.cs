using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Produces("application/json")]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refundService;
    private readonly IOrderService _orderService;
    private readonly ICanteenService _canteenService;

    public RefundsController(
        IRefundService refundService,
        IOrderService orderService,
        ICanteenService canteenService)
    {
        _refundService = refundService;
        _orderService = orderService;
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

    /// <summary>Get all refunds for a specific canteen (Owner scoped).</summary>
    [HttpGet("api/canteens/{canteenId:int}/refunds")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<RefundDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRefundsByCanteen(int canteenId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var list = await _refundService.GetRefundsByCanteenAsync(canteenId);
        return Ok(list);
    }

    /// <summary>Get all order cancellations for a specific canteen (Owner scoped).</summary>
    [HttpGet("api/canteens/{canteenId:int}/cancellations")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<OrderCancellationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCancellationsByCanteen(int canteenId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var list = await _orderService.GetCancellationsByCanteenAsync(canteenId);
        return Ok(list);
    }

    /// <summary>Get cancellation audit record for a specific order.</summary>
    [HttpGet("api/canteens/{canteenId:int}/orders/{orderId:int}/cancellation")]
    [Authorize]
    [ProducesResponseType(typeof(OrderCancellationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCancellationByOrderId(int canteenId, int orderId)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var cancellation = await _orderService.GetCancellationByOrderIdAsync(canteenId, orderId);
        return cancellation is null ? NotFound(new { message = "No cancellation record found for this order." }) : Ok(cancellation);
    }

    /// <summary>Canteen staff cancels an order with a reason and initiates full refund if paid (Owner scoped).</summary>
    [HttpPost("api/canteens/{canteenId:int}/orders/{orderId:int}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrderFromCanteen(
        int canteenId,
        int orderId,
        [FromBody] CancelOrderRequestDto dto)
    {
        if (!await IsCallerAuthorizedForCanteenAsync(canteenId))
        {
            return Forbid();
        }

        var actorOwnerId = User.GetOwnerId() ?? 0;

        try
        {
            var cancelled = await _orderService.CancelOrderFromCanteenAsync(
                canteenId,
                orderId,
                dto?.Reason ?? "Cancelled by canteen staff",
                actorOwnerId);

            return cancelled is null
                ? NotFound(new { message = "Order not found." })
                : Ok(cancelled);
        }
        catch (InvalidOrderException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get all refunds across the platform (Super Admin only).</summary>
    [HttpGet("api/refunds")]
    [Authorize(Roles = "PlatformAdmin")]
    [ProducesResponseType(typeof(IEnumerable<RefundDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRefunds()
    {
        var list = await _refundService.GetAllRefundsAsync();
        return Ok(list);
    }

    /// <summary>Get refund aggregate financial metrics (Super Admin / Canteen Owner).</summary>
    [HttpGet("api/refunds/metrics")]
    [Authorize]
    [ProducesResponseType(typeof(RefundMetricsSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRefundMetrics([FromQuery] int? canteenId = null)
    {
        if (canteenId.HasValue && !await IsCallerAuthorizedForCanteenAsync(canteenId.Value))
        {
            return Forbid();
        }

        if (!canteenId.HasValue && !User.IsInRole("PlatformAdmin"))
        {
            return Forbid();
        }

        var metrics = await _refundService.GetRefundMetricsSummaryAsync(canteenId);
        return Ok(metrics);
    }

    /// <summary>Process a discretionary refund initiated by Super Admin.</summary>
    [HttpPost("api/refunds/admin-discretionary")]
    [Authorize(Roles = "PlatformAdmin")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessAdminDiscretionaryRefund([FromBody] ProcessRefundRequestDto dto)
    {
        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "Refund amount must be greater than zero." });
        }

        var adminOwnerId = User.GetOwnerId() ?? 0;

        try
        {
            var result = await _refundService.ProcessAdminDiscretionaryRefundAsync(
                dto.OrderId,
                dto.Amount,
                dto.Reason,
                adminOwnerId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

