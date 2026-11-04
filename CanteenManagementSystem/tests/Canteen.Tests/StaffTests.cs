using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Repositories;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Canteen.Tests;

public class StaffTests
{
    private CanteenDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(databaseName: "StaffDb_" + Guid.NewGuid().ToString())
            .Options;

        return new CanteenDbContext(options);
    }

    [Fact]
    public async Task GetSummaryMetricsAsync_ReturnsZero_WhenNoStaffExists()
    {
        using var context = CreateInMemoryContext();
        var staffRepo = new StaffRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var service = new StaffService(staffRepo, canteenRepo, NullLogger<StaffService>.Instance);

        var metrics = await service.GetSummaryMetricsAsync(1);

        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TotalStaff);
        Assert.Equal(0, metrics.ActiveStaff);
        Assert.Equal(0m, metrics.ActivePercentage);
        Assert.Equal(0, metrics.OnLeaveStaff);
        Assert.Equal(0, metrics.InactiveStaff);
    }

    [Fact]
    public async Task GetSummaryMetricsAsync_CalculatesCountsAndPercentagesAccurately()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 5;

        // 10 Active, 1 On Leave, 1 Inactive = 12 Total
        for (int i = 1; i <= 10; i++)
        {
            context.StaffMembers.Add(new StaffMember
            {
                StaffId = i,
                CanteenId = canteenId,
                EmployeeId = $"EMP{i:D3}",
                FullName = $"Staff Member {i}",
                MobileNumber = $"98765432{i:D2}",
                Role = "Kitchen Staff",
                Shift = "Morning (6:00 AM - 2:00 PM)",
                Status = "Active",
                JoinedOn = DateTime.UtcNow.AddDays(-10)
            });
        }

        context.StaffMembers.Add(new StaffMember
        {
            StaffId = 11,
            CanteenId = canteenId,
            EmployeeId = "EMP011",
            FullName = "Sneha Naik",
            MobileNumber = "9876543211",
            Role = "Order Manager",
            Shift = "Evening (2:00 PM - 10:00 PM)",
            Status = "On Leave",
            JoinedOn = DateTime.UtcNow.AddDays(-5)
        });

        context.StaffMembers.Add(new StaffMember
        {
            StaffId = 12,
            CanteenId = canteenId,
            EmployeeId = "EMP012",
            FullName = "Inactive Staff",
            MobileNumber = "9876543212",
            Role = "Cleaner",
            Shift = "Morning (6:00 AM - 2:00 PM)",
            Status = "Inactive",
            JoinedOn = DateTime.UtcNow.AddDays(-60)
        });

        await context.SaveChangesAsync();

        var staffRepo = new StaffRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var service = new StaffService(staffRepo, canteenRepo, NullLogger<StaffService>.Instance);

        var metrics = await service.GetSummaryMetricsAsync(canteenId);

        Assert.Equal(12, metrics.TotalStaff);
        Assert.Equal(10, metrics.ActiveStaff);
        Assert.Equal(83.3m, metrics.ActivePercentage);
        Assert.Equal(1, metrics.OnLeaveStaff);
        Assert.Equal(8.3m, metrics.OnLeavePercentage);
        Assert.Equal(1, metrics.InactiveStaff);
        Assert.Equal(8.3m, metrics.InactivePercentage);
    }

    [Fact]
    public async Task CreateStaffAsync_GeneratesEmployeeId_StoresPhoto_AndCalculatesInitials()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 1;

        context.Canteens.Add(new Canteen.Model.Entities.Canteen
        {
            CanteenId = canteenId,
            Name = "CT Catten"
        });
        await context.SaveChangesAsync();

        var staffRepo = new StaffRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var service = new StaffService(staffRepo, canteenRepo, NullLogger<StaffService>.Instance);

        var dto = new CreateStaffMemberDto
        {
            FullName = "Amit More",
            Email = "amit.more@canteenhub.com",
            MobileNumber = "+91 98765 43210",
            Role = "Chef",
            Shift = "Morning (6:00 AM - 2:00 PM)",
            Status = "Active",
            ProfilePhotoUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            Salary = 25000m
        };

        var created = await service.CreateStaffAsync(canteenId, dto);

        Assert.NotNull(created);
        Assert.Equal("EMP001", created.EmployeeId);
        Assert.Equal("Amit More", created.FullName);
        Assert.Equal("AM", created.Initials);
        Assert.Equal("Chef", created.Role);
        Assert.Equal(dto.ProfilePhotoUrl, created.ProfilePhotoUrl);

        // Verify stored in DB
        var savedInDb = await context.StaffMembers.FirstOrDefaultAsync(s => s.StaffId == created.StaffId);
        Assert.NotNull(savedInDb);
        Assert.Equal(dto.ProfilePhotoUrl, savedInDb.ProfilePhotoUrl);

        // Verify activity logged
        var log = await context.StaffActivityLogs.FirstOrDefaultAsync(l => l.StaffId == created.StaffId);
        Assert.NotNull(log);
        Assert.Equal("Staff Created", log.Action);
    }

    [Fact]
    public async Task GetPagedStaffAsync_FiltersByRoleStatusShiftAndSearch()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 2;

        context.StaffMembers.AddRange(
            new StaffMember
            {
                StaffId = 1,
                CanteenId = canteenId,
                EmployeeId = "EMP001",
                FullName = "Pooja Deshmukh",
                MobileNumber = "+91 98765 43211",
                Role = "Kitchen Staff",
                Shift = "Morning (6:00 AM - 2:00 PM)",
                Status = "Active"
            },
            new StaffMember
            {
                StaffId = 2,
                CanteenId = canteenId,
                EmployeeId = "EMP002",
                FullName = "Rohan Gaikwad",
                MobileNumber = "+91 98765 43212",
                Role = "Counter Staff",
                Shift = "Evening (2:00 PM - 10:00 PM)",
                Status = "Active"
            },
            new StaffMember
            {
                StaffId = 3,
                CanteenId = canteenId,
                EmployeeId = "EMP003",
                FullName = "Vikas Kadam",
                MobileNumber = "+91 98765 43214",
                Role = "Inventory Staff",
                Shift = "Morning (6:00 AM - 2:00 PM)",
                Status = "Active"
            }
        );
        await context.SaveChangesAsync();

        var staffRepo = new StaffRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var service = new StaffService(staffRepo, canteenRepo, NullLogger<StaffService>.Instance);

        // Filter by Role: Counter Staff
        var roleFilter = await service.GetPagedStaffAsync(canteenId, 1, 10, null, "Counter Staff", null, null);
        Assert.Single(roleFilter.Items);
        Assert.Equal("Rohan Gaikwad", roleFilter.Items[0].FullName);

        // Filter by Shift: Morning
        var shiftFilter = await service.GetPagedStaffAsync(canteenId, 1, 10, null, null, null, "Morning");
        Assert.Equal(2, shiftFilter.Items.Count);

        // Search by EmployeeId: EMP003
        var searchFilter = await service.GetPagedStaffAsync(canteenId, 1, 10, "EMP003", null, null, null);
        Assert.Single(searchFilter.Items);
        Assert.Equal("Vikas Kadam", searchFilter.Items[0].FullName);
    }

    [Fact]
    public async Task UpdateStaffAsync_UpdatesProfileAndLogsActivity()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 3;

        var member = new StaffMember
        {
            StaffId = 1,
            CanteenId = canteenId,
            EmployeeId = "EMP001",
            FullName = "Rohan Gaikwad",
            Role = "Counter Staff",
            Shift = "Evening",
            Status = "Active"
        };
        context.StaffMembers.Add(member);
        await context.SaveChangesAsync();

        var staffRepo = new StaffRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var service = new StaffService(staffRepo, canteenRepo, NullLogger<StaffService>.Instance);

        var updateDto = new UpdateStaffMemberDto
        {
            Role = "Order Manager",
            Status = "On Leave",
            ProfilePhotoUrl = "https://example.com/avatar.jpg"
        };

        var updated = await service.UpdateStaffAsync(canteenId, 1, updateDto);

        Assert.Equal("Order Manager", updated.Role);
        Assert.Equal("On Leave", updated.Status);
        Assert.Equal("https://example.com/avatar.jpg", updated.ProfilePhotoUrl);

        var log = await context.StaffActivityLogs.FirstOrDefaultAsync(l => l.StaffId == 1 && l.Action == "Profile Updated");
        Assert.NotNull(log);
    }

    [Fact]
    public async Task MarkAttendanceAsync_RecordsCheckInAndCheckOut()
    {
        using var context = CreateInMemoryContext();
        int canteenId = 4;

        var member = new StaffMember
        {
            StaffId = 10,
            CanteenId = canteenId,
            EmployeeId = "EMP010",
            FullName = "Kavita Rao"
        };
        context.StaffMembers.Add(member);
        await context.SaveChangesAsync();

        var staffRepo = new StaffRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var service = new StaffService(staffRepo, canteenRepo, NullLogger<StaffService>.Instance);

        // Clock In
        var checkIn = await service.MarkAttendanceAsync(canteenId, new MarkAttendanceDto
        {
            StaffId = 10,
            Action = "CheckIn",
            Status = "Present"
        });
        Assert.NotNull(checkIn.CheckInTime);

        // Clock Out
        var checkOut = await service.MarkAttendanceAsync(canteenId, new MarkAttendanceDto
        {
            StaffId = 10,
            Action = "CheckOut",
            Status = "Present"
        });
        Assert.NotNull(checkOut.CheckOutTime);
    }

    [Fact]
    public async Task DeleteStaffAsync_EnforcesMultiTenantIsolation()
    {
        using var context = CreateInMemoryContext();
        int canteenA = 1;
        int canteenB = 2;

        context.StaffMembers.Add(new StaffMember
        {
            StaffId = 50,
            CanteenId = canteenA,
            EmployeeId = "EMP050",
            FullName = "Canteen A Staff"
        });
        await context.SaveChangesAsync();

        var staffRepo = new StaffRepository(context);
        var canteenRepo = new CanteenRepository(context);
        var service = new StaffService(staffRepo, canteenRepo, NullLogger<StaffService>.Instance);

        // Attempt deletion from Canteen B
        var deletedFromB = await service.DeleteStaffAsync(canteenB, 50);
        Assert.False(deletedFromB);

        // Ensure still exists in Canteen A
        var stillExists = await context.StaffMembers.AnyAsync(s => s.StaffId == 50);
        Assert.True(stillExists);

        // Delete from correct Canteen A
        var deletedFromA = await service.DeleteStaffAsync(canteenA, 50);
        Assert.True(deletedFromA);
    }
}

