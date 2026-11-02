using System;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Canteen.Tests;

public class InventoryTests
{
    private CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "InventoryTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    [Fact]
    public async Task CreateIngredient_WithInitialStock_CreatesIngredientAndOpeningTransaction()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var dto = new CreateIngredientDto
        {
            Name = "Basmati Rice",
            Category = "Grains",
            BaseUnit = "g",
            InitialStock = 1000m,
            MinimumStockLevel = 200m,
            ReorderLevel = 500m,
            CostPerBaseUnit = 0.08m
        };

        // Act
        var created = await service.CreateIngredientAsync(canteenId: 1, dto, ownerId: 10);

        // Assert
        Assert.NotNull(created);
        Assert.True(created.IngredientId > 0);
        Assert.Equal("Basmati Rice", created.Name);
        Assert.Equal(1000m, created.CurrentStock);
        Assert.Equal(0m, created.ReservedStock);
        Assert.Equal(1000m, created.AvailableStock);
        Assert.False(created.IsLowStock);

        var transactions = (await repo.GetTransactionsAsync(canteenId: 1, created.IngredientId)).ToList();
        Assert.Single(transactions);
        Assert.Equal("OpeningStock", transactions[0].TransactionType);
        Assert.Equal(1000m, transactions[0].Quantity);
        Assert.Equal(0m, transactions[0].PreviousStock);
        Assert.Equal(1000m, transactions[0].NewStock);
    }

    [Fact]
    public async Task CreateIngredient_DuplicateNameInSameCanteen_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var dto1 = new CreateIngredientDto { Name = "Cooking Oil", Category = "Oils", BaseUnit = "ml", InitialStock = 500 };
        await service.CreateIngredientAsync(canteenId: 1, dto1);

        var dto2 = new CreateIngredientDto { Name = "  cooking oil  ", Category = "Oils", BaseUnit = "ml", InitialStock = 100 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateIngredientAsync(canteenId: 1, dto2));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateIngredient_SameNameInDifferentCanteen_Succeeds()
    {
        // Arrange: Canteen 1 and Canteen 2 both have "Cooking Oil"
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var c1 = await service.CreateIngredientAsync(canteenId: 1, new CreateIngredientDto { Name = "Cooking Oil", BaseUnit = "ml", InitialStock = 500 });
        var c2 = await service.CreateIngredientAsync(canteenId: 2, new CreateIngredientDto { Name = "Cooking Oil", BaseUnit = "ml", InitialStock = 800 });

        // Assert
        Assert.NotEqual(c1.IngredientId, c2.IngredientId);
        Assert.Equal(500m, c1.CurrentStock);
        Assert.Equal(800m, c2.CurrentStock);
    }

    [Fact]
    public async Task AdjustStock_Purchase_IncreasesCurrentStockAndLogsTransaction()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var ingredient = await service.CreateIngredientAsync(canteenId: 1, new CreateIngredientDto
        {
            Name = "Paneer",
            Category = "Dairy",
            BaseUnit = "g",
            InitialStock = 200m
        });

        // Act: Purchase 500g
        var adjusted = await service.AdjustStockAsync(canteenId: 1, ingredient.IngredientId, new AdjustStockDto
        {
            OperationType = "Purchase",
            Quantity = 500m,
            ReferenceNumber = "INV-9988",
            Reason = "Fresh dairy delivery"
        }, ownerId: 10);

        // Assert
        Assert.NotNull(adjusted);
        Assert.Equal(700m, adjusted!.CurrentStock);

        var txs = (await repo.GetTransactionsAsync(canteenId: 1, ingredient.IngredientId)).ToList();
        Assert.Equal(2, txs.Count); // OpeningStock + Purchase
        var latestTx = txs.First(t => t.TransactionType == "Purchase");
        Assert.Equal(500m, latestTx.Quantity);
        Assert.Equal(200m, latestTx.PreviousStock);
        Assert.Equal(700m, latestTx.NewStock);
        Assert.Equal("INV-9988", latestTx.ReferenceNumber);
    }

    [Fact]
    public async Task AdjustStock_ReductionBelowZero_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var ingredient = await service.CreateIngredientAsync(canteenId: 1, new CreateIngredientDto
        {
            Name = "Eggs",
            Category = "Poultry",
            BaseUnit = "pcs",
            InitialStock = 10m
        });

        // Act & Assert: Attempt reduction by 15 (resulting in -5)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustStockAsync(canteenId: 1, ingredient.IngredientId, new AdjustStockDto
            {
                OperationType = "Adjustment",
                Quantity = -15m,
                IsAbsoluteCount = false,
                Reason = "Stock correction"
            }));

        Assert.Contains("Cannot reduce stock below zero", ex.Message);
    }

    [Fact]
    public async Task AdjustStock_ReductionBelowReservedStock_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var entity = new InventoryIngredient
        {
            CanteenId = 1,
            Name = "Chicken",
            BaseUnit = "g",
            CurrentStock = 500m,
            ReservedStock = 200m, // 200g reserved for pending orders
            IsActive = true
        };
        context.InventoryIngredients.Add(entity);
        await context.SaveChangesAsync();

        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        // Act & Assert: Attempt to set absolute stock to 150g (which is < 200g reserved)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustStockAsync(canteenId: 1, entity.IngredientId, new AdjustStockDto
            {
                OperationType = "Adjustment",
                Quantity = 150m,
                IsAbsoluteCount = true,
                Reason = "Inventory recount"
            }));

        Assert.Contains("below currently reserved stock", ex.Message);
    }

    [Fact]
    public async Task RecordWastage_ValidQuantity_ReducesStockAndLogsWastage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var ingredient = await service.CreateIngredientAsync(canteenId: 1, new CreateIngredientDto
        {
            Name = "Tomatoes",
            Category = "Produce",
            BaseUnit = "kg",
            InitialStock = 20m,
            CostPerBaseUnit = 40m // ₹40 per kg
        });

        // Act: 2kg spoiled
        var wastage = await service.RecordWastageAsync(canteenId: 1, ingredient.IngredientId, new RecordWastageDto
        {
            Quantity = 2m,
            Reason = "Spoiled",
            Notes = "Overripe due to heat"
        }, ownerId: 10);

        // Assert
        Assert.NotNull(wastage);
        Assert.Equal(2m, wastage!.Quantity);
        Assert.Equal("Spoiled", wastage.Reason);
        Assert.Equal(80m, wastage.EstimatedCost); // 2kg * ₹40 = ₹80

        var updated = await service.GetIngredientByIdAsync(canteenId: 1, ingredient.IngredientId);
        Assert.Equal(18m, updated!.CurrentStock);

        var txs = (await repo.GetTransactionsAsync(canteenId: 1, ingredient.IngredientId)).ToList();
        var wasteTx = txs.FirstOrDefault(t => t.TransactionType == "Wastage");
        Assert.NotNull(wasteTx);
        Assert.Equal(-2m, wasteTx!.Quantity);
        Assert.Equal(20m, wasteTx.PreviousStock);
        Assert.Equal(18m, wasteTx.NewStock);
    }

    [Fact]
    public async Task LowStockDetection_WhenAvailableStockDropsBelowThreshold_ReportsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var normal = await service.CreateIngredientAsync(canteenId: 1, new CreateIngredientDto
        {
            Name = "Flour",
            BaseUnit = "kg",
            InitialStock = 50m,
            MinimumStockLevel = 10m
        });

        var low = await service.CreateIngredientAsync(canteenId: 1, new CreateIngredientDto
        {
            Name = "Sugar",
            BaseUnit = "kg",
            InitialStock = 5m,
            MinimumStockLevel = 10m // Initial stock (5) <= Min level (10)
        });

        // Act
        var dashboard = await service.GetDashboardSummaryAsync(canteenId: 1);

        // Assert
        Assert.Equal(2, dashboard.TotalIngredients);
        Assert.Equal(1, dashboard.LowStockCount);
        Assert.Single(dashboard.LowStockIngredients);
        Assert.Equal("Sugar", dashboard.LowStockIngredients[0].Name);
    }

    [Fact]
    public async Task CrossTenantSecurity_CannotAccessOrModifyOtherCanteenIngredient()
    {
        // Arrange: Ingredient belongs to Canteen 1
        using var context = CreateInMemoryDbContext();
        var repo = new InventoryRepository(context);
        var service = new InventoryService(repo);

        var c1Ing = await service.CreateIngredientAsync(canteenId: 1, new CreateIngredientDto
        {
            Name = "Secret Spice",
            BaseUnit = "g",
            InitialStock = 100m
        });

        // Act: Canteen 2 tries to read, update, adjust, or waste Canteen 1's ingredient
        var readResult = await service.GetIngredientByIdAsync(canteenId: 2, c1Ing.IngredientId);
        var updateResult = await service.UpdateIngredientAsync(canteenId: 2, c1Ing.IngredientId, new UpdateIngredientDto { Name = "Hacked Spice", BaseUnit = "g" });
        var adjustResult = await service.AdjustStockAsync(canteenId: 2, c1Ing.IngredientId, new AdjustStockDto { OperationType = "Purchase", Quantity = 50, Reason = "Hacked" });
        var wasteResult = await service.RecordWastageAsync(canteenId: 2, c1Ing.IngredientId, new RecordWastageDto { Quantity = 10, Reason = "Spoiled" });

        // Assert
        Assert.Null(readResult);
        Assert.Null(updateResult);
        Assert.Null(adjustResult);
        Assert.Null(wasteResult);
    }
}

