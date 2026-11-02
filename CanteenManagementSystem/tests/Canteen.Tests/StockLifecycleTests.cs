using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Canteen.Tests;

public class StockLifecycleTests
{
    private class DummyNotificationService : IOrderNotificationService
    {
        public Task NotifyNewOrderPlacedAsync(OrderDto order) => Task.CompletedTask;
        public Task NotifyPaymentConfirmedAsync(int canteenId, int orderId, string orderNumber, decimal amount, DateTime paidAt) => Task.CompletedTask;
        public Task NotifyOrderStatusUpdatedAsync(int orderId, string orderNumber, string newStatus, DateTime updatedAt) => Task.CompletedTask;
    }

    private CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "StockLifecycleTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    private async Task<(
        Canteen.Model.Entities.Canteen canteen,
        CanteenTable table,
        Product product,
        InventoryIngredient paneer,
        InventoryIngredient tomato,
        Recipe recipe
    )> SeedTestDataAsync(CanteenDbContext context)
    {
        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = 1,
            Name = "Katti Batti Cafe",
            Slug = "katti-batti-cafe",
            IsActive = true
        };
        context.Canteens.Add(canteen);

        var table = new CanteenTable
        {
            TableId = 1,
            CanteenId = 1,
            TableNumber = 1,
            Capacity = 4,
            IsAvailable = true,
            QRToken = "valid-qr-token-123"
        };
        context.Tables.Add(table);

        var category = new Category
        {
            CategoryId = 1,
            CanteenId = 1,
            Name = "Curries"
        };
        context.Categories.Add(category);

        var product = new Product
        {
            ProductId = 101,
            CanteenId = 1,
            CategoryId = 1,
            Name = "Paneer Butter Masala",
            Description = "Delicious cottage cheese curry",
            Price = 120.00m,
            StockQuantity = 50,
            IsAvailable = true,
            FoodType = "Veg",
            Category = category
        };
        context.Products.Add(product);

        var paneer = new InventoryIngredient
        {
            IngredientId = 201,
            CanteenId = 1,
            Name = "Fresh Paneer",
            Category = "Dairy",
            BaseUnit = "g",
            CurrentStock = 1000m,
            ReservedStock = 0m,
            MinimumStockLevel = 200m,
            CostPerBaseUnit = 0.40m, // ₹0.40/g
            IsActive = true
        };

        var tomato = new InventoryIngredient
        {
            IngredientId = 202,
            CanteenId = 1,
            Name = "Tomato Puree",
            Category = "Produce",
            BaseUnit = "g",
            CurrentStock = 2000m,
            ReservedStock = 0m,
            MinimumStockLevel = 300m,
            CostPerBaseUnit = 0.10m, // ₹0.10/g
            IsActive = true
        };
        context.InventoryIngredients.AddRange(paneer, tomato);

        var recipe = new Recipe
        {
            RecipeId = 301,
            CanteenId = 1,
            ProductId = product.ProductId,
            ServingSize = 1,
            IsActive = true,
            Ingredients = new List<RecipeIngredient>
            {
                new() { IngredientId = paneer.IngredientId, Quantity = 100m, Unit = "g", Ingredient = paneer },
                new() { IngredientId = tomato.IngredientId, Quantity = 150m, Unit = "g", Ingredient = tomato }
            }
        };
        context.Recipes.Add(recipe);

        await context.SaveChangesAsync();

        return (canteen, table, product, paneer, tomato, recipe);
    }

    [Fact]
    public async Task CreateOrder_WithRecipeItem_ReservesRawIngredientsAndLeavesPhysicalStockIntact()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, table, product, paneer, tomato, _) = await SeedTestDataAsync(context);

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var notificationService = new DummyNotificationService();

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            null,
            notificationService,
            recipeRepo,
            inventoryRepo);

        var createDto = new CreateOrderDto
        {
            TableId = table.TableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.ProductId, Quantity = 2 } // 2 portions
            }
        };

        // Act
        var order = await orderService.CreateAsync(createDto, table.QRToken);

        // Assert
        Assert.NotNull(order);
        Assert.Equal("Pending", order.Status);

        // Check ingredients: 2 portions = 200g Paneer, 300g Tomato
        var reloadedPaneer = await inventoryRepo.GetIngredientByIdAsync(1, paneer.IngredientId);
        Assert.NotNull(reloadedPaneer);
        Assert.Equal(1000m, reloadedPaneer.CurrentStock); // Physical stock intact
        Assert.Equal(200m, reloadedPaneer.ReservedStock);  // 2 * 100g reserved
        Assert.Equal(800m, reloadedPaneer.AvailableStock); // 1000 - 200

        var reloadedTomato = await inventoryRepo.GetIngredientByIdAsync(1, tomato.IngredientId);
        Assert.NotNull(reloadedTomato);
        Assert.Equal(2000m, reloadedTomato.CurrentStock); // Physical stock intact
        Assert.Equal(300m, reloadedTomato.ReservedStock);  // 2 * 150g reserved
        Assert.Equal(1700m, reloadedTomato.AvailableStock);

        // Check reservation record
        var reservation = await inventoryRepo.GetReservationByOrderIdAsync(1, order.OrderId);
        Assert.NotNull(reservation);
        Assert.Equal("Active", reservation.Status);
        Assert.Equal(2, reservation.Items.Count);

        var paneerItem = reservation.Items.First(i => i.IngredientId == paneer.IngredientId);
        Assert.Equal(200m, paneerItem.ReservedQuantity);

        var tomatoItem = reservation.Items.First(i => i.IngredientId == tomato.IngredientId);
        Assert.Equal(300m, tomatoItem.ReservedQuantity);
    }

    [Fact]
    public async Task CreateOrder_InsufficientRawStock_RejectsOrderAndRollsBackDeductions()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, table, product, paneer, _, _) = await SeedTestDataAsync(context);

        // Reduce Paneer available stock to only 150g (order of 2 portions needs 200g)
        paneer.CurrentStock = 150m;
        await context.SaveChangesAsync();

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var notificationService = new DummyNotificationService();

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            null,
            notificationService,
            recipeRepo,
            inventoryRepo);

        var createDto = new CreateOrderDto
        {
            TableId = table.TableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.ProductId, Quantity = 2 }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOrderException>(() => orderService.CreateAsync(createDto, table.QRToken));
        Assert.Contains("Insufficient raw stock for 'Fresh Paneer'", ex.Message);

        // Product stock should not be deducted (should remain 50)
        var reloadedProduct = await productRepo.GetByIdAsync(1, product.ProductId);
        Assert.Equal(50, reloadedProduct!.StockQuantity);

        // No reservations persisted
        var reservationsCount = await context.StockReservations.CountAsync();
        Assert.Equal(0, reservationsCount);
    }

    [Fact]
    public async Task UpdateStatus_ToInPreparation_ConsumesReservedStockAndCreatesAuditTransaction()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, table, product, paneer, tomato, _) = await SeedTestDataAsync(context);

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var notificationService = new DummyNotificationService();

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            null,
            notificationService,
            recipeRepo,
            inventoryRepo);

        var createDto = new CreateOrderDto
        {
            TableId = table.TableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.ProductId, Quantity = 2 }
            }
        };

        var order = await orderService.CreateAsync(createDto, table.QRToken);

        // Act: Kitchen advances status to InPreparation
        var updated = await orderService.UpdateStatusAsync(1, order.OrderId, "InPreparation");

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("InPreparation", updated.Status);

        // Stock consumption check:
        // Paneer: 1000 - 200 = 800g current, 0g reserved
        var reloadedPaneer = await inventoryRepo.GetIngredientByIdAsync(1, paneer.IngredientId);
        Assert.Equal(800m, reloadedPaneer!.CurrentStock);
        Assert.Equal(0m, reloadedPaneer.ReservedStock);

        // Tomato: 2000 - 300 = 1700g current, 0g reserved
        var reloadedTomato = await inventoryRepo.GetIngredientByIdAsync(1, tomato.IngredientId);
        Assert.Equal(1700m, reloadedTomato!.CurrentStock);
        Assert.Equal(0m, reloadedTomato.ReservedStock);

        // Reservation status should be Consumed
        var reservation = await inventoryRepo.GetReservationByOrderIdAsync(1, order.OrderId);
        Assert.Equal("Consumed", reservation!.Status);
        Assert.NotNull(reservation.ConsumedAt);

        // Stock transactions audit
        var transactions = (await inventoryRepo.GetTransactionsAsync(1)).ToList();
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal("Consumption", t.TransactionType));
        Assert.All(transactions, t => Assert.Equal(order.OrderId, t.OrderId));
    }

    [Fact]
    public async Task CancelOrder_PrePreparation_ReleasesReservationHoldWithoutDeductingStock()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, table, product, paneer, tomato, _) = await SeedTestDataAsync(context);

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var notificationService = new DummyNotificationService();

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            null,
            notificationService,
            recipeRepo,
            inventoryRepo);

        var createDto = new CreateOrderDto
        {
            TableId = table.TableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.ProductId, Quantity = 2 }
            }
        };

        var order = await orderService.CreateAsync(createDto, table.QRToken);

        // Act: Customer self-cancels while still Pending
        var cancelled = await orderService.CancelOrderByTrackingTokenAsync(order.TrackingToken);

        // Assert
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);

        // Stock checks:
        // Paneer: ReservedStock back to 0, CurrentStock untouched at 1000g
        var reloadedPaneer = await inventoryRepo.GetIngredientByIdAsync(1, paneer.IngredientId);
        Assert.Equal(1000m, reloadedPaneer!.CurrentStock);
        Assert.Equal(0m, reloadedPaneer.ReservedStock);

        // Tomato: ReservedStock back to 0, CurrentStock untouched at 2000g
        var reloadedTomato = await inventoryRepo.GetIngredientByIdAsync(1, tomato.IngredientId);
        Assert.Equal(2000m, reloadedTomato!.CurrentStock);
        Assert.Equal(0m, reloadedTomato.ReservedStock);

        // Product stock restored back to 50
        var reloadedProduct = await productRepo.GetByIdAsync(1, product.ProductId);
        Assert.Equal(50, reloadedProduct!.StockQuantity);

        // Reservation status should be Released
        var reservation = await inventoryRepo.GetReservationByOrderIdAsync(1, order.OrderId);
        Assert.Equal("Released", reservation!.Status);
        Assert.NotNull(reservation.ReleasedAt);

        // Audit transactions: type ReservationRelease
        var transactions = (await inventoryRepo.GetTransactionsAsync(1)).ToList();
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal("ReservationRelease", t.TransactionType));
    }

    [Fact]
    public async Task CancelOrder_PostPreparation_LogsKitchenIngredientWastage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, table, product, paneer, tomato, _) = await SeedTestDataAsync(context);

        var orderRepo = new OrderRepository(context);
        var productRepo = new ProductRepository(context);
        var tableRepo = new CanteenTableRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var notificationService = new DummyNotificationService();

        var orderService = new OrderService(
            orderRepo,
            productRepo,
            tableRepo,
            canteenRepo,
            null,
            notificationService,
            recipeRepo,
            inventoryRepo);

        var createDto = new CreateOrderDto
        {
            TableId = table.TableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.ProductId, Quantity = 2 }
            }
        };

        var order = await orderService.CreateAsync(createDto, table.QRToken);

        // Transition to InPreparation (Cooking starts, stock consumed)
        await orderService.UpdateStatusAsync(1, order.OrderId, "InPreparation");

        // Act: Order cancelled after prep begins
        var cancelled = await orderService.UpdateStatusAsync(1, order.OrderId, "Cancelled");

        // Assert
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);

        // Reservation marked as Wasted
        var reservation = await inventoryRepo.GetReservationByOrderIdAsync(1, order.OrderId);
        Assert.Equal("Wasted", reservation!.Status);

        // Automatic wastage write-offs
        var wastages = (await inventoryRepo.GetWastagesAsync(1)).ToList();
        Assert.Equal(2, wastages.Count);

        var paneerWaste = wastages.First(w => w.IngredientId == paneer.IngredientId);
        Assert.Equal(200m, paneerWaste.Quantity);
        Assert.Equal("PrepWaste", paneerWaste.Reason);
        Assert.Equal(80.00m, paneerWaste.EstimatedCost); // 200g * ₹0.40 = ₹80.00

        var tomatoWaste = wastages.First(w => w.IngredientId == tomato.IngredientId);
        Assert.Equal(300m, tomatoWaste.Quantity);
        Assert.Equal("PrepWaste", tomatoWaste.Reason);
        Assert.Equal(30.00m, tomatoWaste.EstimatedCost); // 300g * ₹0.10 = ₹30.00
    }
}
