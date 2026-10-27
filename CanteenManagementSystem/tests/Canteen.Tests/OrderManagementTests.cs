using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Canteen.Tests;

public class OrderManagementTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrderManagementTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_WithValidQrToken_CalculatesServerPrice_AndDeductsStock()
    {
        // 1. Arrange canteen, table, category, and product
        var (token, canteenId) = await CreateOwnerAndCanteenAsync("order_success");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tableId, qrToken) = await CreateTableAsync(canteenId, 1);
        var categoryId = await CreateCategoryAsync(canteenId, "Mains");
        var productId = await CreateProductAsync(canteenId, categoryId, "Veg Thali", price: 120.00m, stock: 10);

        // 2. Act: Customer places order via QR token
        var orderPayload = new CreateOrderDto
        {
            TableId = tableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = productId, Quantity = 2 }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/orders?qrToken={qrToken}", orderPayload);

        // 3. Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected Created, got {response.StatusCode}. Content: {content}");
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(canteenId, order.CanteenId);
        Assert.Equal(tableId, order.TableId);
        Assert.Equal(240.00m, order.TotalAmount); // 2 * 120.00
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Single(order.Items);
        Assert.Equal(2, order.Items[0].Quantity);
        Assert.Equal(120.00m, order.Items[0].UnitPrice);
        Assert.Equal(240.00m, order.Items[0].Subtotal);

        // Verify stock deducted in DB: 10 - 2 = 8
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
        var dbProduct = db.Products.First(p => p.ProductId == productId);
        Assert.Equal(8, dbProduct.StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_WithProductFromDifferentCanteen_Returns400BadRequest()
    {
        // Arrange: Canteen A
        var (tokenA, canteenA) = await CreateOwnerAndCanteenAsync("cross_canteen_a");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var (tableIdA, qrTokenA) = await CreateTableAsync(canteenA, 1);

        // Arrange: Canteen B with a product
        var (tokenB, canteenB) = await CreateOwnerAndCanteenAsync("cross_canteen_b");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var catB = await CreateCategoryAsync(canteenB, "Snacks");
        var productB = await CreateProductAsync(canteenB, catB, "Samosa", price: 20.00m, stock: 15);

        // Act: Customer at Table A tries to order Product B from Canteen B
        var maliciousPayload = new CreateOrderDto
        {
            TableId = tableIdA,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = productB, Quantity = 1 }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/orders?qrToken={qrTokenA}", maliciousPayload);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WhenStockInsufficient_RestoresPreviouslyDeductedItems()
    {
        // Arrange
        var (token, canteenId) = await CreateOwnerAndCanteenAsync("stock_rollback");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tableId, qrToken) = await CreateTableAsync(canteenId, 1);
        var categoryId = await CreateCategoryAsync(canteenId, "Combos");

        // Item 1 has 5 in stock; Item 2 only has 1 in stock
        var product1Id = await CreateProductAsync(canteenId, categoryId, "Burger", price: 80.00m, stock: 5);
        var product2Id = await CreateProductAsync(canteenId, categoryId, "Drink", price: 30.00m, stock: 1);

        // Act: Customer orders 2 Burgers (valid) and 3 Drinks (insufficient!)
        var orderPayload = new CreateOrderDto
        {
            TableId = tableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product1Id, Quantity = 2 },
                new() { ProductId = product2Id, Quantity = 3 }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/orders?qrToken={qrToken}", orderPayload);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Verify Product 1 stock was rolled back to original 5 (no stock leak!)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
        var dbProduct1 = db.Products.First(p => p.ProductId == product1Id);
        var dbProduct2 = db.Products.First(p => p.ProductId == product2Id);
        Assert.Equal(5, dbProduct1.StockQuantity);
        Assert.Equal(1, dbProduct2.StockQuantity);
    }

    [Fact]
    public async Task GetAllOrders_WhenOwnerNotAuthorizedForCanteen_Returns403Forbidden()
    {
        // Arrange: Owner A and Owner B
        var (tokenA, canteenA) = await CreateOwnerAndCanteenAsync("auth_owner_a");
        var (_, canteenB) = await CreateOwnerAndCanteenAsync("auth_owner_b");

        // Act: Owner A tries to access Canteen B's orders
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.GetAsync($"/api/canteens/{canteenB}/orders");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_KitchenWorkflow_EnforcesStateTransitions()
    {
        // 1. Arrange order in Canteen
        var (token, canteenId) = await CreateOwnerAndCanteenAsync("kitchen_flow");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tableId, qrToken) = await CreateTableAsync(canteenId, 1);
        var categoryId = await CreateCategoryAsync(canteenId, "FastFood");
        var productId = await CreateProductAsync(canteenId, categoryId, "Sandwich", price: 50.00m, stock: 10);

        var orderRes = await _client.PostAsJsonAsync($"/api/orders?qrToken={qrToken}", new CreateOrderDto
        {
            TableId = tableId,
            Items = new List<CreateOrderItemDto> { new() { ProductId = productId, Quantity = 1 } }
        });
        orderRes.EnsureSuccessStatusCode();
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>();

        // 2. Act: Valid transition: Pending -> Confirmed
        var step1 = await _client.PatchAsJsonAsync($"/api/canteens/{canteenId}/orders/{order!.OrderId}/status", new UpdateOrderStatusDto
        {
            Status = "Confirmed"
        });
        Assert.Equal(HttpStatusCode.OK, step1.StatusCode);
        var updated1 = await step1.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Confirmed", updated1!.Status);

        // 3. Act: Valid transition: Confirmed -> InPreparation
        var step2 = await _client.PatchAsJsonAsync($"/api/canteens/{canteenId}/orders/{order.OrderId}/status", new UpdateOrderStatusDto
        {
            Status = "InPreparation"
        });
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        // 4. Act: Illegal transition: InPreparation -> Completed (skipping Ready)
        var illegalStep = await _client.PatchAsJsonAsync($"/api/canteens/{canteenId}/orders/{order.OrderId}/status", new UpdateOrderStatusDto
        {
            Status = "Completed"
        });
        Assert.Equal(HttpStatusCode.BadRequest, illegalStep.StatusCode);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(string Token, int CanteenId)> CreateOwnerAndCanteenAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@test.com";
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = $"Owner {prefix}",
            Email = email,
            Password = "Password123!"
        });
        regResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Password123!"
        });
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var canteenResponse = await _client.PostAsJsonAsync("/api/canteens", new
        {
            Name = $"Canteen {prefix}",
            Location = "100 Food Court"
        });
        canteenResponse.EnsureSuccessStatusCode();
        var canteen = await canteenResponse.Content.ReadFromJsonAsync<CanteenDto>();

        return (canteen!.Token ?? auth.Token, canteen.CanteenId);
    }

    private async Task<(int TableId, string QrToken)> CreateTableAsync(int canteenId, int tableNumber)
    {
        var res = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/tables", new
        {
            TableNumber = tableNumber,
            Capacity = 4
        });
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<CanteenTableDto>();
        return (dto!.TableId, dto.QRToken);
    }

    private async Task<int> CreateCategoryAsync(int canteenId, string name)
    {
        var res = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/categories", new
        {
            Name = name,
            Description = "Delicious"
        });
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<CategoryDto>();
        return dto!.CategoryId;
    }

    private async Task<int> CreateProductAsync(int canteenId, int categoryId, string name, decimal price, int stock)
    {
        var res = await _client.PostAsJsonAsync("/api/products", new
        {
            Name = name,
            Description = "Tasty",
            Price = price,
            StockQuantity = stock,
            CategoryId = categoryId,
            IsAvailable = true
        });
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<ProductDto>();
        return dto!.ProductId;
    }
}
