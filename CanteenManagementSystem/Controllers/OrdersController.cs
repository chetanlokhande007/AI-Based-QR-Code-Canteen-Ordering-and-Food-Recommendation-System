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
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICanteenService _canteenService;

    public OrdersController(IOrderService orderService, ICanteenService canteenService)
    {
        _orderService = orderService;
        _canteenService = canteenService;
    }

    private async Task<bool> IsAuthorizedAsync(int canteenId)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId.HasValue && ownerCanteenId.Value == canteenId)
        {
            return true;
        }

        var ownerId = User.GetOwnerId();
        if (ownerId.HasValue && await _canteenService.IsOwnerAuthorizedAsync(ownerId.Value, canteenId))
        {
            return true;
        }

        return false;
    }

    /// <summary>Create a new customer order using a table QR token.</summary>
    [HttpPost("api/orders")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOrder(
        [FromQuery] string qrToken,
        [FromBody] CreateOrderDto dto)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return BadRequest(new { message = "Query parameter 'qrToken' is required." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _orderService.CreateAsync(dto, qrToken);
            return CreatedAtAction(
                nameof(TrackOrder),
                new { trackingToken = created.TrackingToken },
                created);
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new { message = ex.Message, productId = ex.ProductId, productName = ex.ProductName });
        }
        catch (InvalidOrderException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Track an order's status securely using its unguessable tracking token (Customer access).</summary>
    [HttpGet("api/orders/track/{trackingToken}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrackOrder(string trackingToken)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return NotFound();
        }

        var order = await _orderService.GetByTrackingTokenAsync(trackingToken);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Get all orders for a canteen (Owner only).</summary>
    [HttpGet("api/canteens/{canteenId:int}/orders")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllByCanteen(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var orders = await _orderService.GetAllByCanteenAsync(canteenId);
        return Ok(orders);
    }

    /// <summary>Get an order by ID (Owner only).</summary>
    [HttpGet("api/canteens/{canteenId:int}/orders/{orderId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int canteenId, int orderId)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var order = await _orderService.GetByIdAsync(canteenId, orderId);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Update an order's status through the kitchen workflow (Owner only).</summary>
    [HttpPatch("api/canteens/{canteenId:int}/orders/{orderId:int}/status")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int canteenId,
        int orderId,
        [FromBody] UpdateOrderStatusDto dto)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updated = await _orderService.UpdateStatusAsync(canteenId, orderId, dto.Status);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOrderException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
