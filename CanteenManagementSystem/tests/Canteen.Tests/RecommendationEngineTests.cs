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

public class RecommendationEngineTests
{
    private CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "RecommendationTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    private async Task<(
        Canteen.Model.Entities.Canteen canteen,
        Product breakfastDish,
        Product lunchDish,
        Product eveningDish,
        Product beverageDish,
        Product outOfStockDish
    )> SeedTestDataAsync(CanteenDbContext context)
    {
        var canteen = new Canteen.Model.Entities.Canteen
        {
            CanteenId = 1,
            Name = "Campus Hub Canteen",
            Slug = "campus-hub-canteen",
            IsActive = true
        };
        context.Canteens.Add(canteen);

        var breakfastCat = new Category { CategoryId = 1, CanteenId = 1, Name = "Breakfast" };
        var lunchCat = new Category { CategoryId = 2, CanteenId = 1, Name = "Main Course" };
        var snacksCat = new Category { CategoryId = 3, CanteenId = 1, Name = "Snacks" };
        var beverageCat = new Category { CategoryId = 4, CanteenId = 1, Name = "Beverages" };
        context.Categories.AddRange(breakfastCat, lunchCat, snacksCat, beverageCat);

        var breakfastDish = new Product
        {
            ProductId = 10,
            CanteenId = 1,
            CategoryId = 1,
            Name = "Kanda Poha & Masala Chai",
            Description = "Traditional Maharashtrian breakfast flattened rice with hot tea",
            Price = 45.00m,
            StockQuantity = 50,
            IsAvailable = true,
            FoodType = "Veg",
            Category = breakfastCat
        };

        var lunchDish = new Product
        {
            ProductId = 20,
            CanteenId = 1,
            CategoryId = 2,
            Name = "Special Veg Thali",
            Description = "Complete lunch meal with curries, rice, dal, and chapatis",
            Price = 120.00m,
            StockQuantity = 30,
            IsAvailable = true,
            FoodType = "Veg",
            Category = lunchCat
        };

        var eveningDish = new Product
        {
            ProductId = 30,
            CanteenId = 1,
            CategoryId = 3,
            Name = "Crispy Samosa Plate",
            Description = "Golden fried samosas with mint and tamarind chutney",
            Price = 30.00m,
            StockQuantity = 40,
            IsAvailable = true,
            FoodType = "Veg",
            Category = snacksCat
        };

        var beverageDish = new Product
        {
            ProductId = 40,
            CanteenId = 1,
            CategoryId = 4,
            Name = "Cold Coffee with Ice Cream",
            Description = "Chilled creamy blend of espresso and vanilla ice cream",
            Price = 60.00m,
            StockQuantity = 25,
            IsAvailable = true,
            FoodType = "Veg",
            Category = beverageCat
        };

        var outOfStockDish = new Product
        {
            ProductId = 50,
            CanteenId = 1,
            CategoryId = 2,
            Name = "Chicken Biryani",
            Description = "Aromatic spiced rice with tender chicken",
            Price = 160.00m,
            StockQuantity = 0, // OUT OF STOCK
            IsAvailable = false,
            FoodType = "Non-Veg",
            Category = lunchCat
        };

        context.Products.AddRange(breakfastDish, lunchDish, eveningDish, beverageDish, outOfStockDish);
        await context.SaveChangesAsync();

        return (canteen, breakfastDish, lunchDish, eveningDish, beverageDish, outOfStockDish);
    }

    [Fact]
    public async Task GetMenuRecommendations_Morning_RanksBreakfastItemsHighest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, breakfast, lunch, _, _, _) = await SeedTestDataAsync(context);

        var repo = new RecommendationRepository(context);
        var service = new FoodRecommendationService(repo);

        // Simulated time: 08:30 AM IST (03:00 AM UTC)
        var morningUtc = new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc);

        // Act
        var recommendations = (await service.GetMenuRecommendationsAsync(canteenId: 1, limit: 4, simulatedTimeUtc: morningUtc)).ToList();

        // Assert
        Assert.NotEmpty(recommendations);
        var topRecommendation = recommendations.First();
        Assert.Equal(breakfast.ProductId, topRecommendation.ProductId);
        Assert.Equal("Trending Breakfast", topRecommendation.BadgeText);
        Assert.Equal("TimeBased", topRecommendation.RecommendationCategory);
    }

    [Fact]
    public async Task GetMenuRecommendations_Afternoon_RanksLunchItemsHighest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, lunch, _, _, _) = await SeedTestDataAsync(context);

        var repo = new RecommendationRepository(context);
        var service = new FoodRecommendationService(repo);

        // Simulated time: 13:00 IST (07:30 AM UTC)
        var afternoonUtc = new DateTime(2026, 11, 2, 7, 30, 0, DateTimeKind.Utc);

        // Act
        var recommendations = (await service.GetMenuRecommendationsAsync(canteenId: 1, limit: 4, simulatedTimeUtc: afternoonUtc)).ToList();

        // Assert
        Assert.NotEmpty(recommendations);
        var topRecommendation = recommendations.First();
        Assert.Equal(lunch.ProductId, topRecommendation.ProductId);
        Assert.Equal("Lunch Favorite", topRecommendation.BadgeText);
    }

    [Fact]
    public async Task GetMenuRecommendations_SuppressesOutOfStockProducts()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, _, _, _, outOfStock) = await SeedTestDataAsync(context);

        var repo = new RecommendationRepository(context);
        var service = new FoodRecommendationService(repo);

        // Act
        var recommendations = (await service.GetMenuRecommendationsAsync(canteenId: 1, limit: 10)).ToList();

        // Assert
        Assert.DoesNotContain(recommendations, r => r.ProductId == outOfStock.ProductId);
    }

    [Fact]
    public async Task GetMenuRecommendations_FoodTypeFilter_FiltersStrictly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        await SeedTestDataAsync(context);

        var repo = new RecommendationRepository(context);
        var service = new FoodRecommendationService(repo);

        // Act
        var vegRecommendations = (await service.GetMenuRecommendationsAsync(canteenId: 1, foodType: "Veg", limit: 10)).ToList();

        // Assert
        Assert.NotEmpty(vegRecommendations);
        Assert.All(vegRecommendations, r => Assert.Equal("Veg", r.FoodType));
    }

    [Fact]
    public async Task GetCartAddons_BasketMining_RecommendsFrequentlyBoughtPair()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, _, eveningDish, beverageDish, _) = await SeedTestDataAsync(context);

        // Seed 5 historical orders where Samosa (eveningDish) was ordered with Cold Coffee (beverageDish)
        for (int i = 1; i <= 5; i++)
        {
            var order = new Order
            {
                OrderId = i,
                CanteenId = 1,
                TableId = 1,
                OrderNumber = $"ORD-HIST-{i:00}",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Items = new List<OrderItem>
                {
                    new() { ProductId = eveningDish.ProductId, Quantity = 1, UnitPrice = eveningDish.Price },
                    new() { ProductId = beverageDish.ProductId, Quantity = 1, UnitPrice = beverageDish.Price }
                }
            };
            context.Orders.Add(order);
        }
        await context.SaveChangesAsync();

        var repo = new RecommendationRepository(context);
        var service = new FoodRecommendationService(repo);

        // Customer currently has Samosa in cart
        var request = new CartAddonRequestDto
        {
            CartProductIds = new List<int> { eveningDish.ProductId },
            Limit = 3
        };

        // Act
        var addons = (await service.GetCartAddonsAsync(1, request)).ToList();

        // Assert
        Assert.NotEmpty(addons);
        var topAddon = addons.First();
        Assert.Equal(beverageDish.ProductId, topAddon.ProductId);
        Assert.Equal("Crispy Samosa Plate", topAddon.PairedWithProductName);
        Assert.True(topAddon.PairConfidencePercentage >= 50);
        Assert.DoesNotContain(addons, a => a.ProductId == eveningDish.ProductId); // Never recommends what's already in the cart
    }

    [Fact]
    public async Task RecordEvent_And_GetInsights_CalculatesCorrectCTRAndConversion()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, breakfast, _, _, _, _) = await SeedTestDataAsync(context);

        var repo = new RecommendationRepository(context);
        var service = new FoodRecommendationService(repo);

        // 100 Impressions, 20 Clicks, 5 Orders for the breakfast item
        for (int i = 0; i < 100; i++)
        {
            await service.RecordEventAsync(1, new RecordRecommendationEventDto
            {
                ProductId = breakfast.ProductId,
                EventType = "Impression",
                RecommendationCategory = "TimeBased"
            });
        }

        for (int i = 0; i < 20; i++)
        {
            await service.RecordEventAsync(1, new RecordRecommendationEventDto
            {
                ProductId = breakfast.ProductId,
                EventType = "Click",
                RecommendationCategory = "TimeBased"
            });
        }

        for (int i = 0; i < 5; i++)
        {
            await service.RecordEventAsync(1, new RecordRecommendationEventDto
            {
                ProductId = breakfast.ProductId,
                EventType = "Ordered",
                RecommendationCategory = "TimeBased"
            });
        }

        // Act
        var insights = await service.GetInsightsAsync(1, daysBack: 30);

        // Assert
        Assert.Equal(100, insights.TotalImpressions);
        Assert.Equal(20, insights.TotalClicks);
        Assert.Equal(5, insights.TotalOrders);
        // CTR = 20 / 100 = 20.0%
        Assert.Equal(20.0, insights.ClickThroughRate);
        // Conversion Rate = 5 / 20 = 25.0%
        Assert.Equal(25.0, insights.ConversionRate);

        Assert.Single(insights.TopConvertingDishes);
        var topDish = insights.TopConvertingDishes[0];
        Assert.Equal(breakfast.ProductId, topDish.ProductId);
        Assert.Equal("Kanda Poha & Masala Chai", topDish.ProductName);
        Assert.Equal(20, topDish.Clicks);
        Assert.Equal(5, topDish.Orders);
        Assert.Equal(25.0, topDish.ConversionRate);
    }
}

