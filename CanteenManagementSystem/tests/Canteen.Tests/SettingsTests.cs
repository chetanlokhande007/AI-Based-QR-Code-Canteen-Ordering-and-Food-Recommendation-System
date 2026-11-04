using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Canteen.Tests;

public class SettingsTests
{
    private class FakeConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values = new()
        {
            ["Jwt:Key"] = "ThisIsASecretKeyForTestingCanteenHubAuthentication2026!!",
            ["Jwt:Issuer"] = "CanteenHub",
            ["Jwt:Audience"] = "CanteenHubAudience"
        };

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private CanteenDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new CanteenDbContext(options);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_ShouldSucceed()
    {
        var context = CreateInMemoryContext("SettingsDb_ChangePassword_Success");
        var ownerRepo = new OwnerRepository(context);
        var config = new FakeConfiguration();
        var canteenRepo = new CanteenRepository(context);
        var authService = new AuthService(ownerRepo, canteenRepo, config);

        var registered = await authService.RegisterAsync(new RegisterOwnerDto
        {
            FullName = "Settings Owner",
            Email = "settings.owner@example.com",
            Password = "InitialPassword123!"
        });

        // Act
        var result = await authService.ChangePasswordAsync(registered.OwnerId, new ChangePasswordRequestDto
        {
            CurrentPassword = "InitialPassword123!",
            NewPassword = "NewSecretPassword456!"
        });

        // Assert
        Assert.True(result);

        // Verify can login with new password
        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "settings.owner@example.com",
            Password = "NewSecretPassword456!"
        });

        Assert.NotNull(loginResult);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidCurrentPassword_ShouldReturnFalse()
    {
        var context = CreateInMemoryContext("SettingsDb_ChangePassword_Failure");
        var ownerRepo = new OwnerRepository(context);
        var config = new FakeConfiguration();
        var canteenRepo = new CanteenRepository(context);
        var authService = new AuthService(ownerRepo, canteenRepo, config);

        var registered = await authService.RegisterAsync(new RegisterOwnerDto
        {
            FullName = "Settings Owner 2",
            Email = "settings2@example.com",
            Password = "InitialPassword123!"
        });

        // Act
        var result = await authService.ChangePasswordAsync(registered.OwnerId, new ChangePasswordRequestDto
        {
            CurrentPassword = "WrongPassword!",
            NewPassword = "NewSecretPassword456!"
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCancellationPolicyAsync_WhenNoneExists_ShouldReturnSensibleDefaults()
    {
        var context = CreateInMemoryContext("SettingsDb_CP_Default");
        var canteenRepo = new CanteenRepository(context);
        var ownerRepo = new OwnerRepository(context);

        var canteenService = new CanteenService(canteenRepo, ownerRepo, null!, null!, null!);

        // Act
        var policy = await canteenService.GetCancellationPolicyAsync(999);

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(999, policy.CanteenId);
        Assert.True(policy.EnableCustomerCancellation);
        Assert.Equal(5, policy.CustomCutoffMinutes);
        Assert.NotEmpty(policy.PredefinedCancellationReasons);
        Assert.Contains("Item out of stock", policy.PredefinedCancellationReasons);
    }

    [Fact]
    public async Task UpdateCancellationPolicyAsync_ShouldPersistAndClampCutoff()
    {
        var context = CreateInMemoryContext("SettingsDb_CP_Update");
        var canteenRepo = new CanteenRepository(context);
        var ownerRepo = new OwnerRepository(context);

        var canteenService = new CanteenService(canteenRepo, ownerRepo, null!, null!, null!);

        // Act
        var updated = await canteenService.UpdateCancellationPolicyAsync(1, new UpdateCanteenCancellationPolicyDto
        {
            EnableCustomerCancellation = false,
            CustomCutoffMinutes = 50, // Should clamp to 15
            PredefinedCancellationReasons = new List<string> { "Closing early", "Kitchen busy" },
            CustomPolicyNotice = "Notice: cancellations are not allowed once order is accepted."
        });

        // Assert
        Assert.NotNull(updated);
        Assert.False(updated.EnableCustomerCancellation);
        Assert.Equal(15, updated.CustomCutoffMinutes); // Clamped to 15
        Assert.Equal(2, updated.PredefinedCancellationReasons.Count);
        Assert.Equal("Notice: cancellations are not allowed once order is accepted.", updated.CustomPolicyNotice);

        // Re-read to confirm persistence
        var fetched = await canteenService.GetCancellationPolicyAsync(1);
        Assert.False(fetched.EnableCustomerCancellation);
        Assert.Equal(15, fetched.CustomCutoffMinutes);
    }
}
