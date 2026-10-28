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
}

