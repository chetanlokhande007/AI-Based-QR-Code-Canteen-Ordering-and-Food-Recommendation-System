using System;
using System.Collections.Generic;
using Canteen.Model.Enums;

namespace Canteen.Model.DTOs;

public class PlatformAdminMetricsDto
{
    public int TotalCanteens { get; set; }
    public int ActiveCanteens { get; set; }
    public int SuspendedCanteens { get; set; }
    public int TotalOwners { get; set; }
    public IEnumerable<AdminCanteenItemDto> RecentCanteens { get; set; } = new List<AdminCanteenItemDto>();
    public IEnumerable<MonthlyRegistrationTrendDto> RegistrationTrend { get; set; } = new List<MonthlyRegistrationTrendDto>();
    public StatusDistributionDto StatusDistribution { get; set; } = new();
    public IEnumerable<PlatformActivityDto> RecentActivities { get; set; } = new List<PlatformActivityDto>();
}

public class MonthlyRegistrationTrendDto
{
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class StatusDistributionDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Suspended { get; set; }
    public int SetupInProgress { get; set; }
}

public class PlatformActivityDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = "info"; // "canteen", "setup", "activation", "suspension", "login"
}

public class AdminCanteenItemDto
{
    public int CanteenId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? CanteenType { get; set; }
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public TenantStatus TenantStatus { get; set; } = TenantStatus.Active;
    public SetupStatus SetupStatus { get; set; } = SetupStatus.Draft;
    public int MenuItemsCount { get; set; }
    public int TablesCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateCanteenStatusDto
{
    public bool? IsActive { get; set; }
    public TenantStatus? TenantStatus { get; set; }
}

public class AdminCanteenDetailsDto
{
    public int CanteenId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? CanteenType { get; set; }
    public string? Description { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? ImageUrl { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Landmark { get; set; }
    public string? OperatingHoursJson { get; set; }
    public bool IsActive { get; set; }
    public TenantStatus TenantStatus { get; set; }
    public SetupStatus SetupStatus { get; set; }
    public DateTime CreatedAt { get; set; }

    public AdminOwnerSummaryDto Owner { get; set; } = new();
    public AdminCanteenStatsDto Statistics { get; set; } = new();
    public IEnumerable<CanteenTableDto> Tables { get; set; } = new List<CanteenTableDto>();
    public IEnumerable<AdminFoodItemDto> FoodItems { get; set; } = new List<AdminFoodItemDto>();
    public IEnumerable<AdminOrderSummaryDto> Orders { get; set; } = new List<AdminOrderSummaryDto>();
    public AdminBankDetailsDto BankDetails { get; set; } = new();
}

public class AdminOwnerSummaryDto
{
    public int OwnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "CanteenOwner";
    public DateTime CreatedAt { get; set; }
}

public class AdminCanteenStatsDto
{
    public int TablesCount { get; set; }
    public int FoodItemsCount { get; set; }
    public int TotalOrdersCount { get; set; }
    public decimal Revenue { get; set; }
}

public class AdminFoodItemDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public decimal Price { get; set; }
    public string FoodType { get; set; } = "Veg";
    public bool IsAvailable { get; set; }
    public string? ImageUrl { get; set; }
    public int StockQuantity { get; set; }
}

public class AdminOrderSummaryDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int TableNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string PaymentStatus { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}

public class AdminBankDetailsDto
{
    public string? BankAccountHolder { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumberMasked { get; set; }
    public string? IfscCode { get; set; }
    public string? AccountType { get; set; }
    public string? UpiId { get; set; }
    public bool IsConfigured { get; set; }
}

public class AdminPaymentOverviewDto
{
    public int TotalTransactions { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public IEnumerable<AdminPaymentTransactionDto> RecentTransactions { get; set; } = new List<AdminPaymentTransactionDto>();
}

public class AdminPaymentTransactionDto
{
    public int PaymentId { get; set; }
    public string? TransactionId { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "Pending"; // "Success", "Failed", "Pending"
    public string PaymentMethod { get; set; } = "Razorpay (Online)";
    public DateTime CreatedAt { get; set; }
}

public class AdminCanteenPaymentSetupDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string? BankAccountHolder { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumberMasked { get; set; }
    public string? IfscCode { get; set; }
    public string? AccountType { get; set; }
    public string? UpiId { get; set; }
    public bool IsConfigured { get; set; }
    public string SettlementStatus { get; set; } = "Not Configured";
}

public class AdminAuditLogDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string User { get; set; } = "Platform Admin";
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
}

public class AdminOwnerItemDto
{
    public int OwnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "CanteenOwner";
    public int CanteensCount { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
}

public class AdminOwnerDetailsDto
{
    public int OwnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "CanteenOwner";
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public IEnumerable<AdminCanteenItemDto> Canteens { get; set; } = new List<AdminCanteenItemDto>();
}
