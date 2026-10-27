using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.Model.DTOs;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Canteen.Tests;

public class QrCodeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public QrCodeTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void GenerateTableMenuUrl_ReturnsProperlyFormattedUrl()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Frontend:BaseMenuUrl", "https://canteen.example.com/order-menu" }
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var service = new QrCodeService(config);

        // Act
        var url = service.GenerateTableMenuUrl(12, 34, "abc-token-123");

        // Assert
        Assert.Equal("https://canteen.example.com/order-menu?canteenId=12&tableId=34&token=abc-token-123", url);
    }

    [Fact]
    public void GenerateQrCodePng_ReturnsValidPngHeaderBytes()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var service = new QrCodeService(config);

        // Act
        var pngBytes = service.GenerateQrCodePng("https://canteen.example.com/order-menu?token=test");

        // Assert
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 64);

        // Standard PNG Magic Number Header: 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        for (int i = 0; i < pngHeader.Length; i++)
        {
            Assert.Equal(pngHeader[i], pngBytes[i]);
        }
    }

    [Fact]
    public void GenerateQrCodePng_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var service = new QrCodeService(config);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.GenerateQrCodePng(""));
    }

    [Fact]
    public async Task GetQrImage_And_GetQrUrl_ReturnSuccessAndValidPayloads()
    {
        // 1. Register owner & create canteen
        var (token, canteenId) = await CreateOwnerAndCanteenAsync("qr_owner_1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Create a table
        var tableResponse = await _client.PostAsJsonAsync($"/api/canteens/{canteenId}/tables", new
        {
            TableNumber = 101,
            Capacity = 4
        });
        Assert.Equal(HttpStatusCode.Created, tableResponse.StatusCode);
        var tableDto = await tableResponse.Content.ReadFromJsonAsync<CanteenTableDto>();
        Assert.NotNull(tableDto);

        // 3. Act: Request QR URL endpoint
        var urlResponse = await _client.GetAsync($"/api/canteens/{canteenId}/tables/{tableDto.TableId}/qr-url");
        Assert.Equal(HttpStatusCode.OK, urlResponse.StatusCode);
        var urlJson = await urlResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(urlJson.TryGetProperty("menuUrl", out var menuUrlProp));
        Assert.Contains($"canteenId={canteenId}", menuUrlProp.GetString());
        Assert.Contains($"tableId={tableDto.TableId}", menuUrlProp.GetString());

        // 4. Act: Request QR PNG image
        var imageResponse = await _client.GetAsync($"/api/canteens/{canteenId}/tables/{tableDto.TableId}/qr-image");
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.Equal("image/png", imageResponse.Content.Headers.ContentType?.MediaType);

        var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        for (int i = 0; i < pngHeader.Length; i++)
        {
            Assert.Equal(pngHeader[i], imageBytes[i]);
        }

        // 5. Act: Regenerate QR Token
        var regenResponse = await _client.PostAsync($"/api/canteens/{canteenId}/tables/{tableDto.TableId}/regenerate-qr", null);
        Assert.Equal(HttpStatusCode.OK, regenResponse.StatusCode);
        var regenDto = await regenResponse.Content.ReadFromJsonAsync<CanteenTableDto>();
        Assert.NotNull(regenDto);
        Assert.NotEqual(tableDto.QRToken, regenDto.QRToken);
    }

    private async Task<(string Token, int CanteenId)> CreateOwnerAndCanteenAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@test.com";
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "QR Owner",
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
            Location = "123 Campus Lane"
        });
        canteenResponse.EnsureSuccessStatusCode();
        var canteen = await canteenResponse.Content.ReadFromJsonAsync<CanteenDto>();

        return (canteen!.Token ?? auth.Token, canteen.CanteenId);
    }
}
