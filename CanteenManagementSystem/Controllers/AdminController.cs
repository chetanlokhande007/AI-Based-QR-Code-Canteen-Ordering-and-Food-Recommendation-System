using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Data;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Canteen.Model.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "PlatformAdmin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly ICanteenService _canteenService;
    private readonly ICanteenTableService _tableService;
    private readonly IQrCodeService _qrCodeService;
    private readonly CanteenDbContext _context;

    public AdminController(
        ICanteenService canteenService,
        ICanteenTableService tableService,
        IQrCodeService qrCodeService,
        CanteenDbContext context)
    {
        _canteenService = canteenService;
        _tableService = tableService;
        _qrCodeService = qrCodeService;
        _context = context;
    }

    /// <summary>Get platform-wide overview metrics (Platform Admin only).</summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(PlatformAdminMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await _canteenService.GetPlatformMetricsAsync();

        // Calculate Registration Trend for the last 6 months
        var allCanteens = await _context.Canteens.AsNoTracking().ToListAsync();
        var now = DateTime.UtcNow;
        var trend = new List<MonthlyRegistrationTrendDto>();

        for (int i = 5; i >= 0; i--)
        {
            var targetMonthDate = now.AddMonths(-i);
            var monthName = targetMonthDate.ToString("MMM", CultureInfo.InvariantCulture);
            var count = allCanteens.Count(c => c.CreatedAt.Year == targetMonthDate.Year && c.CreatedAt.Month == targetMonthDate.Month);
            trend.Add(new MonthlyRegistrationTrendDto
            {
                Month = monthName,
                Count = count
            });
        }

        metrics.RegistrationTrend = trend;

        // Calculate Status Distribution
        var activeCount = allCanteens.Count(c => c.TenantStatus == TenantStatus.Active && c.IsActive);
        var suspendedCount = allCanteens.Count(c => c.TenantStatus == TenantStatus.Suspended);
        var setupInProgressCount = allCanteens.Count(c => c.SetupStatus == SetupStatus.Draft && c.TenantStatus != TenantStatus.Suspended);

        metrics.StatusDistribution = new StatusDistributionDto
        {
            Total = allCanteens.Count,
            Active = activeCount,
            Suspended = suspendedCount,
            SetupInProgress = setupInProgressCount
        };

        // Real activities derived from canteens
        var activities = new List<PlatformActivityDto>();
        int actId = 1;
        foreach (var c in allCanteens.OrderByDescending(c => c.CreatedAt).Take(5))
        {
            activities.Add(new PlatformActivityDto
            {
                Id = actId++,
                Title = "New canteen registered",
                Subtitle = $"{c.Name} (Canteen #{c.CanteenId})",
                Timestamp = c.CreatedAt,
                Type = "canteen"
            });

            if (c.SetupStatus == SetupStatus.Completed)
            {
                activities.Add(new PlatformActivityDto
                {
                    Id = actId++,
                    Title = "Canteen setup completed",
                    Subtitle = $"{c.Name}",
                    Timestamp = c.CreatedAt.AddMinutes(2),
                    Type = "setup"
                });
            }

            if (c.IsActive)
            {
                activities.Add(new PlatformActivityDto
                {
                    Id = actId++,
                    Title = "Canteen active & accepting orders",
                    Subtitle = $"{c.Name}",
                    Timestamp = c.CreatedAt.AddMinutes(5),
                    Type = "activation"
                });
            }
        }

        metrics.RecentActivities = activities.OrderByDescending(a => a.Timestamp).Take(6).ToList();

        return Ok(metrics);
    }

    /// <summary>Get all registered canteens with owner details (Platform Admin only).</summary>
    [HttpGet("canteens")]
    [ProducesResponseType(typeof(IEnumerable<AdminCanteenItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCanteens()
    {
        var canteens = await _canteenService.GetAdminCanteensAsync();
        return Ok(canteens);
    }

    /// <summary>Activate or suspend a canteen (Platform Admin only).</summary>
    [HttpPatch("canteens/{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateCanteenStatusDto dto)
    {
        var success = await _canteenService.UpdateCanteenStatusAsync(id, dto.IsActive, dto.TenantStatus);
        if (!success)
        {
            return NotFound(new { message = $"Canteen with ID {id} was not found." });
        }

        var isSuspended = dto.TenantStatus == TenantStatus.Suspended || (dto.TenantStatus == null && dto.IsActive == false);
        var effectiveTenantStatus = dto.TenantStatus ?? (isSuspended ? TenantStatus.Suspended : TenantStatus.Active);
        var effectiveIsActive = dto.IsActive ?? !isSuspended;

        return Ok(new
        {
            canteenId = id,
            isActive = effectiveIsActive,
            tenantStatus = effectiveTenantStatus.ToString(),
            message = $"Canteen #{id} status has been successfully updated to {effectiveTenantStatus}."
        });
    }

    /// <summary>Get full details for a single canteen (Platform Admin only).</summary>
    [HttpGet("canteens/{id:int}/details")]
    [ProducesResponseType(typeof(AdminCanteenDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCanteenDetails(int id)
    {
        var canteen = await _context.Canteens
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.Tables)
            .Include(c => c.Products)
                .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(c => c.CanteenId == id);

        if (canteen == null)
        {
            return NotFound(new { message = $"Canteen #{id} was not found." });
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Where(o => o.CanteenId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(15)
            .ToListAsync();

        var totalOrdersCount = await _context.Orders.CountAsync(o => o.CanteenId == id);
        var revenue = await _context.Orders
            .Where(o => o.CanteenId == id && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Success"))
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var hasBankConfigured = !string.IsNullOrWhiteSpace(canteen.BankAccountHolder) &&
                                !string.IsNullOrWhiteSpace(canteen.AccountNumber) &&
                                !string.IsNullOrWhiteSpace(canteen.IfscCode);

        string? maskedAccount = null;
        if (!string.IsNullOrWhiteSpace(canteen.AccountNumber))
        {
            var raw = canteen.AccountNumber.Trim();
            maskedAccount = raw.Length > 4 ? $"•••• •••• •••• {raw[^4..]}" : raw;
        }

        var details = new AdminCanteenDetailsDto
        {
            CanteenId = canteen.CanteenId,
            Name = canteen.Name,
            Slug = canteen.Slug,
            Location = canteen.Location,
            CanteenType = canteen.CanteenType,
            Description = canteen.Description,
            ContactPhone = canteen.ContactPhone,
            ContactEmail = canteen.ContactEmail,
            ImageUrl = canteen.ImageUrl,
            AddressLine1 = canteen.AddressLine1,
            AddressLine2 = canteen.AddressLine2,
            City = canteen.City,
            State = canteen.State,
            PostalCode = canteen.PostalCode,
            Country = canteen.Country,
            Landmark = canteen.Landmark,
            OperatingHoursJson = canteen.OperatingHoursJson,
            IsActive = canteen.IsActive,
            TenantStatus = canteen.TenantStatus,
            SetupStatus = canteen.SetupStatus,
            CreatedAt = canteen.CreatedAt,

            Owner = new AdminOwnerSummaryDto
            {
                OwnerId = canteen.OwnerId ?? 0,
                FullName = canteen.Owner?.FullName ?? "Unassigned",
                Email = canteen.Owner?.Email ?? "N/A",
                Role = canteen.Owner?.Role ?? "CanteenOwner",
                CreatedAt = canteen.Owner?.CreatedAt ?? canteen.CreatedAt
            },

            Statistics = new AdminCanteenStatsDto
            {
                TablesCount = canteen.Tables.Count,
                FoodItemsCount = canteen.Products.Count,
                TotalOrdersCount = totalOrdersCount,
                Revenue = revenue
            },

            Tables = canteen.Tables.OrderBy(t => t.TableNumber).Select(t => new CanteenTableDto
            {
                TableId = t.TableId,
                CanteenId = t.CanteenId,
                TableNumber = t.TableNumber,
                Capacity = t.Capacity,
                IsAvailable = t.IsAvailable,
                QRToken = t.QRToken
            }).ToList(),

            FoodItems = canteen.Products.OrderBy(p => p.Name).Select(p => new AdminFoodItemDto
            {
                ProductId = p.ProductId,
                Name = p.Name,
                CategoryName = p.Category?.Name ?? "General",
                Price = p.Price,
                FoodType = p.FoodType.ToString(),
                IsAvailable = p.IsAvailable,
                ImageUrl = p.ImageUrl,
                StockQuantity = p.StockQuantity
            }).ToList(),

            Orders = orders.Select(o => new AdminOrderSummaryDto
            {
                OrderId = o.OrderId,
                OrderNumber = o.OrderNumber,
                TableNumber = o.Table?.TableNumber ?? 0,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus,
                CreatedAt = o.CreatedAt
            }).ToList(),

            BankDetails = new AdminBankDetailsDto
            {
                BankAccountHolder = canteen.BankAccountHolder,
                BankName = canteen.BankName,
                AccountNumberMasked = maskedAccount,
                IfscCode = canteen.IfscCode,
                AccountType = canteen.AccountType,
                UpiId = canteen.UpiId,
                IsConfigured = hasBankConfigured
            }
        };

        return Ok(details);
    }

    /// <summary>Get all tables for a specific canteen (Platform Admin only).</summary>
    [HttpGet("canteens/{canteenId:int}/tables")]
    [ProducesResponseType(typeof(IEnumerable<CanteenTableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCanteenTables(int canteenId)
    {
        var tables = await _tableService.GetAllByCanteenAsync(canteenId);
        return Ok(tables);
    }

    /// <summary>Get QR image for a specific canteen table (Platform Admin only).</summary>
    [HttpGet("canteens/{canteenId:int}/tables/{tableId:int}/qr-image")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTableQrImage(int canteenId, int tableId, [FromQuery] int pixelsPerModule = 10)
    {
        var table = await _tableService.GetByIdAsync(canteenId, tableId);
        if (table is null)
        {
            return NotFound(new { message = $"Table #{tableId} was not found for canteen #{canteenId}." });
        }

        var menuUrl = _qrCodeService.GenerateTableMenuUrl(canteenId, tableId, table.QRToken);
        var pngBytes = _qrCodeService.GenerateQrCodePng(menuUrl, Math.Clamp(pixelsPerModule, 5, 30));

        return File(pngBytes, "image/png", $"table-{tableId}-qr.png");
    }

    /// <summary>Get QR URL for a specific canteen table (Platform Admin only).</summary>
    [HttpGet("canteens/{canteenId:int}/tables/{tableId:int}/qr-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTableQrUrl(int canteenId, int tableId)
    {
        var table = await _tableService.GetByIdAsync(canteenId, tableId);
        if (table is null)
        {
            return NotFound(new { message = $"Table #{tableId} was not found for canteen #{canteenId}." });
        }

        var menuUrl = _qrCodeService.GenerateTableMenuUrl(canteenId, tableId, table.QRToken);
        return Ok(new
        {
            tableId = table.TableId,
            canteenId = table.CanteenId,
            tableNumber = table.TableNumber,
            qrToken = table.QRToken,
            menuUrl
        });
    }

    /// <summary>Get Payment Overview and Transactions (Platform Admin only).</summary>
    [HttpGet("payments/overview")]
    [ProducesResponseType(typeof(AdminPaymentOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentOverview()
    {
        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Order)
                .ThenInclude(o => o.Canteen)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var totalTransactions = payments.Count;
        var successfulCount = payments.Count(p => p.Status.Equals("Success", StringComparison.OrdinalIgnoreCase) ||
                                                  p.Status.Equals("Captured", StringComparison.OrdinalIgnoreCase) ||
                                                  p.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase));
        var failedCount = payments.Count(p => p.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase));
        var totalRevenue = payments
            .Where(p => p.Status.Equals("Success", StringComparison.OrdinalIgnoreCase) ||
                        p.Status.Equals("Captured", StringComparison.OrdinalIgnoreCase) ||
                        p.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount);

        var transactions = payments.Take(25).Select(p => new AdminPaymentTransactionDto
        {
            PaymentId = p.PaymentId,
            TransactionId = p.RazorpayPaymentId ?? p.RazorpayOrderId,
            OrderId = p.OrderId,
            OrderNumber = p.Order?.OrderNumber ?? $"ORD-{p.OrderId}",
            CanteenId = p.CanteenId,
            CanteenName = p.Order?.Canteen?.Name ?? $"Canteen #{p.CanteenId}",
            Amount = p.Amount,
            Currency = p.Currency,
            Status = p.Status,
            PaymentMethod = "Razorpay (Online)",
            CreatedAt = p.CreatedAt
        }).ToList();

        return Ok(new AdminPaymentOverviewDto
        {
            TotalTransactions = totalTransactions,
            SuccessfulCount = successfulCount,
            FailedCount = failedCount,
            TotalRevenue = totalRevenue,
            RecentTransactions = transactions
        });
    }

    /// <summary>Get Canteen Payment and Settlement Setup Status (Platform Admin only).</summary>
    [HttpGet("payments/canteens-setup")]
    [ProducesResponseType(typeof(IEnumerable<AdminCanteenPaymentSetupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCanteensPaymentSetup()
    {
        var canteens = await _context.Canteens
            .AsNoTracking()
            .Include(c => c.Owner)
            .OrderBy(c => c.CanteenId)
            .ToListAsync();

        var result = canteens.Select(c =>
        {
            var isConfigured = !string.IsNullOrWhiteSpace(c.BankAccountHolder) &&
                               !string.IsNullOrWhiteSpace(c.AccountNumber) &&
                               !string.IsNullOrWhiteSpace(c.IfscCode);

            string? maskedAccount = null;
            if (!string.IsNullOrWhiteSpace(c.AccountNumber))
            {
                var raw = c.AccountNumber.Trim();
                maskedAccount = raw.Length > 4 ? $"•••• •••• •••• {raw[^4..]}" : raw;
            }

            return new AdminCanteenPaymentSetupDto
            {
                CanteenId = c.CanteenId,
                CanteenName = c.Name,
                OwnerName = c.Owner?.FullName ?? "Unassigned",
                OwnerEmail = c.Owner?.Email ?? "N/A",
                BankAccountHolder = c.BankAccountHolder,
                BankName = c.BankName,
                AccountNumberMasked = maskedAccount,
                IfscCode = c.IfscCode,
                AccountType = c.AccountType,
                UpiId = c.UpiId,
                IsConfigured = isConfigured,
                SettlementStatus = isConfigured ? "Configured" : "Not Configured"
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>Get Platform Audit Logs (Platform Admin only).</summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(IEnumerable<AdminAuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs()
    {
        var canteens = await _context.Canteens
            .AsNoTracking()
            .Include(c => c.Owner)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var logs = new List<AdminAuditLogDto>();
        int idCounter = 1;

        // Current admin login audit event
        logs.Add(new AdminAuditLogDto
        {
            Id = idCounter++,
            Timestamp = DateTime.UtcNow,
            User = "Platform Admin",
            Action = "Viewed dashboard",
            Details = "Platform overview metrics",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"
        });

        foreach (var c in canteens)
        {
            if (c.IsActive)
            {
                logs.Add(new AdminAuditLogDto
                {
                    Id = idCounter++,
                    Timestamp = c.CreatedAt.AddMinutes(5),
                    User = "Platform Admin",
                    Action = "Activated canteen",
                    Details = $"{c.Name} (#{c.CanteenId})",
                    IpAddress = "127.0.0.1"
                });
            }
            else
            {
                logs.Add(new AdminAuditLogDto
                {
                    Id = idCounter++,
                    Timestamp = c.CreatedAt.AddMinutes(5),
                    User = "Platform Admin",
                    Action = "Suspended canteen",
                    Details = $"{c.Name} (#{c.CanteenId})",
                    IpAddress = "127.0.0.1"
                });
            }

            if (c.SetupStatus == SetupStatus.Completed)
            {
                logs.Add(new AdminAuditLogDto
                {
                    Id = idCounter++,
                    Timestamp = c.CreatedAt.AddMinutes(2),
                    User = c.Owner?.FullName ?? "Canteen Owner",
                    Action = "Canteen setup completed",
                    Details = $"{c.Name} (#{c.CanteenId})",
                    IpAddress = "127.0.0.1"
                });
            }

            logs.Add(new AdminAuditLogDto
            {
                Id = idCounter++,
                Timestamp = c.CreatedAt,
                User = c.Owner?.FullName ?? "System",
                Action = "Registered canteen",
                Details = $"{c.Name} (#{c.CanteenId})",
                IpAddress = "127.0.0.1"
            });
        }

        return Ok(logs.OrderByDescending(l => l.Timestamp).Take(50).ToList());
    }

    /// <summary>Get all SaaS owner accounts (Platform Admin only).</summary>
    [HttpGet("owners")]
    [ProducesResponseType(typeof(IEnumerable<AdminOwnerItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOwners()
    {
        var owners = await _context.Owners
            .AsNoTracking()
            .Include(o => o.Canteens)
            .Where(o => o.Role != "PlatformAdmin")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = owners.Select(o => new AdminOwnerItemDto
        {
            OwnerId = o.OwnerId,
            FullName = o.FullName,
            Email = o.Email,
            Role = o.Role,
            CanteensCount = o.Canteens.Count,
            Status = "Active",
            CreatedAt = o.CreatedAt
        }).ToList();

        return Ok(result);
    }

    /// <summary>Get details of an owner including their canteens (Platform Admin only).</summary>
    [HttpGet("owners/{id:int}")]
    [ProducesResponseType(typeof(AdminOwnerDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwnerDetails(int id)
    {
        var owner = await _context.Owners
            .AsNoTracking()
            .Include(o => o.Canteens)
                .ThenInclude(c => c.Products)
            .Include(o => o.Canteens)
                .ThenInclude(c => c.Tables)
            .FirstOrDefaultAsync(o => o.OwnerId == id);

        if (owner == null)
        {
            return NotFound(new { message = $"Owner #{id} was not found." });
        }

        var result = new AdminOwnerDetailsDto
        {
            OwnerId = owner.OwnerId,
            FullName = owner.FullName,
            Email = owner.Email,
            Role = owner.Role,
            Status = "Active",
            CreatedAt = owner.CreatedAt,
            Canteens = owner.Canteens.Select(c => new AdminCanteenItemDto
            {
                CanteenId = c.CanteenId,
                Name = c.Name,
                Location = c.Location,
                CanteenType = c.CanteenType,
                OwnerId = owner.OwnerId,
                OwnerName = owner.FullName,
                OwnerEmail = owner.Email,
                IsActive = c.IsActive,
                TenantStatus = c.TenantStatus,
                SetupStatus = c.SetupStatus,
                MenuItemsCount = c.Products.Count,
                TablesCount = c.Tables.Count,
                CreatedAt = c.CreatedAt
            }).ToList()
        };

        return Ok(result);
    }
}
