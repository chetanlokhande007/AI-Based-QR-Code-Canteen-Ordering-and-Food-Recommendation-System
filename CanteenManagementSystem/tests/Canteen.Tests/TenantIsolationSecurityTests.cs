using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Canteen.Model.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Canteen.Tests;

public class TenantIsolationSecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TenantIsolationSecurityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, int OwnerId, int CanteenId)> RegisterOwnerAndCanteenAsync(string emailPrefix, string canteenName)
    {
        var email = $"{emailPrefix}_{Guid.NewGuid():N}@test.com";
        var regDto = new RegisterOwnerDto
        {
            FullName = $"{emailPrefix} Owner",
            Email = email,
            Password = "SecurePassword123!"
        };

        var res = await _client.PostAsJsonAsync("/api/auth/register", regDto);
        res.EnsureSuccessStatusCode();
        var data = await res.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(data);

        // Set token to create their canteen
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.Token);
        var createCanteenRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = canteenName,
            Location = "Campus Block"
        });
        createCanteenRes.EnsureSuccessStatusCode();
        var canteen = await createCanteenRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(canteen);
        Assert.NotNull(canteen.Token);

        return (canteen.Token, data.OwnerId, canteen.CanteenId);
    }

    private async Task<string> CreatePlatformAdminTokenAsync(string emailPrefix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();

        var adminEmail = $"{emailPrefix}_{Guid.NewGuid():N}@admin.com";
        var admin = new Owner
        {
            FullName = "Platform Super Admin",
            Email = adminEmail,
            PasswordHash = "fakehash",
            Role = "PlatformAdmin",
            CreatedAt = DateTime.UtcNow
        };

        db.Owners.Add(admin);
        await db.SaveChangesAsync();

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var token = await authService.GenerateTokenForOwnerAsync(admin.OwnerId);
        Assert.NotNull(token);
        return token;
    }

    // ─── 1. Category Cross-Tenant Isolation ─────────────────────────────────
    [Fact]
    public async Task OwnerA_Cannot_Create_Update_Or_Delete_OwnerB_Category_Returns403()
    {
        var (tokenA, _, canteenIdA) = await RegisterOwnerAndCanteenAsync("owner_a_cat", "Canteen A");
        var (tokenB, _, canteenIdB) = await RegisterOwnerAndCanteenAsync("owner_b_cat", "Canteen B");

        // Owner B creates a category in Canteen B
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var catResB = await _client.PostAsJsonAsync($"/api/canteens/{canteenIdB}/categories", new CreateCategoryDto
        {
            Name = "Beverages B",
            Description = "Cold and Hot Drinks"
        });
        catResB.EnsureSuccessStatusCode();
        var catB = await catResB.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(catB);

        // Switch identity to Owner A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Owner A attempts to create a category in Canteen B
        var createRes = await _client.PostAsJsonAsync($"/api/canteens/{canteenIdB}/categories", new CreateCategoryDto
        {
            Name = "Malicious Category",
            Description = "Should be rejected"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createRes.StatusCode);

        // Owner A attempts to update Owner B's category
        var updateRes = await _client.PutAsJsonAsync($"/api/canteens/{canteenIdB}/categories/{catB.CategoryId}", new UpdateCategoryDto
        {
            Name = "Tampered Category",
            Description = "Should be rejected"
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);

        // Owner A attempts to delete Owner B's category
        var deleteRes = await _client.DeleteAsync($"/api/canteens/{canteenIdB}/categories/{catB.CategoryId}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    // ─── 2. Product Cross-Tenant Isolation ──────────────────────────────────
    [Fact]
    public async Task OwnerA_Cannot_Create_Update_Or_Delete_OwnerB_Product_Returns403()
    {
        var (tokenA, _, canteenIdA) = await RegisterOwnerAndCanteenAsync("owner_a_prod", "Canteen A");
        var (tokenB, _, canteenIdB) = await RegisterOwnerAndCanteenAsync("owner_b_prod", "Canteen B");

        // Owner B creates category and product in Canteen B
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var catResB = await _client.PostAsJsonAsync($"/api/canteens/{canteenIdB}/categories", new CreateCategoryDto
        {
            Name = "Snacks B"
        });
        var catB = await catResB.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(catB);

        var prodResB = await _client.PostAsJsonAsync($"/api/products?canteenId={canteenIdB}", new CreateProductDto
        {
            CategoryId = catB.CategoryId,
            Name = "Samosa B",
            Price = 20.00m,
            StockQuantity = 50
        });
        prodResB.EnsureSuccessStatusCode();
        var prodB = await prodResB.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        Assert.NotNull(prodB);

        // Switch identity to Owner A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Owner A attempts to create product in Canteen B
        var createRes = await _client.PostAsJsonAsync($"/api/products?canteenId={canteenIdB}", new CreateProductDto
        {
            CategoryId = catB.CategoryId,
            Name = "Injected Samosa",
            Price = 1.00m,
            StockQuantity = 100
        });
        Assert.Equal(HttpStatusCode.Forbidden, createRes.StatusCode);

        // Owner A attempts to update Owner B's product
        var updateRes = await _client.PutAsJsonAsync($"/api/products/{prodB.ProductId}?canteenId={canteenIdB}", new UpdateProductDto
        {
            Name = "Tampered Samosa",
            Price = 0.01m,
            StockQuantity = 0
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);

        // Owner A attempts to delete Owner B's product
        var deleteRes = await _client.DeleteAsync($"/api/products/{prodB.ProductId}?canteenId={canteenIdB}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    // ─── 3. Table and QR Cross-Tenant Isolation ─────────────────────────────
    [Fact]
    public async Task OwnerA_Cannot_Access_Or_Modify_OwnerB_Tables_Returns403()
    {
        var (tokenA, _, canteenIdA) = await RegisterOwnerAndCanteenAsync("owner_a_tbl", "Canteen A");
        var (tokenB, _, canteenIdB) = await RegisterOwnerAndCanteenAsync("owner_b_tbl", "Canteen B");

        // Owner B creates table in Canteen B
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var tableResB = await _client.PostAsJsonAsync($"/api/canteens/{canteenIdB}/tables", new CreateCanteenTableDto
        {
            TableNumber = 101,
            Capacity = 4
        });
        tableResB.EnsureSuccessStatusCode();
        var tableB = await tableResB.Content.ReadFromJsonAsync<CanteenTableDto>(JsonOptions);
        Assert.NotNull(tableB);

        // Switch to Owner A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Owner A attempts to create table in Canteen B
        var createRes = await _client.PostAsJsonAsync($"/api/canteens/{canteenIdB}/tables", new CreateCanteenTableDto
        {
            TableNumber = 102,
            Capacity = 2
        });
        Assert.Equal(HttpStatusCode.Forbidden, createRes.StatusCode);

        // Owner A attempts to update Owner B's table
        var updateRes = await _client.PutAsJsonAsync($"/api/canteens/{canteenIdB}/tables/{tableB.TableId}", new UpdateCanteenTableDto
        {
            TableNumber = 101,
            Capacity = 10
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);

        // Owner A attempts to regenerate Owner B's QR token
        var regenRes = await _client.PostAsync($"/api/canteens/{canteenIdB}/tables/{tableB.TableId}/regenerate-qr", null);
        Assert.Equal(HttpStatusCode.Forbidden, regenRes.StatusCode);

        // Owner A attempts to delete Owner B's table
        var deleteRes = await _client.DeleteAsync($"/api/canteens/{canteenIdB}/tables/{tableB.TableId}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);

        // Owner A attempts to list Owner B's tables
        var getTablesRes = await _client.GetAsync($"/api/canteens/{canteenIdB}/tables");
        Assert.Equal(HttpStatusCode.Forbidden, getTablesRes.StatusCode);

        // Owner A attempts to get Owner B's table by ID
        var getTableByIdRes = await _client.GetAsync($"/api/canteens/{canteenIdB}/tables/{tableB.TableId}");
        Assert.Equal(HttpStatusCode.Forbidden, getTableByIdRes.StatusCode);

        // Owner A attempts to get Owner B's table QR image
        var getQrImageRes = await _client.GetAsync($"/api/canteens/{canteenIdB}/tables/{tableB.TableId}/qr-image");
        Assert.Equal(HttpStatusCode.Forbidden, getQrImageRes.StatusCode);

        // Owner A attempts to get Owner B's table QR URL
        var getQrUrlRes = await _client.GetAsync($"/api/canteens/{canteenIdB}/tables/{tableB.TableId}/qr-url");
        Assert.Equal(HttpStatusCode.Forbidden, getQrUrlRes.StatusCode);
    }

    // ─── 4. Orders and Payments Cross-Tenant Isolation ──────────────────────
    [Fact]
    public async Task OwnerA_Cannot_Access_OwnerB_Orders_Or_Payments_Returns403()
    {
        var (tokenA, _, canteenIdA) = await RegisterOwnerAndCanteenAsync("owner_a_ord", "Canteen A");
        var (tokenB, _, canteenIdB) = await RegisterOwnerAndCanteenAsync("owner_b_ord", "Canteen B");

        // Switch to Owner A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Owner A attempts to list Owner B's orders
        var listRes = await _client.GetAsync($"/api/canteens/{canteenIdB}/orders");
        Assert.Equal(HttpStatusCode.Forbidden, listRes.StatusCode);

        // Owner A attempts to view order #999 in Canteen B
        var viewRes = await _client.GetAsync($"/api/canteens/{canteenIdB}/orders/999");
        Assert.Equal(HttpStatusCode.Forbidden, viewRes.StatusCode);

        // Owner A attempts to update order status in Canteen B
        var statusRes = await _client.PatchAsJsonAsync($"/api/canteens/{canteenIdB}/orders/999/status", new UpdateOrderStatusDto
        {
            Status = "Completed"
        });
        Assert.Equal(HttpStatusCode.Forbidden, statusRes.StatusCode);

        // Owner A attempts to inspect payment in Canteen B
        var payRes = await _client.GetAsync($"/api/canteens/{canteenIdB}/orders/999/payment");
        Assert.Equal(HttpStatusCode.Forbidden, payRes.StatusCode);
    }

    // ─── 5. Cross-Canteen Product/Category Reference Rejected ───────────────
    [Fact]
    public async Task CrossCanteen_ProductCategory_Reference_Is_Rejected_400BadRequest()
    {
        var (tokenA, _, canteenIdA) = await RegisterOwnerAndCanteenAsync("owner_a_xref", "Canteen A");
        var (tokenB, _, canteenIdB) = await RegisterOwnerAndCanteenAsync("owner_b_xref", "Canteen B");

        // Owner A creates Category in Canteen A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var catResA = await _client.PostAsJsonAsync($"/api/canteens/{canteenIdA}/categories", new CreateCategoryDto
        {
            Name = "Category in Canteen A"
        });
        var catA = await catResA.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(catA);

        // Owner B attempts to create product in Canteen B referencing Category in Canteen A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var prodRes = await _client.PostAsJsonAsync($"/api/products?canteenId={canteenIdB}", new CreateProductDto
        {
            CategoryId = catA.CategoryId, // Belongs to Canteen A
            Name = "Stolen Category Product",
            Price = 50.00m,
            StockQuantity = 10
        });

        Assert.Equal(HttpStatusCode.BadRequest, prodRes.StatusCode);
    }

    // ─── 6. Canteen Switching Security ──────────────────────────────────────
    [Fact]
    public async Task Owner_Cannot_Switch_To_Unowned_Or_Suspended_Canteen()
    {
        var (tokenA, ownerIdA, canteenIdA) = await RegisterOwnerAndCanteenAsync("switch_sec_a", "Canteen A");
        var (tokenB, ownerIdB, canteenIdB) = await RegisterOwnerAndCanteenAsync("switch_sec_b", "Canteen B");

        // 1. Owner A attempts to switch to Canteen B (which they do not own)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var switchUnownedRes = await _client.PostAsync($"/api/auth/switch-canteen/{canteenIdB}", null);
        Assert.Equal(HttpStatusCode.Forbidden, switchUnownedRes.StatusCode);

        // 2. Owner A creates a second canteen (Canteen A2)
        var createCanteen2Res = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Canteen A2",
            Location = "South Gate"
        });
        var canteenA2 = await createCanteen2Res.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(canteenA2);

        // 3. Platform Admin suspends Canteen A2
        var adminToken = await CreatePlatformAdminTokenAsync("admin_switch_susp");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var suspendRes = await _client.PatchAsJsonAsync($"/api/admin/canteens/{canteenA2.CanteenId}/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Suspended
        });
        Assert.Equal(HttpStatusCode.OK, suspendRes.StatusCode);

        // 4. Owner A tries switching to suspended Canteen A2 -> 403 Forbidden
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var switchSuspendedRes = await _client.PostAsync($"/api/auth/switch-canteen/{canteenA2.CanteenId}", null);
        Assert.Equal(HttpStatusCode.Forbidden, switchSuspendedRes.StatusCode);
    }

    // ─── 7. Suspended Tenant Blocked From Operations With Valid JWT ─────────
    [Fact]
    public async Task SuspendedTenant_Blocked_From_Operations_Even_With_Previously_Issued_JWT()
    {
        // 1. Owner creates canteen and holds a valid token with CanteenId claim
        var (token, _, canteenId) = await RegisterOwnerAndCanteenAsync("susp_op_owner", "Suspended Operations Canteen");

        // 2. Platform Admin suspends the tenant
        var adminToken = await CreatePlatformAdminTokenAsync("admin_susp_op");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var suspendRes = await _client.PatchAsJsonAsync($"/api/admin/canteens/{canteenId}/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Suspended
        });
        Assert.Equal(HttpStatusCode.OK, suspendRes.StatusCode);

        // 3. Owner attempts operational actions using their previously issued (unexpired) JWT
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Attempt category creation -> 403 Forbidden
        var catRes = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/categories", new CreateCategoryDto { Name = "Category" });
        Assert.Equal(HttpStatusCode.Forbidden, catRes.StatusCode);

        // Attempt table creation -> 403 Forbidden
        var tblRes = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/tables", new CreateCanteenTableDto { TableNumber = 50, Capacity = 4 });
        Assert.Equal(HttpStatusCode.Forbidden, tblRes.StatusCode);

        // Attempt product creation -> 403 Forbidden
        var prodRes = await _client.PostAsJsonAsync($"/api/products?canteenId={canteenId}", new CreateProductDto { CategoryId = 1, Name = "Item", Price = 10, StockQuantity = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, prodRes.StatusCode);

        // Attempt canteen update -> 403 Forbidden
        var updateRes = await _client.PutAsJsonAsync($"/api/canteens/{canteenId}", new UpdateCanteenDto { Name = "Renamed", Location = "Renamed" });
        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);
    }

    // ─── 8. Customer Cannot Place Order on Suspended Canteen ────────────────
    [Fact]
    public async Task Customer_Cannot_Place_Order_On_Suspended_Canteen()
    {
        // 1. Owner creates canteen, category, table, and product
        var (token, _, canteenId) = await RegisterOwnerAndCanteenAsync("cust_susp_order", "Order Suspended Canteen");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var catRes = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/categories", new CreateCategoryDto { Name = "Meals" });
        var cat = await catRes.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(cat);

        var tableRes = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/tables", new CreateCanteenTableDto { TableNumber = 1, Capacity = 2 });
        var table = await tableRes.Content.ReadFromJsonAsync<CanteenTableDto>(JsonOptions);
        Assert.NotNull(table);

        var prodRes = await _client.PostAsJsonAsync($"/api/products?canteenId={canteenId}", new CreateProductDto
        {
            CategoryId = cat.CategoryId,
            Name = "Thali",
            Price = 100.00m,
            StockQuantity = 10
        });
        var prod = await prodRes.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        Assert.NotNull(prod);

        // 2. Platform Admin suspends the canteen
        var adminToken = await CreatePlatformAdminTokenAsync("admin_cust_susp");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PatchAsJsonAsync($"/api/admin/canteens/{canteenId}/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Suspended
        });

        // 3. Customer attempts to place an order via table QR token
        _client.DefaultRequestHeaders.Authorization = null; // Anonymous customer
        var orderDto = new CreateOrderDto
        {
            TableId = table.TableId,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = prod.ProductId, Quantity = 1 }
            }
        };

        var orderRes = await _client.PostAsJsonAsync($"/api/orders?qrToken={table.QRToken}", orderDto);
        Assert.Equal(HttpStatusCode.BadRequest, orderRes.StatusCode);
    }

    // ─── 9. Public Menus for Suspended Canteen Return 404 ───────────────────
    [Fact]
    public async Task Public_Menu_Endpoints_For_Suspended_Canteen_Return_404NotFound()
    {
        var (token, _, canteenId) = await RegisterOwnerAndCanteenAsync("pub_susp_menu", "Suspended Menu Canteen");

        // Suspend the canteen
        var adminToken = await CreatePlatformAdminTokenAsync("admin_menu_susp");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PatchAsJsonAsync($"/api/admin/canteens/{canteenId}/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Suspended
        });

        // Anonymous public queries
        _client.DefaultRequestHeaders.Authorization = null;

        var pubInfo = await _client.GetAsync($"/api/canteens/{canteenId}/public-info");
        Assert.Equal(HttpStatusCode.NotFound, pubInfo.StatusCode);

        var categories = await _client.GetAsync($"/api/canteens/{canteenId}/categories");
        Assert.Equal(HttpStatusCode.NotFound, categories.StatusCode);

        var products = await _client.GetAsync($"/api/products/canteen/{canteenId}");
        Assert.Equal(HttpStatusCode.NotFound, products.StatusCode);

        var tables = await _client.GetAsync($"/api/canteens/{canteenId}/tables");
        Assert.Equal(HttpStatusCode.NotFound, tables.StatusCode);
    }

    // ─── 10. Registration Enforces Unique Email and CanteenOwner Role ────────
    [Fact]
    public async Task Registration_Enforces_Unique_Email_And_CanteenOwner_Role()
    {
        var email = $"reg_test_{Guid.NewGuid():N}@test.com";
        var regDto = new RegisterOwnerDto
        {
            FullName = "New Owner",
            Email = email,
            Password = "SecurePassword123!"
        };

        var res = await _client.PostAsJsonAsync("/api/auth/register", regDto);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var authData = await res.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(authData);
        Assert.Equal("CanteenOwner", authData.Role);
        Assert.Null(authData.CanteenId);

        // Attempt duplicate registration with same email
        var duplicateRes = await _client.PostAsJsonAsync("/api/auth/register", regDto);
        Assert.Equal(HttpStatusCode.Conflict, duplicateRes.StatusCode);
    }

    // ─── 11. Registration With Initial Canteen Details ───────────────────────
    [Fact]
    public async Task Registration_With_Initial_Canteen_Sets_Up_Tenant_And_Active_Context()
    {
        var email = $"reg_canteen_{Guid.NewGuid():N}@test.com";
        var regDto = new RegisterOwnerDto
        {
            FullName = "Quick Owner",
            Email = email,
            Password = "SecurePassword123!",
            CanteenName = "Quick Campus Cafe",
            ContactPhone = "9876543210",
            CanteenAddress = "Academic Block A, Pune"
        };

        var res = await _client.PostAsJsonAsync("/api/auth/register", regDto);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var authData = await res.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(authData);
        Assert.NotNull(authData.CanteenId);

        // Verify canteen details
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.Token);
        var canteenRes = await _client.GetAsync($"/api/canteens/{authData.CanteenId}");
        canteenRes.EnsureSuccessStatusCode();
        var canteen = await canteenRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(canteen);
        Assert.Equal("Quick Campus Cafe", canteen.Name);
        Assert.False(string.IsNullOrWhiteSpace(canteen.Slug));
        Assert.Equal(TenantStatus.Active, canteen.TenantStatus);
        Assert.Equal(SetupStatus.Draft, canteen.SetupStatus);
    }

    // ─── 12. Canteen Generates Unique Slug And Supports Public Browsing ──────
    [Fact]
    public async Task Canteen_Generates_Unique_Slug_And_Supports_Public_Browsing_By_Slug()
    {
        var (token, _, canteenId) = await RegisterOwnerAndCanteenAsync("slug_owner", "Royal Engineering Mess");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var canteenRes = await _client.GetAsync($"/api/canteens/{canteenId}");
        var canteen = await canteenRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(canteen);
        Assert.False(string.IsNullOrWhiteSpace(canteen.Slug));

        // Public customer access via by-slug
        _client.DefaultRequestHeaders.Authorization = null;
        var pubRes = await _client.GetAsync($"/api/canteens/by-slug/{canteen.Slug}/public-info");
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);
        var pubInfo = await pubRes.Content.ReadFromJsonAsync<PublicCanteenDto>(JsonOptions);
        Assert.NotNull(pubInfo);
        Assert.Equal("Royal Engineering Mess", pubInfo.Name);
        Assert.Equal(canteen.Slug, pubInfo.Slug);
    }

    // ─── 13. Bank Details Are Saved And Isolated From Unauthorized Users ─────
    [Fact]
    public async Task Bank_Details_Are_Saved_And_Isolated_From_Unauthorized_Users()
    {
        var (token1, _, canteen1Id) = await RegisterOwnerAndCanteenAsync("bank_owner1", "Bank Secured Canteen");
        var (token2, _, _) = await RegisterOwnerAndCanteenAsync("bank_owner2", "Other Canteen");

        // Owner 1 updates bank details
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var updateDto = new UpdateCanteenDto
        {
            Name = "Bank Secured Canteen",
            Location = "Campus West",
            BankAccountHolder = "Owner One",
            BankName = "State Bank of India",
            AccountNumber = "123456789012",
            IfscCode = "SBIN0001234",
            AccountType = "Current Account",
            UpiId = "owner1@sbi",
            IsActive = true
        };
        var updateRes = await _client.PutAsJsonAsync($"/api/canteens/{canteen1Id}", updateDto);
        updateRes.EnsureSuccessStatusCode();

        // 1. Owner 1 reads their own canteen: bank details present
        var owner1View = await (await _client.GetAsync($"/api/canteens/{canteen1Id}")).Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(owner1View);
        Assert.Equal("Owner One", owner1View.BankAccountHolder);
        Assert.Equal("123456789012", owner1View.AccountNumber);
        Assert.Equal("SBIN0001234", owner1View.IfscCode);

        // 2. Owner 2 reads Canteen 1: bank details must be masked/null
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var owner2View = await (await _client.GetAsync($"/api/canteens/{canteen1Id}")).Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(owner2View);
        Assert.Null(owner2View.BankAccountHolder);
        Assert.Null(owner2View.AccountNumber);
        Assert.Null(owner2View.IfscCode);

        // 3. Anonymous public customer: public-info does not contain bank fields
        _client.DefaultRequestHeaders.Authorization = null;
        var pubRes = await _client.GetAsync($"/api/canteens/{canteen1Id}/public-info");
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);
        var pubJson = await pubRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("123456789012", pubJson);
        Assert.DoesNotContain("SBIN0001234", pubJson);
    }

    // ─── 14. Owner Cannot Tamper With TenantStatus In Profile Update ─────────
    [Fact]
    public async Task Owner_Cannot_Tamper_With_TenantStatus_In_Profile_Update()
    {
        var (token, _, canteenId) = await RegisterOwnerAndCanteenAsync("tamper_owner", "Tamper Test Canteen");

        // Platform admin suspends the canteen
        var adminToken = await CreatePlatformAdminTokenAsync("admin_tamper");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PatchAsJsonAsync($"/api/admin/canteens/{canteenId}/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Suspended
        });

        // Owner attempts to update canteen (or reinstate it)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.PutAsJsonAsync($"/api/canteens/{canteenId}", new UpdateCanteenDto
        {
            Name = "Reactivated Canteen",
            Location = "New Location",
            IsActive = true
        });

        // Forbidden because tenant is suspended
        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);

        // Check canteen status remains Suspended
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var canteen = await (await _client.GetAsync($"/api/canteens/{canteenId}")).Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(canteen);
        Assert.Equal(TenantStatus.Suspended, canteen.TenantStatus);
    }
}

