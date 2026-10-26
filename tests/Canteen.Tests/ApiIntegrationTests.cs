using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Canteen.Tests;

public class ApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── 1. Registration & CanteenId Stripping ───────────────────────────────
    [Fact]
    public async Task Register_Returns201_AndDoesNotAssignClientSuppliedCanteenId()
    {
        // Arrange: client tries to pass CanteenId: 999
        var registerPayload = new
        {
            FullName = "Security Tester",
            Email = $"tester_{Guid.NewGuid():N}@test.com",
            Password = "Password123!",
            CanteenId = 999
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(result);
        Assert.Null(result.CanteenId);
        Assert.Equal("CanteenOwner", result.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));

        // Verify in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
        var persistedOwner = db.Owners.FirstOrDefault(o => o.Email == registerPayload.Email);
        Assert.NotNull(persistedOwner);
        Assert.Null(persistedOwner.CanteenId);
    }

    // ─── 2. Valid Login Returns JWT ──────────────────────────────────────────
    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndJwtToken()
    {
        // Arrange
        var email = $"login_{Guid.NewGuid():N}@test.com";
        var password = "ValidPassword123!";

        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "Login User",
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.Equal(email, auth.Email);
    }

    // ─── 3. Invalid Credentials Rejected ─────────────────────────────────────
    [Fact]
    public async Task Login_WithInvalidPasswordOrMissingUser_Returns401()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "Some User",
            Email = email,
            Password = "Password123!"
        });

        // Wrong password
        var wrongPassResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "WrongPassword!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassResponse.StatusCode);

        // Non-existent email
        var notFoundResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nobody_exists_here@test.com",
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, notFoundResponse.StatusCode);
    }

    // ─── 4. Protected Endpoints Reject Unauthenticated Requests ──────────────
    [Fact]
    public async Task ProtectedEndpoints_WithoutToken_Return401()
    {
        // Create Canteen
        var canteenRes = await _client.PostAsJsonAsync("/api/canteens", new { Name = "X", Location = "Y" });
        Assert.Equal(HttpStatusCode.Unauthorized, canteenRes.StatusCode);

        // Create Category
        var catRes = await _client.PostAsJsonAsync("/api/canteens/1/categories", new { Name = "X" });
        Assert.Equal(HttpStatusCode.Unauthorized, catRes.StatusCode);

        // Create Product
        var prodRes = await _client.PostAsJsonAsync("/api/products", new { CategoryId = 1, Name = "X", Price = 10m });
        Assert.Equal(HttpStatusCode.Unauthorized, prodRes.StatusCode);

        // Create Table
        var tableRes = await _client.PostAsJsonAsync("/api/canteens/1/tables", new { TableNumber = 1, Capacity = 4 });
        Assert.Equal(HttpStatusCode.Unauthorized, tableRes.StatusCode);
    }

    // ─── 5. Multi-Tenant Isolation: Owner A vs Owner B ────────────────────────
    [Fact]
    public async Task MultiTenantIsolation_OwnerACannotAccessOrModifyOwnerBResources()
    {
        // Setup Owner A + Canteen A
        var (tokenA, canteenAId) = await RegisterAndCreateCanteenAsync("ownerA", "Canteen A");

        // Setup Owner B + Canteen B
        var (tokenB, canteenBId) = await RegisterAndCreateCanteenAsync("ownerB", "Canteen B");

        Assert.NotEqual(canteenAId, canteenBId);

        // 1. Owner A attempts to UPDATE Owner B's Canteen
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/canteens/{canteenBId}")
        {
            Content = JsonContent.Create(new { Name = "Hacked Name", Location = "Hacked", IsActive = true })
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var updateRes = await _client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);

        // 2. Owner A attempts to DELETE Owner B's Canteen
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/canteens/{canteenBId}");
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var deleteRes = await _client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);

        // 3. Owner A attempts to CREATE a Category in Owner B's Canteen
        var catReq = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenBId}/categories")
        {
            Content = JsonContent.Create(new { Name = "Illegal Category", Description = "Cross tenant" })
        };
        catReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var catRes = await _client.SendAsync(catReq);
        Assert.Equal(HttpStatusCode.Forbidden, catRes.StatusCode);

        // 4. Owner A attempts to CREATE a Table in Owner B's Canteen
        var tableReq = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenBId}/tables")
        {
            Content = JsonContent.Create(new { TableNumber = 99, Capacity = 4 })
        };
        tableReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var tableRes = await _client.SendAsync(tableReq);
        Assert.Equal(HttpStatusCode.Forbidden, tableRes.StatusCode);
    }

    // ─── 6. Product Category Tenant Validation ───────────────────────────────
    [Fact]
    public async Task CreateProduct_WithCrossTenantOrMissingCategory_Returns400()
    {
        var (tokenA, canteenAId) = await RegisterAndCreateCanteenAsync("prodOwnerA", "Canteen A");
        var (tokenB, canteenBId) = await RegisterAndCreateCanteenAsync("prodOwnerB", "Canteen B");

        // Owner B creates a valid category in Canteen B
        var catReqB = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenBId}/categories")
        {
            Content = JsonContent.Create(new { Name = "Owner B Category" })
        };
        catReqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var catResB = await _client.SendAsync(catReqB);
        Assert.Equal(HttpStatusCode.Created, catResB.StatusCode);
        var createdCatB = await catResB.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(createdCatB);

        // Owner A attempts to create a product pointing to Owner B's category
        var prodCrossReq = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = createdCatB.CategoryId, // belongs to Canteen B
                Name = "Sneaky Product",
                Price = 15.00m,
                StockQuantity = 10
            })
        };
        prodCrossReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var prodCrossRes = await _client.SendAsync(prodCrossReq);

        // Must reject with 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, prodCrossRes.StatusCode);

        // Owner A attempts to create a product pointing to a non-existent category
        var prodMissingReq = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = 999999,
                Name = "Ghost Product",
                Price = 15.00m,
                StockQuantity = 10
            })
        };
        prodMissingReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var prodMissingRes = await _client.SendAsync(prodMissingReq);

        Assert.Equal(HttpStatusCode.BadRequest, prodMissingRes.StatusCode);
    }

    // ─── 7. Duplicate Table Numbers Return 409 Conflict ──────────────────────
    [Fact]
    public async Task CreateTable_WithDuplicateTableNumber_Returns409Conflict()
    {
        var (tokenA, canteenAId) = await RegisterAndCreateCanteenAsync("tableOwner", "Table Canteen");

        // First table with TableNumber = 10 -> 201 Created
        var t1Req = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenAId}/tables")
        {
            Content = JsonContent.Create(new { TableNumber = 10, Capacity = 4 })
        };
        t1Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var t1Res = await _client.SendAsync(t1Req);
        Assert.Equal(HttpStatusCode.Created, t1Res.StatusCode);

        // Duplicate table with TableNumber = 10 in same canteen -> 409 Conflict
        var t2Req = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenAId}/tables")
        {
            Content = JsonContent.Create(new { TableNumber = 10, Capacity = 6 })
        };
        t2Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var t2Res = await _client.SendAsync(t2Req);

        Assert.Equal(HttpStatusCode.Conflict, t2Res.StatusCode);
    }

    // ─── 8. DTO Validation Rejection ─────────────────────────────────────────
    [Fact]
    public async Task InvalidDtoPayloads_Return400BadRequest()
    {
        // Invalid email format and too-short password
        var invalidRegRes = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "A", // too short (MinimumLength = 2)
            Email = "not-a-valid-email",
            Password = "123" // too short (MinimumLength = 6)
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRegRes.StatusCode);

        var (token, _) = await RegisterAndCreateCanteenAsync("validUser", "Valid Canteen");

        // Negative price on product creation
        var invalidProductReq = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = 1,
                Name = "Invalid Item",
                Price = -10.00m,
                StockQuantity = -5
            })
        };
        invalidProductReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidProductRes = await _client.SendAsync(invalidProductReq);

        Assert.Equal(HttpStatusCode.BadRequest, invalidProductRes.StatusCode);
    }

    // ─── 9. Category Deletion Conflict Guard (BUG-001) ───────────────────────
    [Fact]
    public async Task DeleteCategory_WhenCategoryContainsProducts_Returns409Conflict()
    {
        var (token, canteenId) = await RegisterAndCreateCanteenAsync("catConflictOwner", "Conflict Canteen");

        // 1. Create a category
        var catReq = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenId}/categories")
        {
            Content = JsonContent.Create(new { Name = "Main Dishes", Description = "Entrees" })
        };
        catReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var catRes = await _client.SendAsync(catReq);
        Assert.Equal(HttpStatusCode.Created, catRes.StatusCode);
        var category = await catRes.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(category);

        // 2. Create a product inside that category
        var prodReq = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = category.CategoryId,
                Name = "Cheeseburger",
                Price = 8.50m,
                StockQuantity = 20
            })
        };
        prodReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var prodRes = await _client.SendAsync(prodReq);
        Assert.Equal(HttpStatusCode.Created, prodRes.StatusCode);

        // 3. Attempt to delete the category while it still contains products
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/canteens/{canteenId}/categories/{category.CategoryId}");
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleteRes = await _client.SendAsync(deleteReq);

        // Assert: Must return 409 Conflict, NOT 500 Internal Server Error
        Assert.Equal(HttpStatusCode.Conflict, deleteRes.StatusCode);
    }

    // ─── 10. Empty Category Deletion Allowed ─────────────────────────────────
    [Fact]
    public async Task DeleteCategory_WhenCategoryIsEmpty_Returns204NoContent()
    {
        var (token, canteenId) = await RegisterAndCreateCanteenAsync("emptyCatOwner", "Empty Cat Canteen");

        // 1. Create an empty category
        var catReq = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenId}/categories")
        {
            Content = JsonContent.Create(new { Name = "Empty Desserts" })
        };
        catReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var catRes = await _client.SendAsync(catReq);
        Assert.Equal(HttpStatusCode.Created, catRes.StatusCode);
        var category = await catRes.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(category);

        // 2. Delete the empty category
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/canteens/{canteenId}/categories/{category.CategoryId}");
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleteRes = await _client.SendAsync(deleteReq);

        // Assert: Returns 204 NoContent
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
    }

    // ─── 11. Product Availability Filter (BUG-003) ───────────────────────────
    [Fact]
    public async Task GetProducts_WithAvailableOnlyFilter_ReturnsOnlyInStockAndAvailableProducts()
    {
        var (token, canteenId) = await RegisterAndCreateCanteenAsync("filterOwner", "Filter Canteen");

        // 1. Create Category
        var catReq = new HttpRequestMessage(HttpMethod.Post, $"/api/canteens/{canteenId}/categories")
        {
            Content = JsonContent.Create(new { Name = "Snacks" })
        };
        catReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var catRes = await _client.SendAsync(catReq);
        Assert.Equal(HttpStatusCode.Created, catRes.StatusCode);
        var cat = await catRes.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(cat);

        // 2. Create Product 1: Available + In Stock (Stock = 10, IsAvailable = true)
        var p1Req = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = cat.CategoryId,
                Name = "Available InStock Snack",
                Price = 5.00m,
                StockQuantity = 10
            })
        };
        p1Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var p1Res = await _client.SendAsync(p1Req);
        Assert.Equal(HttpStatusCode.Created, p1Res.StatusCode);

        // 3. Create Product 2: Available + Zero Stock (Stock = 0, IsAvailable = true)
        var p2Req = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = cat.CategoryId,
                Name = "SoldOut Snack",
                Price = 4.00m,
                StockQuantity = 0
            })
        };
        p2Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var p2Res = await _client.SendAsync(p2Req);
        Assert.Equal(HttpStatusCode.Created, p2Res.StatusCode);

        // 4. Create Product 3: Unavailable + In Stock (Stock = 5, IsAvailable = false)
        var p3Req = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = cat.CategoryId,
                Name = "Unavailable Snack",
                Price = 6.00m,
                StockQuantity = 5
            })
        };
        p3Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var p3Res = await _client.SendAsync(p3Req);
        Assert.Equal(HttpStatusCode.Created, p3Res.StatusCode);
        var p3 = await p3Res.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(p3);

        // Set Product 3 to IsAvailable = false
        var updateP3Req = new HttpRequestMessage(HttpMethod.Put, $"/api/products/{p3.ProductId}")
        {
            Content = JsonContent.Create(new
            {
                CategoryId = cat.CategoryId,
                Name = "Unavailable Snack",
                Price = 6.00m,
                StockQuantity = 5,
                IsAvailable = false
            })
        };
        updateP3Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateP3Res = await _client.SendAsync(updateP3Req);
        Assert.Equal(HttpStatusCode.OK, updateP3Res.StatusCode);

        // ─── Verification A: availableOnly = true by Canteen ─────────────────
        var filteredRes = await _client.GetAsync($"/api/products/canteen/{canteenId}?availableOnly=true");
        Assert.Equal(HttpStatusCode.OK, filteredRes.StatusCode);
        var filteredProducts = await filteredRes.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(filteredProducts);
        Assert.Single(filteredProducts);
        Assert.Equal("Available InStock Snack", filteredProducts[0].Name);

        // ─── Verification B: default (availableOnly omitted) returns all 3 ──
        var allRes = await _client.GetAsync($"/api/products/canteen/{canteenId}");
        Assert.Equal(HttpStatusCode.OK, allRes.StatusCode);
        var allProducts = await allRes.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(allProducts);
        Assert.Equal(3, allProducts.Count);

        // ─── Verification C: availableOnly = true by Category ────────────────
        var catFilteredRes = await _client.GetAsync($"/api/products/canteen/{canteenId}/category/{cat.CategoryId}?availableOnly=true");
        Assert.Equal(HttpStatusCode.OK, catFilteredRes.StatusCode);
        var catFilteredProducts = await catFilteredRes.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(catFilteredProducts);
        Assert.Single(catFilteredProducts);
        Assert.Equal("Available InStock Snack", catFilteredProducts[0].Name);

        // ─── Verification D: default by Category returns all 3 ───────────────
        var catAllRes = await _client.GetAsync($"/api/products/canteen/{canteenId}/category/{cat.CategoryId}");
        Assert.Equal(HttpStatusCode.OK, catAllRes.StatusCode);
        var catAllProducts = await catAllRes.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(catAllProducts);
        Assert.Equal(3, catAllProducts.Count);
    }

    // ─── Helper: Register, Create Canteen, and Login to receive CanteenId claim
    private async Task<(string Token, int CanteenId)> RegisterAndCreateCanteenAsync(string prefix, string canteenName)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@test.com";
        var password = "SecurePassword123!";

        // 1. Register owner
        var regRes = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = $"{prefix} User",
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.Created, regRes.StatusCode);
        var regAuth = await regRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(regAuth);

        // 2. Create canteen using initial token
        var canteenReq = new HttpRequestMessage(HttpMethod.Post, "/api/canteens")
        {
            Content = JsonContent.Create(new { Name = canteenName, Location = "Building 1" })
        };
        canteenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", regAuth.Token);
        var canteenRes = await _client.SendAsync(canteenReq);
        Assert.Equal(HttpStatusCode.Created, canteenRes.StatusCode);
        var createdCanteen = await canteenRes.Content.ReadFromJsonAsync<CanteenDto>();
        Assert.NotNull(createdCanteen);

        // 3. Login to acquire a fresh JWT signed with the new CanteenId claim
        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginAuth = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginAuth);
        Assert.Equal(createdCanteen.CanteenId, loginAuth.CanteenId);

        return (loginAuth.Token, createdCanteen.CanteenId);
    }
}
