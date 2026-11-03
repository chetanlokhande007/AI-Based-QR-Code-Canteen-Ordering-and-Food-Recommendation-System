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

    private readonly ICanteenRepository? _canteenRepo;
    private readonly IPaymentRepository? _paymentRepo;
    private readonly IRecipeRepository? _recipeRepo;
    private readonly IInventoryRepository? _inventoryRepo;
    private readonly IOrderCancellationRepository? _cancellationRepo;
    private readonly IRefundService? _refundService;
    private readonly ICouponService? _couponService;
    private readonly ICouponRepository? _couponRepo;

    public OrderService(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        ICanteenTableRepository tableRepo,
        IOrderNotificationService notificationService)
        : this(orderRepo, productRepo, tableRepo, null, null, notificationService, null, null)
    {
    }

    public OrderService(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        ICanteenTableRepository tableRepo,
        ICanteenRepository? canteenRepo,
        IOrderNotificationService notificationService)
        : this(orderRepo, productRepo, tableRepo, canteenRepo, null, notificationService, null, null)
    {
    }

    public OrderService(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        ICanteenTableRepository tableRepo,
        ICanteenRepository? canteenRepo,
        IPaymentRepository? paymentRepo,
        IOrderNotificationService notificationService)
        : this(orderRepo, productRepo, tableRepo, canteenRepo, paymentRepo, notificationService, null, null)
    {
    }

    public OrderService(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        ICanteenTableRepository tableRepo,
        ICanteenRepository? canteenRepo,
        IPaymentRepository? paymentRepo,
        IOrderNotificationService notificationService,
        IRecipeRepository? recipeRepo,
        IInventoryRepository? inventoryRepo)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _tableRepo = tableRepo;
        _canteenRepo = canteenRepo;
        _paymentRepo = paymentRepo;
        _notificationService = notificationService;
        _recipeRepo = recipeRepo;
        _inventoryRepo = inventoryRepo;
        _cancellationRepo = null;
        _refundService = null;
    }

    public OrderService(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        ICanteenTableRepository tableRepo,
        ICanteenRepository? canteenRepo,
        IPaymentRepository? paymentRepo,
        IOrderNotificationService notificationService,
        IRecipeRepository? recipeRepo,
        IInventoryRepository? inventoryRepo,
        IOrderCancellationRepository? cancellationRepo,
        IRefundService? refundService,
        ICouponService? couponService = null,
        ICouponRepository? couponRepo = null)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _tableRepo = tableRepo;
        _canteenRepo = canteenRepo;
        _paymentRepo = paymentRepo;
        _notificationService = notificationService;
        _recipeRepo = recipeRepo;
        _inventoryRepo = inventoryRepo;
        _cancellationRepo = cancellationRepo;
        _refundService = refundService;
        _couponService = couponService;
        _couponRepo = couponRepo;
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

        // Validate that the canteen exists and is active for customer orders
        if (_canteenRepo != null && !await _canteenRepo.IsCanteenActiveAsync(canteenId))
        {
            throw new InvalidOrderException("This canteen is currently inactive or suspended for orders.");
        }

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

        // 3–6. Atomically reserve stock and persist order inside a database transaction
        var created = await _orderRepo.ExecuteInTransactionAsync(async () =>
        {
            var deductedProducts = new List<(int ProductId, int Quantity)>();
            foreach (var item in consolidatedItems)
            {
                var success = await _productRepo.DeductStockAsync(canteenId, item.ProductId, item.Quantity);
                if (!success)
                {
                    // Rollback previously deducted items in this order
                    foreach (var (pId, qty) in deductedProducts)
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

                deductedProducts.Add((item.ProductId, item.Quantity));
            }

            // Recipe & Inventory Stock Reservation Check
            StockReservation? stockReservation = null;
            if (_recipeRepo != null && _inventoryRepo != null)
            {
                var recipes = await _recipeRepo.GetAllByCanteenAsync(canteenId, activeOnly: true);
                var recipeMap = recipes.ToDictionary(r => r.ProductId);

                var requiredIngredients = new Dictionary<int, (InventoryIngredient Ingredient, decimal RequiredQty)>();

                foreach (var item in consolidatedItems)
                {
                    if (recipeMap.TryGetValue(item.ProductId, out var recipe) && recipe.Ingredients.Any())
                    {
                        var servingSize = Math.Max(1, recipe.ServingSize);
                        foreach (var ri in recipe.Ingredients)
                        {
                            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, ri.IngredientId, asNoTracking: false);
                            if (ingredient == null) continue;

                            decimal multiplier = 1.0m;
                            try
                            {
                                multiplier = RecipeService.GetUnitConversionMultiplier(ri.Unit, ingredient.BaseUnit, ingredient.Name);
                            }
                            catch
                            {
                                multiplier = 1.0m;
                            }

                            var normalizedPerServing = (ri.Quantity * multiplier) / servingSize;
                            var totalItemNeeded = normalizedPerServing * item.Quantity;

                            if (requiredIngredients.TryGetValue(ingredient.IngredientId, out var existing))
                            {
                                requiredIngredients[ingredient.IngredientId] = (existing.Ingredient, existing.RequiredQty + totalItemNeeded);
                            }
                            else
                            {
                                requiredIngredients[ingredient.IngredientId] = (ingredient, totalItemNeeded);
                            }
                        }
                    }
                }

                // Verify stock availability across all required ingredients
                foreach (var (ingId, (ingredient, neededQty)) in requiredIngredients)
                {
                    if (ingredient.AvailableStock < neededQty)
                    {
                        // Rollback previously deducted products
                        foreach (var (pId, qty) in deductedProducts)
                        {
                            await _productRepo.RestoreStockAsync(canteenId, pId, qty);
                        }

                        throw new InvalidOrderException($"Insufficient raw stock for '{ingredient.Name}'. Available: {ingredient.AvailableStock:0.##} {ingredient.BaseUnit}, Required: {neededQty:0.##} {ingredient.BaseUnit}.");
                    }
                }

                // Atomically reserve the ingredients
                if (requiredIngredients.Any())
                {
                    stockReservation = new StockReservation
                    {
                        CanteenId = canteenId,
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    };

                    foreach (var (ingId, (ingredient, neededQty)) in requiredIngredients)
                    {
                        ingredient.ReservedStock += neededQty;
                        await _inventoryRepo.UpdateIngredientAsync(ingredient);

                        stockReservation.Items.Add(new StockReservationItem
                        {
                            IngredientId = ingId,
                            ReservedQuantity = neededQty
                        });
                    }
                }
            }

            // 4. Calculate prices strictly on the server (zero client trust)
            decimal subtotalAmount = 0;
            var orderItems = new List<OrderItem>();
            foreach (var item in consolidatedItems)
            {
                var product = products[item.ProductId];
                decimal unitPrice = product.Price;
                subtotalAmount += unitPrice * item.Quantity;

                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice
                });
            }

            // Coupon validation & discount calculation
            decimal discountAmount = 0.00m;
            int? appliedCouponId = null;
            string? appliedCouponCode = null;
            Coupon? appliedCoupon = null;

            if (!string.IsNullOrWhiteSpace(dto.CouponCode) && _couponService != null && _couponRepo != null)
            {
                var valResult = await _couponService.ValidateCouponAsync(canteenId, new ValidateCouponRequestDto
                {
                    Code = dto.CouponCode,
                    FoodSubtotal = subtotalAmount,
                    CustomerIdentifier = dto.CustomerPhone
                });

                if (!valResult.IsValid)
                {
                    foreach (var (pId, qty) in deductedProducts)
                    {
                        await _productRepo.RestoreStockAsync(canteenId, pId, qty);
                    }
                    throw new InvalidOrderException(valResult.Message);
                }

                discountAmount = valResult.DiscountAmount;
                appliedCouponCode = valResult.Code;
                appliedCoupon = await _couponRepo.GetByCodeAsync(canteenId, appliedCouponCode!);
                if (appliedCoupon != null)
                {
                    appliedCouponId = appliedCoupon.CouponId;
                    appliedCoupon.TimesUsed++;
                    await _couponRepo.UpdateCouponAsync(appliedCoupon);
                }
            }

            decimal netFoodSubtotal = Math.Max(0m, subtotalAmount - discountAmount);

            // Read canteen tax and platform fee policy if available
            decimal taxPercentage = 0;
            string feePolicy = "CustomerPays";
            if (_canteenRepo != null)
            {
                var canteen = await _canteenRepo.GetByIdAsync(canteenId);
                if (canteen != null)
                {
                    taxPercentage = canteen.TaxPercentage;
                    feePolicy = string.IsNullOrWhiteSpace(canteen.FeeAbsorptionPolicy) ? "CustomerPays" : canteen.FeeAbsorptionPolicy;
                }
            }

            decimal taxAmount = Math.Round(netFoodSubtotal * (taxPercentage / 100m), 2);
            decimal platformFeeAmount = 5.00m; // Standard CanteenHub platform fee
            decimal totalAmount;
            decimal canteenEarningsAmount;

            if (string.Equals(feePolicy, "CanteenAbsorbs", StringComparison.OrdinalIgnoreCase))
            {
                // Customer only pays net food + tax; canteen absorbs platform fee from their earnings
                totalAmount = netFoodSubtotal + taxAmount;
                canteenEarningsAmount = Math.Max(0m, (netFoodSubtotal + taxAmount) - platformFeeAmount);
            }
            else
            {
                // Customer pays net food + tax + platform fee; canteen gets net food + tax
                feePolicy = "CustomerPays";
                totalAmount = netFoodSubtotal + taxAmount + platformFeeAmount;
                canteenEarningsAmount = netFoodSubtotal + taxAmount;
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

            // 6. Persist order (and attached StockReservation)
            var order = new Order
            {
                CanteenId = canteenId,
                TableId = table.TableId,
                OrderNumber = orderNumber,
                Status = "Pending",
                PaymentStatus = "Pending",
                SubtotalAmount = subtotalAmount,
                DiscountAmount = discountAmount,
                CouponCode = appliedCouponCode,
                CouponId = appliedCouponId,
                TaxAmount = taxAmount,
                PlatformFeeAmount = platformFeeAmount,
                PlatformFeePolicy = feePolicy,
                TotalAmount = totalAmount,
                CanteenEarningsAmount = canteenEarningsAmount,
                TrackingToken = trackingToken,
                CreatedAt = DateTime.UtcNow,
                Items = orderItems,
                StockReservation = stockReservation
            };

            var savedOrder = await _orderRepo.AddAsync(order);

            if (appliedCouponId.HasValue && _couponRepo != null)
            {
                var redemption = new CouponRedemption
                {
                    CouponId = appliedCouponId.Value,
                    OrderId = savedOrder.OrderId,
                    CanteenId = canteenId,
                    CustomerIdentifier = dto.CustomerPhone ?? "Customer",
                    DiscountAmountApplied = discountAmount,
                    RedeemedAt = DateTime.UtcNow
                };
                await _couponRepo.AddRedemptionAsync(redemption);
            }

            return savedOrder;
        });

        // Populate navigation names for the DTO
        var orderDto = ToDto(created);
        foreach (var itemDto in orderDto.Items)
        {
            if (products.TryGetValue(itemDto.ProductId, out var prod))
            {
                itemDto.ProductName = prod.Name;
            }
        }
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

    public async Task<OrderDto?> CancelOrderByTrackingTokenAsync(string trackingToken, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return null;
        }

        var order = await _orderRepo.GetByTrackingTokenAsync(trackingToken, asNoTracking: false);
        if (order is null)
        {
            return null;
        }

        if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOrderException("Only pending orders can be cancelled. Your order is already being prepared or completed.");
        }

        decimal nonRefundableFee = 0m;
        decimal refundedAmount = 0m;
        string? cancellationNote = null;

        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            // Platform fee is non-refundable per CanteenHub policy
            nonRefundableFee = string.Equals(order.PlatformFeePolicy, "CustomerPays", StringComparison.OrdinalIgnoreCase)
                ? order.PlatformFeeAmount
                : 0m;
            refundedAmount = Math.Max(0m, order.TotalAmount - nonRefundableFee);

            order.PaymentStatus = "Refunded";
            cancellationNote = $"Order self-cancelled by customer. Food refund: ₹{refundedAmount:F2}. Platform fee (₹{nonRefundableFee:F2}) is non-refundable per platform policy.";

            if (_refundService != null)
            {
                await _refundService.ProcessCustomerCancellationRefundAsync(order.OrderId, cancellationNote ?? "Order self-cancelled by customer before preparation");
            }
            else if (_paymentRepo != null)
            {
                var payment = await _paymentRepo.GetByOrderIdAsync(order.CanteenId, order.OrderId);
                if (payment != null)
                {
                    payment.Status = "Refunded";
                    payment.FailureReason = cancellationNote;
                    await _paymentRepo.UpdateAsync(payment);
                }
            }
        }

                order.Status = "Cancelled";
        order.CancellationReason = cancellationNote ?? "Order self-cancelled by customer before preparation";
        var updated = await _orderRepo.UpdateAsync(order);

        // Record OrderCancellation audit entity
        if (_cancellationRepo != null)
        {
            var cancellation = new OrderCancellation
            {
                OrderId = order.OrderId,
                CanteenId = order.CanteenId,
                InitiatedByRole = "Customer",
                ActorOwnerId = null,
                Reason = cancellationNote ?? "Order self-cancelled by customer before preparation",
                PreviousOrderStatus = "Pending",
                FoodRefundAmount = refundedAmount,
                PlatformFeeRefundAmount = 0m,
                TotalRefundAmount = refundedAmount,
                StockRestored = true,
                PlatformPolicyVersion = order.AppliedPolicyVersion,
                CancelledAt = DateTime.UtcNow
            };
            await _cancellationRepo.AddAsync(cancellation);
        }

        // Restore deducted finished product stock
        if (order.Items != null)
        {
            foreach (var item in order.Items)
            {
                await _productRepo.RestoreStockAsync(order.CanteenId, item.ProductId, item.Quantity);
            }
        }

        // Release any raw ingredient stock reservation (Pre-Prep Cancellation)
        await ReleaseStockReservationAsync(order.CanteenId, order.OrderId, order.OrderNumber);

        await _notificationService.NotifyOrderStatusUpdatedAsync(order.OrderId, order.OrderNumber, "Cancelled", DateTime.UtcNow);

        var dto = ToDto(updated);
        dto.RefundedAmount = refundedAmount > 0 ? refundedAmount : null;
        dto.CancellationNote = cancellationNote;
        return dto;
    }

    public async Task<IEnumerable<OrderDto>> GetAllByCanteenAsync(int canteenId)
    {
        var entities = await _orderRepo.GetAllByCanteenAsync(canteenId);
        return entities.Select(ToDto);
    }

    public async Task<OrderDto?> UpdateStatusAsync(int canteenId, int orderId, string newStatus)
    {
        if (string.Equals(newStatus, "Preparing", StringComparison.OrdinalIgnoreCase))
        {
            newStatus = "InPreparation";
        }

        if (string.IsNullOrWhiteSpace(newStatus) || !ValidStatuses.Contains(newStatus))
        {
            throw new InvalidOrderException($"Status '{newStatus}' is invalid. Allowed: {string.Join(", ", ValidStatuses)}.");
        }

        var order = await _orderRepo.GetByIdAsync(canteenId, orderId, asNoTracking: false);
        if (order is null)
        {
            return null;
        }

        var previousStatus = order.Status;
        ValidateStatusTransition(previousStatus, newStatus);

        order.Status = newStatus;
        var updated = await _orderRepo.UpdateAsync(order);
        var dto = ToDto(updated);

        // Lifecycle 1: Kitchen begins cooking -> Consume reserved raw ingredients
        if (string.Equals(newStatus, "InPreparation", StringComparison.OrdinalIgnoreCase))
        {
            await ConsumeReservedStockAsync(canteenId, orderId, order.OrderNumber);
        }
        // Lifecycle 2: Order cancelled
        else if (string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            bool wasPostPrep = string.Equals(previousStatus, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(previousStatus, "Ready", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(previousStatus, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(previousStatus, "Ready", StringComparison.OrdinalIgnoreCase))
            {
                // Post-Prep Cancellation: Ingredients were already consumed in the kitchen -> log wastage
                await RecordPreparationWastageAsync(canteenId, orderId, order.OrderNumber);
            }
            else
            {
                // Pre-Prep Cancellation: Release ingredient reservation hold and restore product stock
                await ReleaseStockReservationAsync(canteenId, orderId, order.OrderNumber);
                                if (order.Items != null)
                {
                    foreach (var item in order.Items)
                    {
                        await _productRepo.RestoreStockAsync(canteenId, item.ProductId, item.Quantity);
                    }
                }
            }

            decimal refundedAmount = 0m;
            if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                refundedAmount = order.TotalAmount;
                order.PaymentStatus = "Refunded";
                if (_refundService != null)
                {
                    await _refundService.ProcessCanteenCancellationRefundAsync(canteenId, orderId, "Cancelled by canteen staff", 0);
                }
                else if (_paymentRepo != null)
                {
                    var payment = await _paymentRepo.GetByOrderIdAsync(canteenId, orderId);
                    if (payment != null)
                    {
                        payment.Status = "Refunded";
                        payment.FailureReason = "Cancelled by canteen staff";
                        await _paymentRepo.UpdateAsync(payment);
                    }
                }
            }

            if (_cancellationRepo != null)
            {
                var cancellation = new OrderCancellation
                {
                    OrderId = order.OrderId,
                    CanteenId = order.CanteenId,
                    InitiatedByRole = "CanteenOwner",
                    ActorOwnerId = null,
                    Reason = "Cancelled by canteen staff",
                    PreviousOrderStatus = previousStatus,
                    FoodRefundAmount = Math.Max(0m, order.TotalAmount - order.PlatformFeeAmount),
                    PlatformFeeRefundAmount = order.PlatformFeeAmount,
                    TotalRefundAmount = refundedAmount,
                    StockRestored = !wasPostPrep,
                    PlatformPolicyVersion = order.AppliedPolicyVersion,
                    CancelledAt = DateTime.UtcNow
                };
                await _cancellationRepo.AddAsync(cancellation);
            }
        }

        if (string.Equals(newStatus, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(newStatus, "Ready", StringComparison.OrdinalIgnoreCase) || string.Equals(newStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.NotifyOrderStatusUpdatedAsync(order.OrderId, order.OrderNumber, newStatus, DateTime.UtcNow, order.CanteenId);
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
            "Pending" => nextStatus is "Confirmed" or "InPreparation" or "Cancelled",
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

    private async Task ConsumeReservedStockAsync(int canteenId, int orderId, string orderNumber)
    {
        if (_inventoryRepo == null) return;

        var reservation = await _inventoryRepo.GetReservationByOrderIdAsync(canteenId, orderId, asNoTracking: false);
        if (reservation == null || !string.Equals(reservation.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var item in reservation.Items)
        {
            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, item.IngredientId, asNoTracking: false);
            if (ingredient == null) continue;

            var prevStock = ingredient.CurrentStock;
            ingredient.CurrentStock = Math.Max(0m, ingredient.CurrentStock - item.ReservedQuantity);
            ingredient.ReservedStock = Math.Max(0m, ingredient.ReservedStock - item.ReservedQuantity);
            await _inventoryRepo.UpdateIngredientAsync(ingredient);

            await _inventoryRepo.AddStockTransactionAsync(new StockTransaction
            {
                CanteenId = canteenId,
                IngredientId = item.IngredientId,
                TransactionType = "Consumption",
                Quantity = item.ReservedQuantity,
                PreviousStock = prevStock,
                NewStock = ingredient.CurrentStock,
                OrderId = orderId,
                Reason = $"Kitchen preparation started for Order #{orderNumber}",
                CreatedAt = DateTime.UtcNow
            });
        }

        reservation.Status = "Consumed";
        reservation.ConsumedAt = DateTime.UtcNow;
        await _inventoryRepo.UpdateReservationAsync(reservation);
    }

    private async Task ReleaseStockReservationAsync(int canteenId, int orderId, string orderNumber)
    {
        if (_inventoryRepo == null) return;

        var reservation = await _inventoryRepo.GetReservationByOrderIdAsync(canteenId, orderId, asNoTracking: false);
        if (reservation == null || !string.Equals(reservation.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var item in reservation.Items)
        {
            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, item.IngredientId, asNoTracking: false);
            if (ingredient == null) continue;

            ingredient.ReservedStock = Math.Max(0m, ingredient.ReservedStock - item.ReservedQuantity);
            await _inventoryRepo.UpdateIngredientAsync(ingredient);

            await _inventoryRepo.AddStockTransactionAsync(new StockTransaction
            {
                CanteenId = canteenId,
                IngredientId = item.IngredientId,
                TransactionType = "ReservationRelease",
                Quantity = item.ReservedQuantity,
                PreviousStock = ingredient.CurrentStock,
                NewStock = ingredient.CurrentStock,
                OrderId = orderId,
                Reason = $"Reservation released: Order #{orderNumber} cancelled before preparation",
                CreatedAt = DateTime.UtcNow
            });
        }

        reservation.Status = "Released";
        reservation.ReleasedAt = DateTime.UtcNow;
        await _inventoryRepo.UpdateReservationAsync(reservation);
    }

    private async Task RecordPreparationWastageAsync(int canteenId, int orderId, string orderNumber, int? ownerId = null)
    {
        if (_inventoryRepo == null) return;

        var reservation = await _inventoryRepo.GetReservationByOrderIdAsync(canteenId, orderId, asNoTracking: false);
        if (reservation == null || !string.Equals(reservation.Status, "Consumed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var item in reservation.Items)
        {
            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, item.IngredientId, asNoTracking: true);
            var costPerUnit = ingredient?.CostPerBaseUnit ?? 0m;
            var estimatedCost = Math.Round(item.ReservedQuantity * costPerUnit, 2);

            await _inventoryRepo.AddWastageAsync(new IngredientWastage
            {
                CanteenId = canteenId,
                IngredientId = item.IngredientId,
                Quantity = item.ReservedQuantity,
                Reason = "PrepWaste",
                Notes = $"Order #{orderNumber} cancelled after kitchen preparation began",
                EstimatedCost = estimatedCost,
                RecordedByOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            });
        }

        reservation.Status = "Wasted";
        await _inventoryRepo.UpdateReservationAsync(reservation);
    }

    private static OrderDto ToDto(Order o) => new()
    {
        OrderId = o.OrderId,
        CanteenId = o.CanteenId,
        TableId = o.TableId,
        OrderNumber = o.OrderNumber,
        Status = o.Status,
        PaymentStatus = o.PaymentStatus,
        SubtotalAmount = o.SubtotalAmount,
        DiscountAmount = o.DiscountAmount,
        CouponCode = o.CouponCode,
        CouponId = o.CouponId,
        TaxAmount = o.TaxAmount,
        PlatformFeeAmount = o.PlatformFeeAmount,
        PlatformFeePolicy = o.PlatformFeePolicy,
        TotalAmount = o.TotalAmount,
        CanteenEarningsAmount = o.CanteenEarningsAmount,
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

    public async Task<OrderDto?> CancelOrderFromCanteenAsync(int canteenId, int orderId, string reason, int actorOwnerId)
    {
        var order = await _orderRepo.GetByIdAsync(canteenId, orderId, asNoTracking: false);
        if (order is null) return null;

        if (string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOrderException($"Cannot cancel an order that is already {order.Status}.");
        }

        var previousStatus = order.Status;
        bool wasPostPrep = string.Equals(previousStatus, "InPreparation", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(previousStatus, "Ready", StringComparison.OrdinalIgnoreCase);

        if (wasPostPrep)
        {
            await RecordPreparationWastageAsync(canteenId, orderId, order.OrderNumber);
        }
        else
        {
            await ReleaseStockReservationAsync(canteenId, orderId, order.OrderNumber);
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    await _productRepo.RestoreStockAsync(canteenId, item.ProductId, item.Quantity);
                }
            }
        }

        decimal refundedAmount = 0m;
        string cancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by canteen staff" : reason.Trim();

        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            refundedAmount = order.TotalAmount;
            order.PaymentStatus = "Refunded";
            if (_refundService != null)
            {
                await _refundService.ProcessCanteenCancellationRefundAsync(canteenId, orderId, cancellationReason, actorOwnerId);
            }
            else if (_paymentRepo != null)
            {
                var payment = await _paymentRepo.GetByOrderIdAsync(canteenId, orderId);
                if (payment != null)
                {
                    payment.Status = "Refunded";
                    payment.FailureReason = cancellationReason;
                    await _paymentRepo.UpdateAsync(payment);
                }
            }
        }

        order.Status = "Cancelled";
        order.CancellationReason = cancellationReason;
        var updated = await _orderRepo.UpdateAsync(order);

        if (_cancellationRepo != null)
        {
            var cancellation = new OrderCancellation
            {
                OrderId = order.OrderId,
                CanteenId = order.CanteenId,
                InitiatedByRole = "CanteenOwner",
                ActorOwnerId = actorOwnerId > 0 ? actorOwnerId : null,
                Reason = cancellationReason,
                PreviousOrderStatus = previousStatus,
                FoodRefundAmount = Math.Max(0m, order.TotalAmount - order.PlatformFeeAmount),
                PlatformFeeRefundAmount = order.PlatformFeeAmount,
                TotalRefundAmount = refundedAmount,
                StockRestored = !wasPostPrep,
                PlatformPolicyVersion = order.AppliedPolicyVersion,
                CancelledAt = DateTime.UtcNow
            };
            await _cancellationRepo.AddAsync(cancellation);
        }

        await _notificationService.NotifyOrderStatusUpdatedAsync(order.OrderId, order.OrderNumber, "Cancelled", DateTime.UtcNow);

        var dto = ToDto(updated);
        dto.RefundedAmount = refundedAmount > 0 ? refundedAmount : null;
        dto.CancellationNote = cancellationReason;
        return dto;
    }

    public async Task<OrderCancellationDto?> GetCancellationByOrderIdAsync(int canteenId, int orderId)
    {
        if (_cancellationRepo == null) return null;
        var cancellation = await _cancellationRepo.GetByOrderIdAsync(orderId);
        if (cancellation == null || cancellation.CanteenId != canteenId) return null;

        return MapCancellationToDto(cancellation);
    }

    public async Task<IEnumerable<OrderCancellationDto>> GetCancellationsByCanteenAsync(int canteenId)
    {
        if (_cancellationRepo == null) return Enumerable.Empty<OrderCancellationDto>();
        var list = await _cancellationRepo.GetAllByCanteenAsync(canteenId);
        return list.Select(MapCancellationToDto);
    }

    private static OrderCancellationDto MapCancellationToDto(OrderCancellation oc) => new()
    {
        CancellationId = oc.CancellationId,
        OrderId = oc.OrderId,
        CanteenId = oc.CanteenId,
        OrderNumber = oc.Order?.OrderNumber ?? string.Empty,
        InitiatedByRole = oc.InitiatedByRole,
        ActorOwnerId = oc.ActorOwnerId,
        Reason = oc.Reason,
        PreviousOrderStatus = oc.PreviousOrderStatus,
        FoodRefundAmount = oc.FoodRefundAmount,
        PlatformFeeRefundAmount = oc.PlatformFeeRefundAmount,
        TotalRefundAmount = oc.TotalRefundAmount,
        StockRestored = oc.StockRestored,
        PlatformPolicyVersion = oc.PlatformPolicyVersion,
        CancelledAt = oc.CancelledAt
    };

}
