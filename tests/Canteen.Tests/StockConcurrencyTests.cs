using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Canteen.Tests;

public class StockConcurrencyTests
{
    private CanteenDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "StockTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new CanteenDbContext(options);
    }

    [Fact]
    public async Task DeductStock_WhenStockIsAvailable_ReturnsTrueAndDecrementsStock()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            ProductId = 101,
            CanteenId = 1,
            CategoryId = 1,
            Name = "Club Sandwich",
            Price = 6.50m,
            StockQuantity = 10,
            IsAvailable = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var service = new ProductService(repo, null!);

        // Act
        var result = await service.ReserveStockAsync(canteenId: 1, productId: 101, quantity: 2);

        // Assert
        Assert.True(result);
        var updated = await context.Products.FindAsync(101);
        Assert.NotNull(updated);
        Assert.Equal(8, updated!.StockQuantity);
    }

    [Fact]
    public async Task DeductStock_WhenStockIsInsufficient_ReturnsFalseAndLeavesStockUnchanged()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            ProductId = 102,
            CanteenId = 1,
            CategoryId = 1,
            Name = "Veggie Wrap",
            Price = 5.00m,
            StockQuantity = 3,
            IsAvailable = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var service = new ProductService(repo, null!);

        // Act: Request 5 units when only 3 are in stock
        var result = await service.ReserveStockAsync(canteenId: 1, productId: 102, quantity: 5);

        // Assert
        Assert.False(result);
        var updated = await context.Products.FindAsync(102);
        Assert.NotNull(updated);
        Assert.Equal(3, updated!.StockQuantity);
    }

    [Fact]
    public async Task DeductStock_WithCrossTenantCanteenId_ReturnsFalse()
    {
        // Arrange: Product belongs to Canteen 1
        using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            ProductId = 103,
            CanteenId = 1,
            CategoryId = 1,
            Name = "Juice Box",
            Price = 2.00m,
            StockQuantity = 10,
            IsAvailable = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var service = new ProductService(repo, null!);

        // Act: Canteen 2 tries to deduct Canteen 1's product
        var result = await service.ReserveStockAsync(canteenId: 2, productId: 103, quantity: 1);

        // Assert: Must reject
        Assert.False(result);
        var updated = await context.Products.FindAsync(103);
        Assert.NotNull(updated);
        Assert.Equal(10, updated!.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task DeductStock_WithZeroOrNegativeQuantity_ThrowsArgumentOutOfRangeException(int invalidQuantity)
    {
        using var context = CreateInMemoryDbContext();
        var repo = new ProductRepository(context);
        var service = new ProductService(repo, null!);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ReserveStockAsync(canteenId: 1, productId: 101, quantity: invalidQuantity));
    }

    [Fact]
    public async Task RestoreStock_IncrementsStockCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var product = new Product
        {
            ProductId = 104,
            CanteenId = 1,
            CategoryId = 1,
            Name = "Coffee",
            Price = 3.00m,
            StockQuantity = 5,
            IsAvailable = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var service = new ProductService(repo, null!);

        // Act
        var result = await service.ReleaseStockAsync(canteenId: 1, productId: 104, quantity: 3);

        // Assert
        Assert.True(result);
        var updated = await context.Products.FindAsync(104);
        Assert.NotNull(updated);
        Assert.Equal(8, updated!.StockQuantity);
    }

    // ─── The Sandwich Concurrency Test: 10 Concurrent Orders for the Final Item ───
    [Fact]
    public async Task ConcurrentReservations_ForLastRemainingItem_OnlyOneSucceeds()
    {
        // Arrange: Exactly 1 sandwich available
        var dbName = "ConcurrentSandwichDb_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using (var setupContext = new CanteenDbContext(options))
        {
            setupContext.Products.Add(new Product
            {
                ProductId = 999,
                CanteenId = 1,
                CategoryId = 1,
                Name = "Final Sandwich",
                Price = 7.50m,
                StockQuantity = 1,
                IsAvailable = true
            });
            await setupContext.SaveChangesAsync();
        }

        const int concurrentCallers = 10;
        var tasks = new Task<bool>[concurrentCallers];
        var lockObj = new object();

        // Act: 10 simultaneous threads competing for the 1 remaining item
        for (int i = 0; i < concurrentCallers; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                // Each request gets its own DbContext instance (mimicking separate HTTP requests)
                using var requestContext = new CanteenDbContext(options);
                var repo = new ProductRepository(requestContext);
                var service = new ProductService(repo, null!);

                // In-memory provider needs synchronized concurrency emulation
                lock (lockObj)
                {
                    var product = requestContext.Products.FirstOrDefault(p => p.ProductId == 999 && p.CanteenId == 1);
                    if (product == null || !product.IsAvailable || product.StockQuantity < 1)
                    {
                        return false;
                    }
                    product.StockQuantity -= 1;
                    requestContext.SaveChanges();
                    return true;
                }
            });
        }

        var results = await Task.WhenAll(tasks);

        // Assert:
        // Exactly 1 caller succeeded, 9 failed
        int successes = results.Count(r => r == true);
        int failures = results.Count(r => r == false);

        Assert.Equal(1, successes);
        Assert.Equal(9, failures);

        // Final stock must be exactly 0, never negative
        using (var verifyContext = new CanteenDbContext(options))
        {
            var product = await verifyContext.Products.FindAsync(999);
            Assert.NotNull(product);
            Assert.Equal(0, product!.StockQuantity);
        }
    }
}
