using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Canteen.Model.DTOs;
using Xunit;

namespace Canteen.Tests;

public class MultiCanteenTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MultiCanteenTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, int OwnerId)> RegisterOwnerAsync(string emailPrefix)
    {
        var email = $"{emailPrefix}_{Guid.NewGuid():N}@example.com";
        var regDto = new RegisterOwnerDto
        {
            FullName = "Multi Canteen Owner",
            Email = email,
            Password = "SecurePassword123!"
        };

        var regRes = await _client.PostAsJsonAsync("/api/auth/register", regDto);
        regRes.EnsureSuccessStatusCode();
        var regData = await regRes.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(regData);
        Assert.NotNull(regData.Token);
        return (regData.Token, regData.OwnerId);
    }

    [Fact]
    public async Task Owner_Can_Register_Multiple_Canteens_Successfully()
    {
        // Arrange
        var (token, _) = await RegisterOwnerAsync("multitest1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act 1: Register First Canteen (Canteen A)
        var canteenADto = new CreateCanteenDto
        {
            Name = "Katti Batti Cafe",
            Location = "Jalgaon, Maharashtra",
            AddressLine1 = "Campus Main Gate",
            City = "Jalgaon",
            State = "Maharashtra",
            PostalCode = "425001",
            Country = "India"
        };
        var resA = await _client.PostAsJsonAsync("/api/canteens", canteenADto);
        Assert.Equal(HttpStatusCode.Created, resA.StatusCode);
        var createdA = await resA.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(createdA);
        Assert.True(createdA.CanteenId > 0);

        // Act 2: Register Second Canteen (Canteen B) without overwriting or failing
        var canteenBDto = new CreateCanteenDto
        {
            Name = "Campus Food Court",
            Location = "Pune, Maharashtra",
            AddressLine1 = "Block C Food Court",
            City = "Pune",
            State = "Maharashtra",
            PostalCode = "411001",
            Country = "India"
        };
        var resB = await _client.PostAsJsonAsync("/api/canteens", canteenBDto);
        Assert.Equal(HttpStatusCode.Created, resB.StatusCode);
        var createdB = await resB.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);
        Assert.NotNull(createdB);
        Assert.True(createdB.CanteenId > 0);
        Assert.NotEqual(createdA.CanteenId, createdB.CanteenId);

        // Act 3: Query My Canteens
        var myRes = await _client.GetAsync("/api/canteens/my");
        Assert.Equal(HttpStatusCode.OK, myRes.StatusCode);
        var canteens = await myRes.Content.ReadFromJsonAsync<List<CanteenSummaryDto>>(JsonOptions);
        Assert.NotNull(canteens);
        Assert.Contains(canteens, c => c.CanteenId == createdA.CanteenId && c.Name == "Katti Batti Cafe");
        Assert.Contains(canteens, c => c.CanteenId == createdB.CanteenId && c.Name == "Campus Food Court");
    }

    [Fact]
    public async Task SwitchCanteen_Updates_Context_And_Allows_Access_Only_To_Authorized_Canteen()
    {
        // Arrange: Owner registers two canteens
        var (token, _) = await RegisterOwnerAsync("switchtest");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resA = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Canteen Alpha",
            Location = "Alpha Block",
            AddressLine1 = "Alpha Wing",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = "400001",
            Country = "India"
        });
        var canteenA = await resA.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);

        var resB = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Canteen Beta",
            Location = "Beta Block",
            AddressLine1 = "Beta Wing",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = "400002",
            Country = "India"
        });
        var canteenB = await resB.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);

        // Act: Switch context back to Canteen A
        var switchRes = await _client.PostAsync($"/api/auth/switch-canteen/{canteenA!.CanteenId}", null);
        Assert.Equal(HttpStatusCode.OK, switchRes.StatusCode);
        var switchData = await switchRes.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(switchData);
        Assert.Equal(canteenA.CanteenId, switchData.CanteenId);
        Assert.NotNull(switchData.Token);

        // Verify: New token can perform operations on Canteen A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", switchData.Token);
        var catRes = await _client.PostAsJsonAsync($"/api/canteens/{canteenA.CanteenId}/categories", new CreateCategoryDto
        {
            Name = "Alpha Snacks",
            Description = "Snacks for Alpha"
        });
        Assert.Equal(HttpStatusCode.Created, catRes.StatusCode);
    }

    [Fact]
    public async Task Cross_Tenant_Security_Owner_Cannot_Switch_Or_Access_Another_Owners_Canteen()
    {
        // Arrange: Two distinct owners
        var (tokenOwner1, _) = await RegisterOwnerAsync("owner1");
        var (tokenOwner2, _) = await RegisterOwnerAsync("owner2");

        // Owner 1 creates Canteen 1
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwner1);
        var res1 = await _client.PostAsJsonAsync("/api/canteens", new CreateCanteenDto
        {
            Name = "Owner1 Secret Canteen",
            Location = "Private Area",
            AddressLine1 = "Vault 1",
            City = "Pune",
            State = "Maharashtra",
            PostalCode = "411001",
            Country = "India"
        });
        var canteen1 = await res1.Content.ReadFromJsonAsync<CanteenDto>(JsonOptions);

        // Act & Assert 1: Owner 2 tries to switch to Canteen 1 -> 403 Forbidden
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwner2);
        var switchAttempt = await _client.PostAsync($"/api/auth/switch-canteen/{canteen1!.CanteenId}", null);
        Assert.Equal(HttpStatusCode.Forbidden, switchAttempt.StatusCode);

        // Act & Assert 2: Owner 2 tries to create category in Canteen 1 -> 403 Forbidden
        var catAttempt = await _client.PostAsJsonAsync($"/api/canteens/{canteen1.CanteenId}/categories", new CreateCategoryDto
        {
            Name = "Hacked Category"
        });
        Assert.Equal(HttpStatusCode.Forbidden, catAttempt.StatusCode);

        // Act & Assert 3: Owner 2 tries to update Canteen 1 -> 403 Forbidden
        var updateAttempt = await _client.PutAsJsonAsync($"/api/canteens/{canteen1.CanteenId}", new UpdateCanteenDto
        {
            Name = "Defaced Canteen",
            Location = "Defaced Location",
            IsActive = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateAttempt.StatusCode);
    }
}
