using System;
using System.Collections.Generic;
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

public class RecipeTests
{
    private CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "RecipeTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    private async Task<(Canteen.Model.Entities.Canteen canteen, Product product, List<InventoryIngredient> ingredients)> SeedTestDataAsync(CanteenDbContext context, int canteenId = 1)
    {
        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = canteenId,
            Name = $"Test Canteen {canteenId}",
            Slug = $"test-canteen-{canteenId}",
            IsActive = true
        };
        context.Canteens.Add(canteen);

        var category = new Category
        {
            CategoryId = canteenId * 10,
            CanteenId = canteenId,
            Name = "Main Course"
        };
        context.Categories.Add(category);

        var product = new Product
        {
            ProductId = canteenId * 100 + 1,
            CanteenId = canteenId,
            CategoryId = category.CategoryId,
            Name = "Paneer Butter Masala",
            Description = "Delicious cottage cheese curry",
            Price = 180.00m,
            StockQuantity = 50,
            IsAvailable = true,
            FoodType = "Veg",
            Category = category
        };
        context.Products.Add(product);

        var rice = new InventoryIngredient
        {
            IngredientId = canteenId * 1000 + 1,
            CanteenId = canteenId,
            Name = "Basmati Rice",
            Category = "Grains",
            BaseUnit = "g",
            CurrentStock = 5000m,
            ReservedStock = 0m,
            MinimumStockLevel = 500m,
            CostPerBaseUnit = 0.08m, // ₹0.08 per gram (₹80/kg)
            IsActive = true
        };

        var paneer = new InventoryIngredient
        {
            IngredientId = canteenId * 1000 + 2,
            CanteenId = canteenId,
            Name = "Paneer",
            Category = "Dairy",
            BaseUnit = "g",
            CurrentStock = 1000m,
            ReservedStock = 0m,
            MinimumStockLevel = 200m,
            CostPerBaseUnit = 0.40m, // ₹0.40 per gram (₹400/kg)
            IsActive = true
        };

        var oil = new InventoryIngredient
        {
            IngredientId = canteenId * 1000 + 3,
            CanteenId = canteenId,
            Name = "Cooking Oil",
            Category = "Oils",
            BaseUnit = "ml",
            CurrentStock = 2000m,
            ReservedStock = 0m,
            MinimumStockLevel = 200m,
            CostPerBaseUnit = 0.15m, // ₹0.15 per ml (₹150/L)
            IsActive = true
        };

        context.InventoryIngredients.AddRange(rice, paneer, oil);
        await context.SaveChangesAsync();

        return (canteen, product, new List<InventoryIngredient> { rice, paneer, oil });
    }

    [Fact]
    public async Task CreateRecipe_ValidData_CalculatesCorrectFoodCostAndGrossMargin()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, ingredients) = await SeedTestDataAsync(context);

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        var dto = new CreateRecipeDto
        {
            ProductId = product.ProductId,
            ServingSize = 1,
            PreparationNotes = "Simmer paneer in gravy for 10 minutes",
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 200m, Unit = "g" }, // 200g * 0.08 = 16.00
                new() { IngredientId = ingredients[1].IngredientId, Quantity = 100m, Unit = "g" }, // 100g * 0.40 = 40.00
                new() { IngredientId = ingredients[2].IngredientId, Quantity = 20m, Unit = "ml" }  // 20ml * 0.15 = 3.00
            }
        };

        // Act
        var result = await service.CreateRecipeAsync(canteenId: 1, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.RecipeId > 0);
        Assert.Equal("Paneer Butter Masala", result.ProductName);
        Assert.Equal(180.00m, result.ProductPrice);
        Assert.Equal(3, result.Ingredients.Count);

        // Food Cost = 16 + 40 + 3 = 59.00
        Assert.Equal(59.00m, result.TotalFoodCostPerServing);

        // Margin = 180 - 59 = 121.00
        Assert.Equal(121.00m, result.GrossMarginAmount);

        // Margin % = (121 / 180) * 100 = 67.22%
        Assert.Equal(67.22m, result.GrossMarginPercentage);
        Assert.Equal("High", result.MarginStatus);

        // Portions available:
        // Rice: 5000 / 200 = 25
        // Paneer: 1000 / 100 = 10
        // Oil: 2000 / 20 = 100
        // Limiting ingredient = Paneer (10 portions)
        Assert.Equal(10, result.MaxPortionsAvailable);
        Assert.Equal("Paneer", result.LimitingIngredientName);
    }

    [Fact]
    public async Task CreateRecipe_WithUnitConversions_HandlesKgAndLCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, ingredients) = await SeedTestDataAsync(context);

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        var dto = new CreateRecipeDto
        {
            ProductId = product.ProductId,
            ServingSize = 2, // 2 portions per batch
            PreparationNotes = "2 portions batch recipe",
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                // 0.4 kg of Rice (base unit g) -> 400g -> cost 400 * 0.08 = 32.00
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 0.4m, Unit = "kg" },
                // 0.05 L of Oil (base unit ml) -> 50ml -> cost 50 * 0.15 = 7.50
                new() { IngredientId = ingredients[2].IngredientId, Quantity = 0.05m, Unit = "L" }
            }
        };

        // Act
        var result = await service.CreateRecipeAsync(canteenId: 1, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.ServingSize);
        // Batch cost = 32.00 + 7.50 = 39.50. Per serving = 39.50 / 2 = 19.75
        Assert.Equal(19.75m, result.TotalFoodCostPerServing);
        var riceItem = result.Ingredients.First(i => i.IngredientId == ingredients[0].IngredientId);
        var oilItem = result.Ingredients.First(i => i.IngredientId == ingredients[2].IngredientId);
        Assert.Equal(400m, riceItem.NormalizedQuantityInBaseUnit);
        Assert.Equal(50m, oilItem.NormalizedQuantityInBaseUnit);
    }

    [Fact]
    public async Task CreateRecipe_IncompatibleUnits_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, ingredients) = await SeedTestDataAsync(context);

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        var dto = new CreateRecipeDto
        {
            ProductId = product.ProductId,
            ServingSize = 1,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                // Rice is in 'g', attempting to specify 'pcs' should fail
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 5m, Unit = "pcs" }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRecipeAsync(1, dto));
        Assert.Contains("Cannot convert recipe unit", ex.Message);
    }

    [Fact]
    public async Task CreateRecipe_DuplicateProductRecipe_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, ingredients) = await SeedTestDataAsync(context);

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        var dto = new CreateRecipeDto
        {
            ProductId = product.ProductId,
            ServingSize = 1,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 200m, Unit = "g" }
            }
        };

        await service.CreateRecipeAsync(1, dto);

        // Act & Assert (second attempt for same product)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRecipeAsync(1, dto));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateRecipe_CrossTenantIngredientTampering_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, _) = await SeedTestDataAsync(context, canteenId: 1);
        var (_, _, ingredientsCanteen2) = await SeedTestDataAsync(context, canteenId: 2);

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        var dto = new CreateRecipeDto
        {
            ProductId = product.ProductId, // Canteen 1
            ServingSize = 1,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                // Ingredient belongs to Canteen 2!
                new() { IngredientId = ingredientsCanteen2[0].IngredientId, Quantity = 100m, Unit = "g" }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateRecipeAsync(canteenId: 1, dto));
        Assert.Contains("does not belong to canteen", ex.Message);
    }

    [Fact]
    public async Task UpdateRecipe_UpdatesQuantitiesAndRecalculatesCosting()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, ingredients) = await SeedTestDataAsync(context);

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        var createDto = new CreateRecipeDto
        {
            ProductId = product.ProductId,
            ServingSize = 1,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 200m, Unit = "g" }
            }
        };

        var created = await service.CreateRecipeAsync(1, createDto);
        Assert.Equal(16.00m, created.TotalFoodCostPerServing);

        // Act - Increase rice from 200g to 300g and add paneer 50g
        var updateDto = new UpdateRecipeDto
        {
            ServingSize = 1,
            PreparationNotes = "Updated recipe with extra paneer",
            IsActive = true,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 300m, Unit = "g" }, // 300 * 0.08 = 24.00
                new() { IngredientId = ingredients[1].IngredientId, Quantity = 50m, Unit = "g" }   // 50 * 0.40 = 20.00
            }
        };

        var updated = await service.UpdateRecipeAsync(1, created.RecipeId, updateDto);

        // Assert
        Assert.Equal(2, updated.Ingredients.Count);
        // Cost = 24.00 + 20.00 = 44.00
        Assert.Equal(44.00m, updated.TotalFoodCostPerServing);
        Assert.Equal(136.00m, updated.GrossMarginAmount);
        Assert.Equal("Updated recipe with extra paneer", updated.PreparationNotes);
    }

    [Fact]
    public async Task GetPortionAvailability_ReturnsBreakdownAndBottleneck()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, ingredients) = await SeedTestDataAsync(context);

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        var createDto = new CreateRecipeDto
        {
            ProductId = product.ProductId,
            ServingSize = 1,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 500m, Unit = "g" }, // 5000 available / 500 = 10 portions
                new() { IngredientId = ingredients[1].IngredientId, Quantity = 250m, Unit = "g" }  // 1000 available / 250 = 4 portions (Bottleneck!)
            }
        };

        await service.CreateRecipeAsync(1, createDto);

        // Act
        var availability = await service.GetPortionAvailabilityAsync(1, product.ProductId);

        // Assert
        Assert.NotNull(availability);
        Assert.Equal(4, availability.MaxPortionsAvailable);
        Assert.Equal("Paneer", availability.LimitingIngredientName);
        Assert.Equal(2, availability.IngredientBreakdown.Count);

        var riceBreakdown = availability.IngredientBreakdown.First(b => b.IngredientId == ingredients[0].IngredientId);
        Assert.Equal(10, riceBreakdown.PortionsPossible);

        var paneerBreakdown = availability.IngredientBreakdown.First(b => b.IngredientId == ingredients[1].IngredientId);
        Assert.Equal(4, paneerBreakdown.PortionsPossible);
    }

    [Fact]
    public async Task GetCostingSummaries_IncludesBothRecipeAndNonRecipeProducts()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, product, ingredients) = await SeedTestDataAsync(context);

        // Add a second product without a recipe
        var coldDrink = new Product
        {
            ProductId = 999,
            CanteenId = 1,
            CategoryId = product.CategoryId,
            Name = "Cold Drink Can",
            Description = "Packaged beverage",
            Price = 40.00m,
            StockQuantity = 30,
            IsAvailable = true,
            FoodType = "Veg"
        };
        context.Products.Add(coldDrink);
        await context.SaveChangesAsync();

        var recipeRepo = new RecipeRepository(context);
        var inventoryRepo = new InventoryRepository(context);
        var productRepo = new ProductRepository(context);
        var service = new RecipeService(recipeRepo, inventoryRepo, productRepo);

        await service.CreateRecipeAsync(1, new CreateRecipeDto
        {
            ProductId = product.ProductId,
            ServingSize = 1,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientId = ingredients[0].IngredientId, Quantity = 200m, Unit = "g" }
            }
        });

        // Act
        var summaries = (await service.GetCostingSummariesAsync(1)).ToList();

        // Assert
        Assert.Equal(2, summaries.Count);

        var cookedSummary = summaries.First(s => s.ProductId == product.ProductId);
        Assert.True(cookedSummary.HasRecipe);
        Assert.Equal(16.00m, cookedSummary.FoodCostPerServing);
        Assert.Equal(164.00m, cookedSummary.GrossMarginAmount);

        var packagedSummary = summaries.First(s => s.ProductId == 999);
        Assert.False(packagedSummary.HasRecipe);
        Assert.Equal(0m, packagedSummary.FoodCostPerServing);
        Assert.Equal(40.00m, packagedSummary.GrossMarginAmount);
        Assert.Equal("No Recipe", packagedSummary.MarginStatus);
    }
}
