using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class OrderService : IOrderService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending", "Confirmed", "InPreparation", "Ready", "Completed", "Cancelled"
    };

    private readonly IOrderRepository _orderRepo;
    private readonly IProductRepository _productRepo;
    private readonly ICanteenTableRepository _tableRepo;
    private readonly IOrderNotificationService _notificationService;

    public OrderService(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        ICanteenTableRepository tableRepo,
        IOrderNotificationService notificationService)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _tableRepo = tableRepo;
        _notificationService = notificationService;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, string qrToken)
    {
        if (dto == null || dto.Items == null || dto.Items.Count == 0)
        {
            throw new InvalidOrderException("An order must contain at least one item.");
        }

        if (string.IsNullOrWhiteSpace(qrToken))
        {
            throw new InvalidOrderException("A valid QR token is required to place an order.");
        }

        if (dto.Items.Any(i => i.Quantity <= 0))
        {
            throw new InvalidOrderException("Quantity for each item must be at least 1.");
        }

        // Consolidate duplicate products in the request payload
        var consolidatedItems = dto.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new CreateOrderItemDto
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        // 1. Resolve and validate the table by its QR token
        var table = await _tableRepo.GetByQRTokenAsync(qrToken);
        if (table == null || !table.IsAvailable || table.TableId != dto.TableId)
        {
            throw new InvalidOrderException("The table is invalid, unavailable, or does not match the provided QR token.");
        }

        int canteenId = table.CanteenId;

        // 2. Fetch and validate each product (Anti-BOLA check: must belong strictly to this canteen)
        var products = new Dictionary<int, Product>();
        foreach (var item in consolidatedItems)
        {
            var product = await _productRepo.GetByIdAsync(canteenId, item.ProductId);
            if (product == null)
            {
                throw new InvalidOrderException($"Product ID {item.ProductId} does not exist or does not belong to this canteen.");
            }

            if (!product.IsAvailable)
            {
                throw new InvalidOrderException($"Product '{product.Name}' (ID: {product.ProductId}) is currently unavailable.");
            }

            products[item.ProductId] = product;
        }

        // 3. Atomically reserve stock item-by-item; rollback on any failure
        var deducted = new List<(int ProductId, int Quantity)>();
        foreach (var item in consolidatedItems)
        {
            var success = await _productRepo.DeductStockAsync(canteenId, item.ProductId, item.Quantity);
            if (!success)
            {
                // Rollback previously deducted items in this order
                foreach (var (pId, qty) in deducted)
                {
                    await _productRepo.RestoreStockAsync(canteenId, pId, qty);
                }

                var failedProduct = products[item.ProductId];
                throw new InsufficientStockException(
                    failedProduct.ProductId,
                    failedProduct.Name,
                    item.Quantity,
                    failedProduct.StockQuantity);
            }

            deducted.Add((item.ProductId, item.Quantity));
        }

        // 4. Calculate prices strictly on the server (zero client trust)
        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();
        foreach (var item in consolidatedItems)
        {
            var product = products[item.ProductId];
            decimal unitPrice = product.Price;
            totalAmount += unitPrice * item.Quantity;

            orderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice
            });
        }

        // 5. Generate human-friendly unique order number and cryptographically random tracking token
        string orderNumber;
        int attempts = 0;
        do
        {
            orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            attempts++;
        }
        while (await _orderRepo.OrderNumberExistsAsync(canteenId, orderNumber) && attempts < 10);

        var trackingToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        // 6. Persist order
        var order = new Order
        {
            CanteenId = canteenId,
            TableId = table.TableId,
            OrderNumber = orderNumber,
            Status = "Pending",
            PaymentStatus = "Pending",
            TotalAmount = totalAmount,
            TrackingToken = trackingToken,
            CreatedAt = DateTime.UtcNow,
            Items = orderItems
        };

        var created = await _orderRepo.AddAsync(order);

        // Populate navigation names for the DTO
        foreach (var item in created.Items)
        {
            if (products.TryGetValue(item.ProductId, out var prod))
            {
                item.Product = prod;
            }
        }

        var orderDto = ToDto(created);
        await _notificationService.NotifyNewOrderPlacedAsync(orderDto);
        return orderDto;
    }

    public async Task<OrderDto?> GetByIdAsync(int canteenId, int orderId)
    {
        var entity = await _orderRepo.GetByIdAsync(canteenId, orderId, asNoTracking: true);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<OrderDto?> GetByTrackingTokenAsync(string trackingToken)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return null;
        }

        var entity = await _orderRepo.GetByTrackingTokenAsync(trackingToken, asNoTracking: true);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IEnumerable<OrderDto>> GetAllByCanteenAsync(int canteenId)
    {
        var entities = await _orderRepo.GetAllByCanteenAsync(canteenId);
        return entities.Select(ToDto);
    }

    public async Task<OrderDto?> UpdateStatusAsync(int canteenId, int orderId, string newStatus)
    {
        if (string.IsNullOrWhiteSpace(newStatus) || !ValidStatuses.Contains(newStatus))
        {
            throw new InvalidOrderException($"Status '{newStatus}' is invalid. Allowed: {string.Join(", ", ValidStatuses)}.");
        }

        var order = await _orderRepo.GetByIdAsync(canteenId, orderId, asNoTracking: false);
        if (order is null)
        {
            return null;
        }

        ValidateStatusTransition(order.Status, newStatus);

        order.Status = newStatus;
        var updated = await _orderRepo.UpdateAsync(order);
        var dto = ToDto(updated);

        if (string.Equals(newStatus, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(newStatus, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.NotifyOrderStatusUpdatedAsync(order.OrderId, order.OrderNumber, newStatus, DateTime.UtcNow);
        }

        return dto;
    }

    private static void ValidateStatusTransition(string currentStatus, string nextStatus)
    {
        if (string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            return; // No-op transition
        }

        if (string.Equals(currentStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOrderException($"Cannot change status of a completed or cancelled order ({currentStatus} -> {nextStatus}).");
        }

        bool isAllowed = currentStatus switch
        {
            "Pending" => nextStatus is "Confirmed" or "Cancelled",
            "Confirmed" => nextStatus is "InPreparation" or "Cancelled",
            "InPreparation" => nextStatus is "Ready" or "Cancelled",
            "Ready" => nextStatus is "Completed" or "Cancelled",
            _ => false
        };

        if (!isAllowed)
        {
            throw new InvalidOrderException($"Illegal order status transition from '{currentStatus}' to '{nextStatus}'.");
        }
    }

    private static OrderDto ToDto(Order o) => new()
    {
        OrderId = o.OrderId,
        CanteenId = o.CanteenId,
        TableId = o.TableId,
        OrderNumber = o.OrderNumber,
        Status = o.Status,
        PaymentStatus = o.PaymentStatus,
        TotalAmount = o.TotalAmount,
        TrackingToken = o.TrackingToken,
        CreatedAt = o.CreatedAt,
        Items = o.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Subtotal = i.Subtotal
        }).ToList()
    };
}
