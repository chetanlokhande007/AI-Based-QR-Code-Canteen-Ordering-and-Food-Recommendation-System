using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Canteen.Model.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Canteen.Tests;

public class CanteenLifecycleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CanteenLifecycleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, int OwnerId)> RegisterOwnerAsync(string emailPrefix)
    {
        var email = $"{emailPrefix}_{Guid.NewGuid():N}@test.com";
        var regDto = new RegisterOwnerDto
        {
            FullName = "Lifecycle Test Owner",
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

    [Fact]
    public async Task NewCanteen_Has_Default_ActiveTenantStatus_And_DraftSetupStatus()
    {
        // Arrange
        var (token, _) = await RegisterOwnerAsync("new_canteen_status");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act: Create new canteen
        var createRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Lifecycle Canteen 1",
            Location = "North Wing Food Court"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(TenantStatus.Active, created.TenantStatus);
        Assert.Equal(SetupStatus.Draft, created.SetupStatus);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task GetMyCanteens_Returns_Correct_Lifecycle_And_Setup_Statuses()
    {
        // Arrange
        var (token, _) = await RegisterOwnerAsync("my_canteen_status");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Pending Setup Canteen",
            Location = "South Campus"
        });

        // Act: Query /api/canteens/my
        var myRes = await _client.GetAsync("/api/canteens/my");
        Assert.Equal(HttpStatusCode.OK, myRes.StatusCode);

        var canteens = await myRes.Content.ReadFromJsonAsync<List<CanteenSummaryDto>>(JsonOptions);
        Assert.NotNull(canteens);
        var canteen = canteens.FirstOrDefault(c => c.Name == "Pending Setup Canteen");
        Assert.NotNull(canteen);

        // Assert
        Assert.Equal(TenantStatus.Active, canteen.TenantStatus);
        Assert.Equal(SetupStatus.Draft, canteen.SetupStatus);
        Assert.False(canteen.IsSetupComplete);
        Assert.Equal("Setup Pending", canteen.Status);
    }

    [Fact]
    public async Task CanteenOwner_Cannot_Modify_TenantStatus_Via_UpdateEndpoint()
    {
        // Arrange
        var (token, _) = await RegisterOwnerAsync("owner_tamper_status");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Tamper Test Canteen",
            Location = "Central Hub"
        });
        var created = await createRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(created);

        // Act: Owner tries updating name and passing an unauthorized tenantStatus field in JSON
        var rawJson = "{\"name\":\"Tamper Test Canteen Updated\",\"location\":\"Central Hub Ground Floor\",\"isActive\":true,\"tenantStatus\":\"Suspended\"}";
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var updateRes = await _client.PutAsync($"/api/canteens/{created.CanteenId}", content);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(updated);
        // TenantStatus remains Active and cannot be modified by the owner
        Assert.Equal(TenantStatus.Active, updated.TenantStatus);
    }

    [Fact]
    public async Task PlatformAdmin_Can_Suspend_And_Reactivate_Tenant_Via_TenantStatus()
    {
        // Arrange: Owner creates canteen
        var (ownerToken, _) = await RegisterOwnerAsync("admin_lifecycle_test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var createRes = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "To Be Suspended Canteen",
            Location = "East Gate"
        });
        var created = await createRes.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(created);

        // Act 1: Platform Admin suspends tenant via TenantStatus enum
        var adminToken = await CreatePlatformAdminTokenAsync("admin_suspend_tenant");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var patchRes = await _client.PatchAsJsonAsync($"/api/admin/canteens/{created.CanteenId}/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Suspended
        });
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        // Assert 1: Public info returns 404 because tenant is suspended
        var pubRes = await _client.GetAsync($"/api/canteens/{created.CanteenId}/public-info");
        Assert.Equal(HttpStatusCode.NotFound, pubRes.StatusCode);

        // Assert 2: Admin list reflects Suspended status
        var adminListRes = await _client.GetAsync("/api/admin/canteens");
        Assert.Equal(HttpStatusCode.OK, adminListRes.StatusCode);
        var adminList = await adminListRes.Content.ReadFromJsonAsync<List<AdminCanteenItemDto>>(JsonOptions);
        Assert.NotNull(adminList);
        var item = adminList.FirstOrDefault(c => c.CanteenId == created.CanteenId);
        Assert.NotNull(item);
        Assert.Equal(TenantStatus.Suspended, item.TenantStatus);
        Assert.False(item.IsActive);

        // Act 2: Platform Admin reactivates tenant
        var reactivateRes = await _client.PatchAsJsonAsync($"/api/admin/canteens/{created.CanteenId}/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Active
        });
        Assert.Equal(HttpStatusCode.OK, reactivateRes.StatusCode);

        // Assert 3: Public info is now accessible again
        var pubRes2 = await _client.GetAsync($"/api/canteens/{created.CanteenId}/public-info");
        Assert.Equal(HttpStatusCode.OK, pubRes2.StatusCode);
    }

    [Fact]
    public async Task Owner_Cannot_Access_Admin_Status_Endpoint()
    {
        // Arrange
        var (ownerToken, _) = await RegisterOwnerAsync("unauthorized_owner_patch");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Act: Owner attempts to call admin status endpoint
        var patchRes = await _client.PatchAsJsonAsync("/api/admin/canteens/1/status", new UpdateCanteenStatusDto
        {
            TenantStatus = TenantStatus.Active
        });

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, patchRes.StatusCode);
    }
}
