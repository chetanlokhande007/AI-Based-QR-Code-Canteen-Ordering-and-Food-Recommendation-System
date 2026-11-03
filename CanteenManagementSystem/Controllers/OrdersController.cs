using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IOrderNotificationService? _notificationService;

    public OrdersController(
        IOrderService orderService,
        ICanteenService canteenService,
        IOrderNotificationService? notificationService = null)
    {
        _orderService = orderService;
        _canteenService = canteenService;
        _notificationService = notificationService;
    }

    private async Task<bool> IsAuthorizedAsync(int canteenId)
    {
        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return false;

        var activeCanteenId = User.GetCanteenId();
        if (!activeCanteenId.HasValue || activeCanteenId.Value != canteenId)
        {
            return false;
        }

        return await _canteenService.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, canteenId);
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

    /// <summary>Cancel a pending order securely using its unguessable tracking token (Customer access).</summary>
    [HttpPost("api/orders/track/{trackingToken}/cancel")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(string trackingToken, [FromBody] CancelOrderRequestDto? dto = null)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return NotFound();
        }

        try
        {
            var order = await _orderService.CancelOrderByTrackingTokenAsync(trackingToken, dto?.Reason);
            return order is null ? NotFound(new { message = "Order not found." }) : Ok(order);
        }
        catch (InvalidOrderException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

    /// <summary>
    /// Public TV Display endpoint for dining hall wall monitors.
    /// Returns active tokens partitioned into Preparing and Ready without customer PII.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/tv-display")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CanteenTvDisplayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTvDisplayData(int canteenId)
    {
        var canteen = await _canteenService.GetByIdAsync(canteenId);
        if (canteen == null)
        {
            return NotFound(new { message = "Canteen not found." });
        }

        var orders = await _orderService.GetAllByCanteenAsync(canteenId);
        var activeOrders = orders.Where(o =>
            string.Equals(o.Status, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Status, "Preparing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Status, "Ready", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        var preparing = activeOrders
            .Where(o => string.Equals(o.Status, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Status, "Preparing", StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.CreatedAt)
            .Select(MapToTvToken)
            .ToList();

        var ready = activeOrders
            .Where(o => string.Equals(o.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.CreatedAt)
            .Select(MapToTvToken)
            .ToList();

        return Ok(new CanteenTvDisplayDto
        {
            CanteenId = canteenId,
            CanteenName = canteen.Name,
            PreparingOrders = preparing,
            ReadyOrders = ready
        });
    }

    /// <summary>
    /// Explicitly triggers a TV voice announcement for a ready order (e.g. customer recall).
    /// </summary>
    [HttpPost("api/canteens/{canteenId:int}/orders/{orderId:int}/announce-tv")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnnounceOrderOnTv(int canteenId, int orderId)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var order = await _orderService.GetByIdAsync(canteenId, orderId);
        if (order == null)
        {
            return NotFound(new { message = "Order not found." });
        }

        var token = ExtractToken(order.OrderNumber);
        var message = $"Token number {token}, your order is ready for pickup!";

        if (_notificationService != null)
        {
            await _notificationService.NotifyTvAnnouncementAsync(canteenId, order.OrderId, order.OrderNumber, token, message);
        }

        return Ok(new { message = "TV voice announcement triggered successfully.", tokenNumber = token });
    }

    private static TvTokenDto MapToTvToken(OrderDto order)
    {
        var token = ExtractToken(order.OrderNumber);
        return new TvTokenDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            TokenNumber = token,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.CreatedAt,
            ItemCount = order.Items?.Count ?? 0
        };
    }

    private static string ExtractToken(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) return string.Empty;
        var parts = orderNumber.Split('-');
        return parts.Length > 0 ? parts[^1] : orderNumber;
    }
}
