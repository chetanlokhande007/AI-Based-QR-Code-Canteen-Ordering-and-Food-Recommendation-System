using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Canteen.Tests;

public class AdminEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AdminEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, int OwnerId)> RegisterOwnerAsync(string emailPrefix)
    {
        var email = $"{emailPrefix}_{Guid.NewGuid():N}@test.com";
        var regDto = new RegisterOwnerDto
        {
            FullName = "Standard Canteen Owner",
            Email = email,
            Password = "SecurePassword123!"
        };

        var res = await _client.PostAsJsonAsync("/api/auth/register", regDto);
        res.EnsureSuccessStatusCode();
        var data = await res.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(data);
        return (data.Token, data.OwnerId);
    }

    private async Task<string> CreatePlatformAdminTokenAsync(string emailPrefix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

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

        var token = await authService.GenerateTokenForOwnerAsync(admin.OwnerId);
        Assert.NotNull(token);
        return token;
    }

    // ─── 1. Cross-Tenant Protection on GET /api/canteens ──────────────────────
    [Fact]
    public async Task GetAllCanteens_OwnerRole_Returns403Forbidden()
    {
        var (ownerToken, _) = await RegisterOwnerAsync("owner_deny");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await _client.GetAsync("/api/canteens");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCanteens_Anonymous_Returns401Unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/canteens");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCanteens_PlatformAdmin_Returns200OK()
    {
        var adminToken = await CreatePlatformAdminTokenAsync("admin_allow");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/canteens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── 2. Dedicated Platform Admin Metrics API ──────────────────────────────
    [Fact]
    public async Task AdminMetrics_Anonymous_Returns401Unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/admin/metrics");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminMetrics_OwnerRole_Returns403Forbidden()
    {
        var (ownerToken, _) = await RegisterOwnerAsync("owner_metrics");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await _client.GetAsync("/api/admin/metrics");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminMetrics_PlatformAdmin_Returns200WithRealStats()
    {
        var adminToken = await CreatePlatformAdminTokenAsync("admin_metrics");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/admin/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var metrics = await response.Content.ReadFromJsonAsync<PlatformAdminMetricsDto>(JsonOptions);
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalOwners >= 1);
    }

    // ─── 3. Platform Admin Canteens API ───────────────────────────────────────
    [Fact]
    public async Task AdminCanteens_OwnerRole_Returns403Forbidden()
    {
        var (ownerToken, _) = await RegisterOwnerAsync("owner_canteens");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await _client.GetAsync("/api/admin/canteens");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanteens_PlatformAdmin_Returns200WithCanteens()
    {
        // 1. Register an owner and a canteen
        var (ownerToken, _) = await RegisterOwnerAsync("owner_for_admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var createRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Platform Test Canteen",
            Location = "Campus Block A",
            AddressLine1 = "Ground Floor"
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(created);

        // 2. Query as Platform Admin
        var adminToken = await CreatePlatformAdminTokenAsync("admin_list");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var adminRes = await _client.GetAsync("/api/admin/canteens");
        Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);

        var canteens = await adminRes.Content.ReadFromJsonAsync<List<AdminCanteenItemDto>>(JsonOptions);
        Assert.NotNull(canteens);
        Assert.Contains(canteens, c => c.CanteenId == created.CanteenId && c.Name == "Platform Test Canteen");
    }

    // ─── 4. Activate & Suspend Tenant ─────────────────────────────────────────
    [Fact]
    public async Task UpdateCanteenStatus_OwnerRole_Returns403Forbidden()
    {
        var (ownerToken, _) = await RegisterOwnerAsync("owner_status_deny");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await _client.PatchAsJsonAsync("/api/admin/canteens/1/status", new UpdateCanteenStatusDto { IsActive = false });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCanteenStatus_PlatformAdmin_CanSuspendAndReactivate()
    {
        // 1. Create canteen
        var (ownerToken, _) = await RegisterOwnerAsync("owner_suspend");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var createRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Suspension Candidate",
            Location = "West Gate"
        });
        createRes.EnsureSuccessStatusCode();
        var created = await createRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(created);

        // 2. Platform Admin suspends the canteen
        var adminToken = await CreatePlatformAdminTokenAsync("admin_suspend");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var suspendRes = await _client.PatchAsJsonAsync($"/api/admin/canteens/{created.CanteenId}/status", new UpdateCanteenStatusDto { IsActive = false });
        Assert.Equal(HttpStatusCode.OK, suspendRes.StatusCode);

        // Verify public info returns 404 because canteen is inactive
        var pubRes = await _client.GetAsync($"/api/canteens/{created.CanteenId}/public-info");
        Assert.Equal(HttpStatusCode.NotFound, pubRes.StatusCode);

        // 3. Platform Admin reactivates the canteen
        var reactivateRes = await _client.PatchAsJsonAsync($"/api/admin/canteens/{created.CanteenId}/status", new UpdateCanteenStatusDto { IsActive = true });
        Assert.Equal(HttpStatusCode.OK, reactivateRes.StatusCode);

        var pubRes2 = await _client.GetAsync($"/api/canteens/{created.CanteenId}/public-info");
        Assert.Equal(HttpStatusCode.OK, pubRes2.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_Can_View_Any_Canteen_Tables_And_QrImage_Returns200()
    {
        // 1. Create owner and canteen with table
        var (ownerToken, _) = await RegisterOwnerAsync("admin_tbl_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var createRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Admin Inspectable Canteen",
            Location = "Block A"
        });
        createRes.EnsureSuccessStatusCode();
        var canteen = await createRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(canteen);

        // Switch canteen to get token with active CanteenId claim
        var switchRes = await _client.PostAsync($"/api/auth/switch-canteen/{canteen.CanteenId}", null);
        switchRes.EnsureSuccessStatusCode();
        var switchData = await switchRes.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", switchData!.Token);

        var tableRes = await _client.PostAsJsonAsync($"/api/canteens/{canteen.CanteenId}/tables", new CreateCanteenTableDto
        {
            TableNumber = 501,
            Capacity = 6
        });
        tableRes.EnsureSuccessStatusCode();
        var table = await tableRes.Content.ReadFromJsonAsync<CanteenTableDto>(JsonOptions);
        Assert.NotNull(table);

        // 2. Platform Admin inspects the tables
        var adminToken = await CreatePlatformAdminTokenAsync("admin_table_inspector");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var adminTablesRes = await _client.GetAsync($"/api/admin/canteens/{canteen.CanteenId}/tables");
        Assert.Equal(HttpStatusCode.OK, adminTablesRes.StatusCode);
        var tables = await adminTablesRes.Content.ReadFromJsonAsync<List<CanteenTableDto>>(JsonOptions);
        Assert.NotNull(tables);
        Assert.Single(tables);
        Assert.Equal(501, tables[0].TableNumber);
        Assert.Equal(table.QRToken, tables[0].QRToken); // Preserves exact physical QR token

        // 3. Platform Admin inspects QR image
        var qrImgRes = await _client.GetAsync($"/api/admin/canteens/{canteen.CanteenId}/tables/{table.TableId}/qr-image");
        Assert.Equal(HttpStatusCode.OK, qrImgRes.StatusCode);
        Assert.Equal("image/png", qrImgRes.Content.Headers.ContentType?.MediaType);

        // 4. Platform Admin inspects QR URL
        var qrUrlRes = await _client.GetAsync($"/api/admin/canteens/{canteen.CanteenId}/tables/{table.TableId}/qr-url");
        Assert.Equal(HttpStatusCode.OK, qrUrlRes.StatusCode);
    }

    [Fact]
    public async Task RegularOwner_Cannot_Access_Admin_Tables_Returns403()
    {
        var (ownerToken, _) = await RegisterOwnerAsync("owner_not_admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var adminTablesRes = await _client.GetAsync("/api/admin/canteens/1/tables");
        Assert.Equal(HttpStatusCode.Forbidden, adminTablesRes.StatusCode);
    }

    [Fact]
    public async Task Admin_Can_Access_CanteenDetails_Payments_AuditLogs_And_Owners()
    {
        // 1. Arrange a canteen
        var (ownerToken, ownerId) = await RegisterOwnerAsync("admin_suite_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createCanteenRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Suite Canteen",
            Location = "Tech Park, Pune",
            AddressLine1 = "Tower B Ground Floor",
            City = "Pune",
            BankAccountHolder = "Suite Owner",
            BankName = "HDFC Bank",
            AccountNumber = "123456789012",
            IfscCode = "HDFC0001234"
        });
        createCanteenRes.EnsureSuccessStatusCode();
        var canteen = await createCanteenRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(canteen);

        // 2. Platform Admin accesses Canteen Details
        var adminToken = await CreatePlatformAdminTokenAsync("admin_suite_tester");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var detailsRes = await _client.GetAsync($"/api/admin/canteens/{canteen.CanteenId}/details");
        Assert.Equal(HttpStatusCode.OK, detailsRes.StatusCode);
        var details = await detailsRes.Content.ReadFromJsonAsync<AdminCanteenDetailsDto>(JsonOptions);
        Assert.NotNull(details);
        Assert.Equal("Suite Canteen", details.Name);
        Assert.True(details.BankDetails.IsConfigured);
        Assert.Contains("9012", details.BankDetails.AccountNumberMasked);

        // 3. Platform Admin accesses Payment Overview and Setup
        var payOverviewRes = await _client.GetAsync("/api/admin/payments/overview");
        Assert.Equal(HttpStatusCode.OK, payOverviewRes.StatusCode);

        var paySetupRes = await _client.GetAsync("/api/admin/payments/canteens-setup");
        Assert.Equal(HttpStatusCode.OK, paySetupRes.StatusCode);
        var setups = await paySetupRes.Content.ReadFromJsonAsync<List<AdminCanteenPaymentSetupDto>>(JsonOptions);
        Assert.NotNull(setups);
        Assert.Contains(setups, s => s.CanteenId == canteen.CanteenId && s.IsConfigured);

        // 4. Platform Admin accesses Audit Logs
        var auditRes = await _client.GetAsync("/api/admin/audit-logs");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
        var logs = await auditRes.Content.ReadFromJsonAsync<List<AdminAuditLogDto>>(JsonOptions);
        Assert.NotNull(logs);
        Assert.NotEmpty(logs);

        // 5. Platform Admin accesses Owners List and Owner Details
        var ownersRes = await _client.GetAsync("/api/admin/owners");
        Assert.Equal(HttpStatusCode.OK, ownersRes.StatusCode);
        var owners = await ownersRes.Content.ReadFromJsonAsync<List<AdminOwnerItemDto>>(JsonOptions);
        Assert.NotNull(owners);
        Assert.Contains(owners, o => o.OwnerId == ownerId);

        var ownerDetailsRes = await _client.GetAsync($"/api/admin/owners/{ownerId}");
        Assert.Equal(HttpStatusCode.OK, ownerDetailsRes.StatusCode);
        var ownerDetails = await ownerDetailsRes.Content.ReadFromJsonAsync<AdminOwnerDetailsDto>(JsonOptions);
        Assert.NotNull(ownerDetails);
        Assert.Equal(ownerId, ownerDetails.OwnerId);
        Assert.Contains(ownerDetails.Canteens, c => c.CanteenId == canteen.CanteenId);
    }
}
