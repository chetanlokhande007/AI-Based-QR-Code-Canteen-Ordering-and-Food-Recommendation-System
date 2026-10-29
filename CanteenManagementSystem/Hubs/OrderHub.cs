using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Canteen.DAL.Interfaces;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Hubs;

public class OrderHub : Hub
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICanteenRepository? _canteenRepository;
    private readonly ILogger<OrderHub> _logger;

    public OrderHub(
        IOrderRepository orderRepository,
        ILogger<OrderHub> logger)
        : this(orderRepository, null, logger)
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public OrderHub(
        IOrderRepository orderRepository,
        ICanteenRepository? canteenRepository,
        ILogger<OrderHub> logger)
    {
        _orderRepository = orderRepository;
        _canteenRepository = canteenRepository;
        _logger = logger;
    }

    /// <summary>
    /// When an authenticated canteen owner connects, automatically assign them
    /// to their authorized server-controlled canteen kitchen group if the canteen is active.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated == true && Context.User.IsInRole("CanteenOwner"))
        {
            var ownerId = Context.User.GetOwnerId();
            var canteenId = Context.User.GetCanteenId();
            if (ownerId.HasValue && canteenId.HasValue)
            {
                var isAuthorized = _canteenRepository == null ||
                    await _canteenRepository.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, canteenId.Value);

                if (isAuthorized)
                {
                    var groupName = $"canteen_{canteenId.Value}";
                    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                    _logger.LogInformation("Kitchen connection {ConnectionId} auto-joined group {GroupName}.", Context.ConnectionId, groupName);
                }
                else
                {
                    _logger.LogWarning("Connection {ConnectionId} belongs to suspended or unauthorized canteen {CanteenId}.", Context.ConnectionId, canteenId.Value);
                }
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Explicitly joins the kitchen group for the authenticated caller.
    /// Strictly derives the CanteenId from the caller's JWT claims to prevent tenant cross-access,
    /// and verifies that the tenant is active and authorized.
    /// </summary>
    [Authorize(Roles = "CanteenOwner")]
    public async Task JoinKitchenGroup()
    {
        var ownerId = Context.User?.GetOwnerId();
        var canteenId = Context.User?.GetCanteenId();
        if (!ownerId.HasValue || !canteenId.HasValue)
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to join kitchen group without a CanteenId claim.", Context.ConnectionId);
            throw new HubException("Caller is not associated with any canteen.");
        }

        if (_canteenRepository != null && !await _canteenRepository.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, canteenId.Value))
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to join kitchen group for suspended or unauthorized canteen {CanteenId}.", Context.ConnectionId, canteenId.Value);
            throw new HubException("Caller is not authorized for this canteen or the canteen is suspended.");
        }

        var groupName = $"canteen_{canteenId.Value}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} joined kitchen group {GroupName}.", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Allows an anonymous customer to subscribe to status notifications for their order.
    /// Access is strictly gated by validating the order ID against the unguessable tracking token.
    /// </summary>
    [AllowAnonymous]
    public async Task JoinOrderTrackingGroup(int orderId, string trackingToken)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(trackingToken))
        {
            throw new HubException("Valid order ID and tracking token are required.");
        }

        var order = await _orderRepository.GetByIdAsync(orderId, asNoTracking: true);
        if (order == null || string.IsNullOrWhiteSpace(order.TrackingToken))
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to track non-existent order {OrderId}.", Context.ConnectionId, orderId);
            throw new HubException("Invalid order or tracking token.");
        }

        // Constant-time string comparison to prevent timing attacks
        var tokenBytes = Encoding.UTF8.GetBytes(trackingToken);
        var expectedBytes = Encoding.UTF8.GetBytes(order.TrackingToken);

        if (!CryptographicOperations.FixedTimeEquals(tokenBytes, expectedBytes))
        {
            _logger.LogWarning("Connection {ConnectionId} supplied invalid tracking token for order {OrderId}.", Context.ConnectionId, orderId);
            throw new HubException("Invalid order or tracking token.");
        }

        var groupName = $"order_{orderId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Customer connection {ConnectionId} joined tracking group {GroupName}.", Context.ConnectionId, groupName);
    }
}
