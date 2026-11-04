using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs.Reports;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class ReportsRepository : IReportsRepository
{
    private readonly CanteenDbContext _context;

    public ReportsRepository(CanteenDbContext context)
    {
        _context = context;
    }

    private static decimal CalculateGrowth(decimal current, decimal previous)
    {
        if (previous <= 0)
        {
            return current > 0 ? 100m : 0m;
        }
        return Math.Round(((current - previous) / previous) * 100m, 2);
    }

    private async Task<string> GetCanteenNameAsync(int canteenId)
    {
        return await _context.Canteens
            .AsNoTracking()
            .Where(c => c.CanteenId == canteenId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync() ?? "Canteen";
    }

    public async Task<OverviewReportDto> GetOverviewReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, prevStartUtc, prevEndUtc) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        // Fetch Current Period Orders
        var currentOrders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
            .ToListAsync();

        var currentRefunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == filter.CanteenId && r.CreatedAt >= startUtc && r.CreatedAt < endUtc && r.Status == "Succeeded")
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        // Fetch Baseline Comparison Orders
        var prevOrders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= prevStartUtc && o.CreatedAt < prevEndUtc)
            .ToListAsync();

        var prevRefunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == filter.CanteenId && r.CreatedAt >= prevStartUtc && r.CreatedAt < prevEndUtc && r.Status == "Succeeded")
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        // Active products count
        var activeProductsCount = await _context.Products
            .AsNoTracking()
            .CountAsync(p => p.CanteenId == filter.CanteenId && p.IsAvailable);

        // Calculations
        var nonCancelledOrders = currentOrders.Where(o => o.Status != "Cancelled").ToList();
        var grossRevenue = nonCancelledOrders.Sum(o => o.TotalAmount);
        var netRevenue = Math.Max(0m, grossRevenue - currentRefunds);
        var totalOrders = currentOrders.Count;
        var completedOrders = currentOrders.Count(o => o.Status == "Completed");
        var cancelledOrders = currentOrders.Count(o => o.Status == "Cancelled");
        var aov = nonCancelledOrders.Count > 0 ? Math.Round(grossRevenue / nonCancelledOrders.Count, 2) : 0m;

        var prevNonCancelled = prevOrders.Where(o => o.Status != "Cancelled").ToList();
        var prevGross = prevNonCancelled.Sum(o => o.TotalAmount);
        var prevNet = Math.Max(0m, prevGross - prevRefunds);
        var prevTotalOrders = prevOrders.Count;
        var prevAov = prevNonCancelled.Count > 0 ? Math.Round(prevGross / prevNonCancelled.Count, 2) : 0m;

        // Revenue Trend by Day or Hour
        var trendPoints = new List<ReportTimeSeriesPointDto>();
        if ((endUtc - startUtc).TotalDays <= 2)
        {
            // Hourly breakdown for 1-2 days
            for (var dt = startUtc; dt < endUtc; dt = dt.AddHours(1))
            {
                var nextDt = dt.AddHours(1);
                var hourOrders = nonCancelledOrders.Where(o => o.CreatedAt >= dt && o.CreatedAt < nextDt).ToList();
                var hourGross = hourOrders.Sum(o => o.TotalAmount);
                trendPoints.Add(new ReportTimeSeriesPointDto
                {
                    Date = dt,
                    DateLabel = dt.ToString("HH:mm"),
                    GrossSales = hourGross,
                    NetSales = hourGross,
                    OrderCount = hourOrders.Count
                });
            }
        }
        else
        {
            // Daily breakdown
            for (var dt = startUtc.Date; dt < endUtc; dt = dt.AddDays(1))
            {
                var nextDt = dt.AddDays(1);
                var dayOrders = nonCancelledOrders.Where(o => o.CreatedAt >= dt && o.CreatedAt < nextDt).ToList();
                var dayGross = dayOrders.Sum(o => o.TotalAmount);
                trendPoints.Add(new ReportTimeSeriesPointDto
                {
                    Date = dt,
                    DateLabel = dt.ToString("MMM dd"),
                    GrossSales = dayGross,
                    NetSales = dayGross,
                    OrderCount = dayOrders.Count
                });
            }
        }

        // Order Status Distribution
        var statusBreakdown = currentOrders
            .GroupBy(o => o.Status)
            .Select(g => new ReportDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                Percentage = totalOrders > 0 ? Math.Round(((decimal)g.Count() / totalOrders) * 100m, 1) : 0m,
                TotalValue = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Payment Method Distribution
        var currentPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.CanteenId == filter.CanteenId && p.CreatedAt >= startUtc && p.CreatedAt < endUtc)
            .ToListAsync();

        var paymentMethodBreakdown = currentPayments
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new ReportDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                Percentage = currentPayments.Count > 0 ? Math.Round(((decimal)g.Count() / currentPayments.Count) * 100m, 1) : 0m,
                TotalValue = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalValue)
            .ToList();

        // Top Selling Products (Limit 5 for Overview)
        var topSellingProducts = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order.CanteenId == filter.CanteenId &&
                         oi.Order.CreatedAt >= startUtc &&
                         oi.Order.CreatedAt < endUtc &&
                         oi.Order.Status != "Cancelled")
            .GroupBy(oi => new { oi.ProductId, oi.Product.Name, CategoryName = oi.Product.Category.Name })
            .Select(g => new TopProductSummaryDto
            {
                ProductId = g.Key.ProductId,
                Name = g.Key.Name,
                CategoryName = g.Key.CategoryName,
                QuantitySold = g.Sum(x => x.Quantity),
                TotalSales = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToListAsync();

        return new OverviewReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            GrossRevenue = grossRevenue,
            NetRevenue = netRevenue,
            TotalRefunds = currentRefunds,
            TotalOrders = totalOrders,
            CompletedOrders = completedOrders,
            CancelledOrders = cancelledOrders,
            AverageOrderValue = aov,
            ActiveProductsCount = activeProductsCount,
            GrossRevenueGrowthPercent = CalculateGrowth(grossRevenue, prevGross),
            NetRevenueGrowthPercent = CalculateGrowth(netRevenue, prevNet),
            OrdersGrowthPercent = CalculateGrowth(totalOrders, prevTotalOrders),
            AverageOrderValueGrowthPercent = CalculateGrowth(aov, prevAov),
            RevenueTrend = trendPoints,
            OrderStatusBreakdown = statusBreakdown,
            PaymentMethodBreakdown = paymentMethodBreakdown,
            TopSellingProducts = topSellingProducts
        };
    }

    public async Task<SalesReportDto> GetSalesReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, prevStartUtc, prevEndUtc) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var currentOrdersQuery = _context.Orders
            .AsNoTracking()
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc);

        var currentOrders = await currentOrdersQuery.ToListAsync();

        var currentRefunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == filter.CanteenId && r.CreatedAt >= startUtc && r.CreatedAt < endUtc && r.Status == "Succeeded")
            .ToListAsync();

        var prevOrders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= prevStartUtc && o.CreatedAt < prevEndUtc)
            .ToListAsync();

        var prevRefunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == filter.CanteenId && r.CreatedAt >= prevStartUtc && r.CreatedAt < prevEndUtc && r.Status == "Succeeded")
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        var validOrders = currentOrders.Where(o => o.Status != "Cancelled").ToList();
        var totalGross = validOrders.Sum(o => o.TotalAmount);
        var totalRefundAmount = currentRefunds.Sum(r => r.Amount);
        var totalNet = Math.Max(0m, totalGross - totalRefundAmount);
        var subtotal = validOrders.Sum(o => o.SubtotalAmount);
        var discounts = validOrders.Sum(o => o.DiscountAmount);
        var tax = validOrders.Sum(o => o.TaxAmount);
        var platformFees = validOrders.Sum(o => o.PlatformFeeAmount);
        var canteenEarnings = validOrders.Sum(o => o.CanteenEarningsAmount);
        var successfulCount = validOrders.Count;
        var aov = successfulCount > 0 ? Math.Round(totalGross / successfulCount, 2) : 0m;

        var prevValidOrders = prevOrders.Where(o => o.Status != "Cancelled").ToList();
        var prevGross = prevValidOrders.Sum(o => o.TotalAmount);
        var prevNet = Math.Max(0m, prevGross - prevRefunds);

        // Daily Timeline
        var timeline = new List<SalesDailyTimelinePointDto>();
        for (var dt = startUtc.Date; dt < endUtc; dt = dt.AddDays(1))
        {
            var nextDt = dt.AddDays(1);
            var dayOrders = validOrders.Where(o => o.CreatedAt >= dt && o.CreatedAt < nextDt).ToList();
            var dayRefunds = currentRefunds.Where(r => r.CreatedAt >= dt && r.CreatedAt < nextDt).Sum(r => r.Amount);
            var dayGross = dayOrders.Sum(o => o.TotalAmount);

            timeline.Add(new SalesDailyTimelinePointDto
            {
                Date = dt,
                DateLabel = dt.ToString("MMM dd"),
                GrossSales = dayGross,
                NetSales = Math.Max(0m, dayGross - dayRefunds),
                OrderCount = dayOrders.Count,
                TaxCollected = dayOrders.Sum(o => o.TaxAmount),
                DiscountsGiven = dayOrders.Sum(o => o.DiscountAmount),
                RefundsIssued = dayRefunds
            });
        }

        // Payment Methods Breakdown
        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.CanteenId == filter.CanteenId && p.CreatedAt >= startUtc && p.CreatedAt < endUtc)
            .ToListAsync();

        var paymentMethods = payments
            .GroupBy(p => p.PaymentMethod)
            .Select(g =>
            {
                var methodGross = g.Where(x => x.Status == "Paid").Sum(x => x.Amount);
                var methodRefunded = currentRefunds.Where(r => r.Payment != null && r.Payment.PaymentMethod == g.Key).Sum(r => r.Amount);
                return new SalesPaymentMethodBreakdownDto
                {
                    PaymentMethod = g.Key,
                    TransactionsCount = g.Count(),
                    GrossAmount = methodGross,
                    RefundAmount = methodRefunded,
                    NetAmount = Math.Max(0m, methodGross - methodRefunded),
                    SharePercentage = totalGross > 0 ? Math.Round((methodGross / totalGross) * 100m, 1) : 0m
                };
            })
            .OrderByDescending(x => x.GrossAmount)
            .ToList();

        // Category Breakdown
        var categorySales = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order.CanteenId == filter.CanteenId &&
                         oi.Order.CreatedAt >= startUtc &&
                         oi.Order.CreatedAt < endUtc &&
                         oi.Order.Status != "Cancelled")
            .GroupBy(oi => oi.Product.Category.Name)
            .Select(g => new SalesCategoryBreakdownDto
            {
                CategoryName = g.Key,
                QuantitySold = g.Sum(x => x.Quantity),
                TotalSales = g.Sum(x => x.Quantity * x.UnitPrice),
                SharePercentage = totalGross > 0 ? Math.Round((g.Sum(x => x.Quantity * x.UnitPrice) / totalGross) * 100m, 1) : 0m
            })
            .OrderByDescending(x => x.TotalSales)
            .ToListAsync();

        // Paginated Transactions
        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Refunds)
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();
        var pagedOrders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(o => new SaleTransactionDetailDto
            {
                OrderId = o.OrderId,
                OrderNumber = o.OrderNumber,
                TableNumber = o.Table != null ? o.Table.TableNumber.ToString() : "-",
                CreatedAt = o.CreatedAt,
                Subtotal = o.SubtotalAmount,
                Discount = o.DiscountAmount,
                Tax = o.TaxAmount,
                TotalAmount = o.TotalAmount,
                RefundedAmount = o.Refunds.Where(r => r.Status == "Succeeded").Sum(r => (decimal?)r.Amount) ?? 0m,
                NetAmount = Math.Max(0m, o.TotalAmount - (o.Refunds.Where(r => r.Status == "Succeeded").Sum(r => (decimal?)r.Amount) ?? 0m)),
                PaymentMethod = o.Refunds.FirstOrDefault() != null ? "Mixed" : "Counter/Online",
                OrderStatus = o.Status,
                PaymentStatus = o.PaymentStatus
            })
            .ToListAsync();

        return new SalesReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            GrossSales = totalGross,
            NetSales = totalNet,
            SubtotalAmount = subtotal,
            TotalDiscounts = discounts,
            TotalTaxCollected = tax,
            TotalPlatformFees = platformFees,
            TotalCanteenEarnings = canteenEarnings,
            TotalRefunds = totalRefundAmount,
            SuccessfulOrdersCount = successfulCount,
            AverageOrderValue = aov,
            GrossSalesGrowthPercent = CalculateGrowth(totalGross, prevGross),
            NetSalesGrowthPercent = CalculateGrowth(totalNet, prevNet),
            OrdersGrowthPercent = CalculateGrowth(successfulCount, prevValidOrders.Count),
            DailyTimeline = timeline,
            PaymentMethodSummary = paymentMethods,
            CategorySales = categorySales,
            Transactions = new ReportPagedResult<SaleTransactionDetailDto>
            {
                Items = pagedOrders,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<OrderReportDto> GetOrderReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Refunds)
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
            .ToListAsync();

        var totalOrders = orders.Count;
        var completedOrders = orders.Count(o => o.Status == "Completed");
        var cancelledOrders = orders.Count(o => o.Status == "Cancelled");
        var inProgress = orders.Count(o => o.Status is "Pending" or "Preparing" or "Ready");
        var fulfillmentRate = totalOrders > 0 ? Math.Round(((decimal)completedOrders / totalOrders) * 100m, 1) : 0m;
        var cancellationRate = totalOrders > 0 ? Math.Round(((decimal)cancelledOrders / totalOrders) * 100m, 1) : 0m;
        var totalOrderValue = orders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount);
        var cancelledValue = orders.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount);

        // Status Breakdown
        var statusBreakdown = orders
            .GroupBy(o => o.Status)
            .Select(g => new ReportDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                Percentage = totalOrders > 0 ? Math.Round(((decimal)g.Count() / totalOrders) * 100m, 1) : 0m,
                TotalValue = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Cancellation Reasons
        var cancellationReasons = orders
            .Where(o => o.Status == "Cancelled")
            .GroupBy(o => string.IsNullOrWhiteSpace(o.CancellationReason) ? "Customer Request" : o.CancellationReason)
            .Select(g => new CancellationReasonStatDto
            {
                Reason = g.Key,
                Count = g.Count(),
                Percentage = cancelledOrders > 0 ? Math.Round(((decimal)g.Count() / cancelledOrders) * 100m, 1) : 0m,
                TotalRefundedAmount = g.Sum(x => x.Refunds.Where(r => r.Status == "Succeeded").Sum(r => r.Amount))
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Timeline
        var timeline = new List<OrderTimelinePointDto>();
        for (var dt = startUtc.Date; dt < endUtc; dt = dt.AddDays(1))
        {
            var nextDt = dt.AddDays(1);
            var dayOrders = orders.Where(o => o.CreatedAt >= dt && o.CreatedAt < nextDt).ToList();
            timeline.Add(new OrderTimelinePointDto
            {
                Date = dt,
                DateLabel = dt.ToString("MMM dd"),
                TotalOrders = dayOrders.Count,
                CompletedOrders = dayOrders.Count(o => o.Status == "Completed"),
                CancelledOrders = dayOrders.Count(o => o.Status == "Cancelled")
            });
        }

        // Paged Detailed Orders
        var pagedQuery = _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            pagedQuery = pagedQuery.Where(o => o.OrderNumber.ToLower().Contains(search));
        }

        var totalCount = await pagedQuery.CountAsync();
        var pagedItems = await pagedQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(o => new OrderDetailReportItemDto
            {
                OrderId = o.OrderId,
                OrderNumber = o.OrderNumber,
                TableNumber = o.Table != null ? o.Table.TableNumber.ToString() : "-",
                TotalItems = o.Items.Sum(i => i.Quantity),
                SubtotalAmount = o.SubtotalAmount,
                DiscountAmount = o.DiscountAmount,
                TaxAmount = o.TaxAmount,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus,
                CancellationReason = o.CancellationReason,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        return new OrderReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalOrders = totalOrders,
            CompletedOrders = completedOrders,
            CancelledOrders = cancelledOrders,
            InProgressOrders = inProgress,
            FulfillmentRate = fulfillmentRate,
            CancellationRate = cancellationRate,
            TotalOrderValue = totalOrderValue,
            CancelledValue = cancelledValue,
            StatusBreakdown = statusBreakdown,
            CancellationReasons = cancellationReasons,
            Timeline = timeline,
            Orders = new ReportPagedResult<OrderDetailReportItemDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<ProductReportDto> GetProductReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var allProducts = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.CanteenId == filter.CanteenId)
            .ToListAsync();

        var soldItems = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order.CanteenId == filter.CanteenId &&
                         oi.Order.CreatedAt >= startUtc &&
                         oi.Order.CreatedAt < endUtc &&
                         oi.Order.Status != "Cancelled")
            .GroupBy(oi => oi.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                UnitsSold = g.Sum(x => x.Quantity),
                TotalSales = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .ToListAsync();

        var soldDict = soldItems.ToDictionary(x => x.ProductId, x => x);
        var totalRevenue = soldItems.Sum(x => x.TotalSales);
        var totalUnits = soldItems.Sum(x => x.UnitsSold);

        var performanceList = allProducts.Select(p =>
        {
            soldDict.TryGetValue(p.ProductId, out var sold);
            var units = sold?.UnitsSold ?? 0;
            var sales = sold?.TotalSales ?? 0m;
            var share = totalRevenue > 0 ? Math.Round((sales / totalRevenue) * 100m, 1) : 0m;
            return new ProductPerformanceDetailDto
            {
                ProductId = p.ProductId,
                Name = p.Name,
                CategoryName = p.Category.Name,
                FoodType = p.FoodType,
                UnitPrice = p.Price,
                QuantitySold = units,
                TotalRevenue = sales,
                CurrentStock = p.StockQuantity,
                IsAvailable = p.IsAvailable,
                RevenueSharePercentage = share
            };
        }).ToList();

        var topSelling = performanceList.OrderByDescending(x => x.QuantitySold).Take(5).ToList();
        var slowMoving = performanceList.OrderBy(x => x.QuantitySold).Take(5).ToList();

        var categoryPerf = performanceList
            .GroupBy(x => x.CategoryName)
            .Select(g => new CategoryPerformanceStatDto
            {
                CategoryId = 0,
                CategoryName = g.Key,
                TotalProducts = g.Count(),
                UnitsSold = g.Sum(x => x.QuantitySold),
                TotalSales = g.Sum(x => x.TotalRevenue),
                RevenueSharePercentage = totalRevenue > 0 ? Math.Round((g.Sum(x => x.TotalRevenue) / totalRevenue) * 100m, 1) : 0m
            })
            .OrderByDescending(x => x.TotalSales)
            .ToList();

        // Paginated products with optional search
        var pagedQuery = performanceList.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            pagedQuery = pagedQuery.Where(p => p.Name.ToLower().Contains(search) || p.CategoryName.ToLower().Contains(search));
        }

        var totalCount = pagedQuery.Count();
        var pagedItems = pagedQuery
            .OrderByDescending(x => x.TotalRevenue)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new ProductReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalProductsInCatalog = allProducts.Count,
            ActiveProductsCount = allProducts.Count(p => p.IsAvailable),
            OutOfStockCount = allProducts.Count(p => p.StockQuantity <= 0),
            TotalUnitsSold = totalUnits,
            TotalRevenueGenerated = totalRevenue,
            TopSellingProductName = topSelling.FirstOrDefault()?.Name ?? "N/A",
            TopSellingProducts = topSelling,
            SlowMovingProducts = slowMoving,
            CategoryPerformance = categoryPerf,
            Products = new ReportPagedResult<ProductPerformanceDetailDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<PaymentReportDto> GetPaymentReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Order)
            .Include(p => p.Refunds)
            .Where(p => p.CanteenId == filter.CanteenId && p.CreatedAt >= startUtc && p.CreatedAt < endUtc)
            .ToListAsync();

        var refunds = await _context.Refunds
            .AsNoTracking()
            .Where(r => r.CanteenId == filter.CanteenId && r.CreatedAt >= startUtc && r.CreatedAt < endUtc && r.Status == "Succeeded")
            .ToListAsync();

        var paidPayments = payments.Where(p => p.Status == "Paid").ToList();
        var totalGrossCollections = paidPayments.Sum(p => p.Amount);
        var onlineCollections = paidPayments.Where(p => p.PaymentMethod == "Online").Sum(p => p.Amount);
        var counterCollections = paidPayments.Where(p => p.PaymentMethod == "Counter").Sum(p => p.Amount);
        var totalRefundedAmount = refunds.Sum(r => r.Amount);
        var netSettlement = Math.Max(0m, totalGrossCollections - totalRefundedAmount);
        var totalTransactions = payments.Count;
        var successCount = paidPayments.Count;
        var failedCount = payments.Count(p => p.Status == "Failed");
        var successRate = totalTransactions > 0 ? Math.Round(((decimal)successCount / totalTransactions) * 100m, 1) : 0m;

        // Method Breakdown
        var methodsSummary = payments
            .GroupBy(p => p.PaymentMethod)
            .Select(g =>
            {
                var gPaid = g.Where(x => x.Status == "Paid").Sum(x => x.Amount);
                var gRefunded = refunds.Where(r => g.Any(p => p.PaymentId == r.PaymentId)).Sum(r => r.Amount);
                return new PaymentMethodLedgerSummaryDto
                {
                    Method = g.Key,
                    TotalTransactions = g.Count(),
                    SuccessCount = g.Count(x => x.Status == "Paid"),
                    FailedCount = g.Count(x => x.Status == "Failed"),
                    TotalCollected = gPaid,
                    TotalRefunded = gRefunded,
                    NetCollected = Math.Max(0m, gPaid - gRefunded),
                    SharePercentage = totalGrossCollections > 0 ? Math.Round((gPaid / totalGrossCollections) * 100m, 1) : 0m
                };
            })
            .OrderByDescending(x => x.TotalCollected)
            .ToList();

        // Refunds by Role
        var refundsByRole = refunds
            .GroupBy(r => string.IsNullOrWhiteSpace(r.InitiatedByRole) ? "Customer" : r.InitiatedByRole)
            .Select(g => new RefundBreakdownDto
            {
                InitiatedByRole = g.Key,
                RefundCount = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                Percentage = totalRefundedAmount > 0 ? Math.Round((g.Sum(x => x.Amount) / totalRefundedAmount) * 100m, 1) : 0m
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        // Paginated Payments Ledger
        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Order)
            .Include(p => p.Refunds)
            .Where(p => p.CanteenId == filter.CanteenId && p.CreatedAt >= startUtc && p.CreatedAt < endUtc);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(p => (p.RazorpayPaymentId != null && p.RazorpayPaymentId.ToLower().Contains(search)) ||
                                     p.Order.OrderNumber.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();
        var pagedItems = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new PaymentLedgerItemDto
            {
                PaymentId = p.PaymentId,
                OrderId = p.OrderId,
                OrderNumber = p.Order.OrderNumber,
                Amount = p.Amount,
                Currency = p.Currency,
                PaymentMethod = p.PaymentMethod,
                Status = p.Status,
                RazorpayPaymentId = p.RazorpayPaymentId,
                FailureReason = p.FailureReason,
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt,
                RefundedAmount = p.Refunds.Where(r => r.Status == "Succeeded").Sum(r => (decimal?)r.Amount) ?? 0m,
                RefundStatus = p.Refunds.Any(r => r.Status == "Succeeded") ? "Refunded" : "None"
            })
            .ToListAsync();

        return new PaymentReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalGrossCollections = totalGrossCollections,
            OnlineCollections = onlineCollections,
            CounterCashCollections = counterCollections,
            TotalRefundedAmount = totalRefundedAmount,
            NetSettlementAmount = netSettlement,
            TotalTransactions = totalTransactions,
            SuccessfulPaymentsCount = successCount,
            FailedPaymentsCount = failedCount,
            RefundsCount = refunds.Count,
            PaymentSuccessRate = successRate,
            MethodsSummary = methodsSummary,
            RefundsByRole = refundsByRole,
            Payments = new ReportPagedResult<PaymentLedgerItemDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<InventoryReportDto> GetInventoryReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var ingredients = await _context.InventoryIngredients
            .AsNoTracking()
            .Where(i => i.CanteenId == filter.CanteenId)
            .ToListAsync();

        var wastages = await _context.IngredientWastages
            .AsNoTracking()
            .Where(w => w.CanteenId == filter.CanteenId && w.CreatedAt >= startUtc && w.CreatedAt < endUtc)
            .ToListAsync();

        var transactions = await _context.StockTransactions
            .AsNoTracking()
            .Where(t => t.CanteenId == filter.CanteenId && t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .ToListAsync();

        var totalValuation = ingredients.Sum(i => i.CurrentStock * i.CostPerBaseUnit);
        var lowStockList = ingredients
            .Where(i => i.CurrentStock <= i.MinimumStockLevel)
            .Select(i => new LowStockAlertItemDto
            {
                IngredientId = i.IngredientId,
                Name = i.Name,
                Category = i.Category,
                BaseUnit = i.BaseUnit,
                CurrentStock = i.CurrentStock,
                MinimumStockLevel = i.MinimumStockLevel,
                ReorderLevel = i.ReorderLevel,
                CostPerUnit = i.CostPerBaseUnit,
                DeficitQuantity = Math.Max(0m, i.MinimumStockLevel - i.CurrentStock)
            })
            .OrderByDescending(x => x.DeficitQuantity)
            .ToList();

        var totalWastageCost = wastages.Sum(w => w.EstimatedCost);
        var totalWastageQuantity = wastages.Sum(w => w.Quantity);

        var wastageByReason = wastages
            .GroupBy(w => string.IsNullOrWhiteSpace(w.Reason) ? "Spoiled" : w.Reason)
            .Select(g => new WastageReasonSummaryDto
            {
                Reason = g.Key,
                IncidentCount = g.Count(),
                TotalQuantityWasted = g.Sum(x => x.Quantity),
                TotalCost = g.Sum(x => x.EstimatedCost),
                PercentageOfCost = totalWastageCost > 0 ? Math.Round((g.Sum(x => x.EstimatedCost) / totalWastageCost) * 100m, 1) : 0m
            })
            .OrderByDescending(x => x.TotalCost)
            .ToList();

        var movementsByType = transactions
            .GroupBy(t => t.TransactionType)
            .Select(g => new StockMovementSummaryDto
            {
                TransactionType = g.Key,
                TransactionCount = g.Count(),
                TotalQuantityMoved = g.Sum(x => x.Quantity)
            })
            .OrderByDescending(x => x.TransactionCount)
            .ToList();

        // Paginated Ingredients
        var query = ingredients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(search) || i.Category.ToLower().Contains(search));
        }

        var totalCount = query.Count();
        var pagedItems = query
            .OrderBy(i => i.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(i => new IngredientInventoryItemDto
            {
                IngredientId = i.IngredientId,
                Name = i.Name,
                Category = i.Category,
                BaseUnit = i.BaseUnit,
                CurrentStock = i.CurrentStock,
                ReservedStock = i.ReservedStock,
                AvailableStock = i.AvailableStock,
                MinimumStockLevel = i.MinimumStockLevel,
                CostPerBaseUnit = i.CostPerBaseUnit,
                Valuation = i.CurrentStock * i.CostPerBaseUnit,
                IsLowStock = i.CurrentStock <= i.MinimumStockLevel,
                IsActive = i.IsActive
            })
            .ToList();

        return new InventoryReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalIngredientsTracked = ingredients.Count,
            TotalInventoryValuation = totalValuation,
            LowStockAlertsCount = lowStockList.Count,
            TotalWastageCost = totalWastageCost,
            TotalWastageQuantity = totalWastageQuantity,
            TotalStockTransactionsCount = transactions.Count,
            LowStockItems = lowStockList,
            WastageByReason = wastageByReason,
            StockMovementsByType = movementsByType,
            Ingredients = new ReportPagedResult<IngredientInventoryItemDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<CouponReportDto> GetCouponReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var coupons = await _context.Coupons
            .AsNoTracking()
            .Where(c => c.CanteenId == null || c.CanteenId == filter.CanteenId)
            .ToListAsync();

        var redemptions = await _context.CouponRedemptions
            .AsNoTracking()
            .Include(r => r.Coupon)
            .Include(r => r.Order)
            .Where(r => r.CanteenId == filter.CanteenId && r.RedeemedAt >= startUtc && r.RedeemedAt < endUtc)
            .ToListAsync();

        var totalDiscount = redemptions.Sum(r => r.DiscountAmountApplied);
        var totalOrderSales = redemptions.Sum(r => r.Order.TotalAmount);
        var totalRedemptionsCount = redemptions.Count;
        var avgDiscount = totalRedemptionsCount > 0 ? Math.Round(totalDiscount / totalRedemptionsCount, 2) : 0m;
        var roi = totalDiscount > 0 ? Math.Round(totalOrderSales / totalDiscount, 2) : 0m;

        var campaigns = coupons.Select(c =>
        {
            var cReds = redemptions.Where(r => r.CouponId == c.CouponId).ToList();
            return new CouponCampaignPerformanceDto
            {
                CouponId = c.CouponId,
                Code = c.Code,
                Title = c.Title,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                RedemptionsCount = cReds.Count,
                TotalDiscountGiven = cReds.Sum(r => r.DiscountAmountApplied),
                TotalSalesGenerated = cReds.Sum(r => r.Order.TotalAmount),
                IsActive = c.IsActive,
                StartDate = c.StartDate,
                EndDate = c.EndDate
            };
        }).OrderByDescending(x => x.TotalSalesGenerated).ToList();

        // Paginated Redemptions
        var query = redemptions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(r => r.Coupon.Code.ToLower().Contains(search) || r.Order.OrderNumber.ToLower().Contains(search));
        }

        var totalCount = query.Count();
        var pagedItems = query
            .OrderByDescending(r => r.RedeemedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(r => new CouponRedemptionReportItemDto
            {
                RedemptionId = r.RedemptionId,
                CouponCode = r.Coupon.Code,
                OrderNumber = r.Order.OrderNumber,
                CustomerIdentifier = r.CustomerIdentifier,
                DiscountAmountApplied = r.DiscountAmountApplied,
                OrderTotal = r.Order.TotalAmount,
                RedeemedAt = r.RedeemedAt
            })
            .ToList();

        return new CouponReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalActiveCoupons = coupons.Count(c => c.IsActive),
            TotalRedemptionsCount = totalRedemptionsCount,
            TotalDiscountGiven = totalDiscount,
            TotalOrderSalesWithDiscount = totalOrderSales,
            AverageDiscountPerRedemption = avgDiscount,
            PromotionalRoiRatio = roi,
            Campaigns = campaigns,
            Redemptions = new ReportPagedResult<CouponRedemptionReportItemDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<StaffReportDto> GetStaffReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var staff = await _context.StaffMembers
            .AsNoTracking()
            .Where(s => s.CanteenId == filter.CanteenId)
            .ToListAsync();

        var startDay = DateTime.SpecifyKind(startUtc.Date, DateTimeKind.Utc);
        var endDay = DateTime.SpecifyKind(endUtc.Date.AddDays(1), DateTimeKind.Utc);

        var attendances = await _context.StaffAttendances
            .AsNoTracking()
            .Where(sa => sa.CanteenId == filter.CanteenId && sa.Date >= startDay && sa.Date < endDay)
            .ToListAsync();

        var totalStaff = staff.Count;
        var activeStaff = staff.Count(s => s.Status == "Active");
        var onLeave = staff.Count(s => s.Status == "On Leave");
        var inactive = staff.Count(s => s.Status == "Inactive");
        var totalSalary = staff.Sum(s => s.Salary ?? 0m);

        var totalAttendanceLogs = attendances.Count;
        var presentCount = attendances.Count(a => a.Status == "Present");
        var absentCount = attendances.Count(a => a.Status == "Absent");
        var halfDayCount = attendances.Count(a => a.Status == "Half Day");
        var onLeaveAttendance = attendances.Count(a => a.Status == "On Leave");
        var overallAttendanceRate = totalAttendanceLogs > 0
            ? Math.Round(((decimal)(presentCount + (halfDayCount * 0.5m)) / totalAttendanceLogs) * 100m, 1)
            : 0m;

        // Role Breakdown
        var roleBreakdown = staff
            .GroupBy(s => s.Role)
            .Select(g => new ReportDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                Percentage = totalStaff > 0 ? Math.Round(((decimal)g.Count() / totalStaff) * 100m, 1) : 0m,
                TotalValue = g.Sum(x => x.Salary ?? 0m)
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Shift Breakdown
        var shiftBreakdown = staff
            .GroupBy(s => s.Shift)
            .Select(g => new ReportDistributionDto
            {
                Label = g.Key,
                Count = g.Count(),
                Percentage = totalStaff > 0 ? Math.Round(((decimal)g.Count() / totalStaff) * 100m, 1) : 0m,
                TotalValue = 0m
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Daily Attendance Trend
        var dailyTrends = new List<DailyAttendanceTrendDto>();
        for (var dt = startDay; dt < endDay; dt = dt.AddDays(1))
        {
            var dayLogs = attendances.Where(a => a.Date.Date == dt.Date).ToList();
            dailyTrends.Add(new DailyAttendanceTrendDto
            {
                Date = dt,
                DateLabel = dt.ToString("MMM dd"),
                PresentCount = dayLogs.Count(a => a.Status == "Present"),
                AbsentCount = dayLogs.Count(a => a.Status == "Absent"),
                HalfDayCount = dayLogs.Count(a => a.Status == "Half Day"),
                OnLeaveCount = dayLogs.Count(a => a.Status == "On Leave")
            });
        }

        // Paginated Staff Members
        IEnumerable<Canteen.Model.Entities.StaffMember> staffList = staff;
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            staffList = staffList.Where(s => s.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                             s.EmployeeId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                             s.Role.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = staffList.Count();
        var pagedItems = staffList
            .OrderBy(s => s.FullName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s =>
            {
                var sAtt = attendances.Where(a => a.StaffId == s.StaffId).ToList();
                var pDays = sAtt.Count(a => a.Status == "Present");
                var aDays = sAtt.Count(a => a.Status == "Absent");
                var rate = sAtt.Count > 0 ? Math.Round(((decimal)pDays / sAtt.Count) * 100m, 1) : 0m;
                return new StaffMemberReportItemDto
                {
                    StaffId = s.StaffId,
                    EmployeeId = s.EmployeeId,
                    FullName = s.FullName,
                    Role = s.Role,
                    Shift = s.Shift,
                    Status = s.Status,
                    JoinedOn = s.JoinedOn,
                    Salary = s.Salary,
                    PresentDays = pDays,
                    AbsentDays = aDays,
                    AttendanceRate = rate
                };
            })
            .ToList();

        return new StaffReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalStaffCount = totalStaff,
            ActiveStaffCount = activeStaff,
            OnLeaveStaffCount = onLeave,
            InactiveStaffCount = inactive,
            TotalMonthlyPayroll = totalSalary,
            OverallAttendanceRate = overallAttendanceRate,
            RoleBreakdown = roleBreakdown,
            ShiftBreakdown = shiftBreakdown,
            AttendanceSummary = new StaffAttendanceSummaryDto
            {
                TotalAttendanceLogs = totalAttendanceLogs,
                PresentCount = presentCount,
                AbsentCount = absentCount,
                HalfDayCount = halfDayCount,
                OnLeaveCount = onLeaveAttendance,
                AttendancePercentage = overallAttendanceRate,
                DailyTrend = dailyTrends
            },
            StaffMembers = new ReportPagedResult<StaffMemberReportItemDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<StaffAttendanceReportDto> GetAttendanceReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var startDay = DateTime.SpecifyKind(startUtc.Date, DateTimeKind.Utc);
        var endDay = DateTime.SpecifyKind(endUtc.Date.AddDays(1), DateTimeKind.Utc);

        var attendances = await _context.StaffAttendances
            .AsNoTracking()
            .Include(a => a.StaffMember)
            .Where(a => a.CanteenId == filter.CanteenId && a.Date >= startDay && a.Date < endDay)
            .ToListAsync();

        var totalLogs = attendances.Count;
        var present = attendances.Count(a => a.Status == "Present");
        var absent = attendances.Count(a => a.Status == "Absent");
        var halfDay = attendances.Count(a => a.Status == "Half Day");
        var onLeave = attendances.Count(a => a.Status == "On Leave");
        var rate = totalLogs > 0 ? Math.Round(((decimal)(present + (halfDay * 0.5m)) / totalLogs) * 100m, 1) : 0m;

        var dailyRecords = new List<DailyAttendanceTrendDto>();
        for (var dt = startDay; dt < endDay; dt = dt.AddDays(1))
        {
            var dayLogs = attendances.Where(a => a.Date.Date == dt.Date).ToList();
            dailyRecords.Add(new DailyAttendanceTrendDto
            {
                Date = dt,
                DateLabel = dt.ToString("MMM dd"),
                PresentCount = dayLogs.Count(a => a.Status == "Present"),
                AbsentCount = dayLogs.Count(a => a.Status == "Absent"),
                HalfDayCount = dayLogs.Count(a => a.Status == "Half Day"),
                OnLeaveCount = dayLogs.Count(a => a.Status == "On Leave")
            });
        }

        var query = attendances.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(a => a.StaffMember.FullName.ToLower().Contains(search) || a.StaffMember.EmployeeId.ToLower().Contains(search));
        }

        var totalCount = query.Count();
        var pagedItems = query
            .OrderByDescending(a => a.Date)
            .ThenBy(a => a.StaffMember.FullName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new AttendanceRecordDetailDto
            {
                AttendanceId = a.AttendanceId,
                StaffId = a.StaffId,
                EmployeeId = a.StaffMember.EmployeeId,
                FullName = a.StaffMember.FullName,
                Role = a.StaffMember.Role,
                Date = a.Date,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Status = a.Status,
                Notes = a.Notes
            })
            .ToList();

        return new StaffAttendanceReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalWorkDays = (int)(endUtc.Date - startUtc.Date).TotalDays + 1,
            TotalPresent = present,
            TotalAbsent = absent,
            TotalHalfDay = halfDay,
            TotalOnLeave = onLeave,
            OverallAttendanceRate = rate,
            DailyRecords = dailyRecords,
            LogEntries = new ReportPagedResult<AttendanceRecordDetailDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<KitchenReportDto> GetKitchenReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Category)
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
            .ToListAsync();

        var nonCancelled = orders.Where(o => o.Status != "Cancelled").ToList();
        var totalOrdersProcessed = nonCancelled.Count;
        var totalDishes = nonCancelled.SelectMany(o => o.Items).Sum(i => i.Quantity);

        // Approximate duration in minutes based on status / timestamps
        var now = DateTime.UtcNow;
        var queueItems = nonCancelled.Select(o =>
        {
            var duration = (int)(now - o.CreatedAt).TotalMinutes;
            var isDelayed = o.Status is "Pending" or "Preparing" && duration > 20;
            return new KitchenOrderQueueItemDto
            {
                OrderId = o.OrderId,
                OrderNumber = o.OrderNumber,
                TableNumber = o.Table != null ? o.Table.TableNumber.ToString() : "-",
                TotalItemsCount = o.Items.Sum(i => i.Quantity),
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                DurationMinutes = duration,
                IsDelayed = isDelayed
            };
        }).ToList();

        var delayedCount = queueItems.Count(x => x.IsDelayed);
        var preparedUnder15 = queueItems.Count(x => x.DurationMinutes <= 15 || x.Status == "Completed");
        var efficiency = totalOrdersProcessed > 0 ? Math.Round(((decimal)(totalOrdersProcessed - delayedCount) / totalOrdersProcessed) * 100m, 1) : 100m;
        var avgMinutes = totalOrdersProcessed > 0 ? Math.Round((decimal)queueItems.Average(x => Math.Min(x.DurationMinutes, 25)), 1) : 12m;

        // Hourly Velocity
        var velocityByHour = new List<HourlyKitchenVelocityDto>();
        for (int h = 6; h <= 22; h++)
        {
            var hourOrders = nonCancelled.Where(o => o.CreatedAt.Hour == h).ToList();
            velocityByHour.Add(new HourlyKitchenVelocityDto
            {
                Hour = h,
                HourLabel = $"{h:00}:00",
                OrdersCount = hourOrders.Count,
                AvgPrepMinutes = hourOrders.Count > 0 ? 12.5m : 0m
            });
        }

        // Top Prepared Dishes
        var topDishes = nonCancelled
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.ProductId, i.Product.Name, CategoryName = i.Product.Category.Name })
            .Select(g => new KitchenDishThroughputDto
            {
                ProductId = g.Key.ProductId,
                Name = g.Key.Name,
                CategoryName = g.Key.CategoryName,
                TotalUnitsCooked = g.Sum(x => x.Quantity),
                PercentageOfTotal = totalDishes > 0 ? Math.Round(((decimal)g.Sum(x => x.Quantity) / totalDishes) * 100m, 1) : 0m
            })
            .OrderByDescending(x => x.TotalUnitsCooked)
            .Take(8)
            .ToList();

        // Paginated Queue
        var query = queueItems.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(q => q.OrderNumber.ToLower().Contains(search) || q.TableNumber.ToLower().Contains(search));
        }

        var totalCount = query.Count();
        var pagedItems = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new KitchenReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            TotalOrdersProcessed = totalOrdersProcessed,
            TotalDishesPrepared = totalDishes,
            AveragePreparationMinutes = avgMinutes,
            OrdersPreparedUnder15Minutes = preparedUnder15,
            DelayedOrdersCount = delayedCount,
            KitchenEfficiencyScore = efficiency,
            VelocityByHour = velocityByHour,
            TopPreparedDishes = topDishes,
            OrderQueue = new ReportPagedResult<KitchenOrderQueueItemDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            }
        };
    }

    public async Task<PeakHourReportDto> GetPeakHourReportAsync(ReportFilterDto filter)
    {
        var (startUtc, endUtc, _, _) = filter.GetUtcDateRanges();
        var canteenName = await GetCanteenNameAsync(filter.CanteenId);

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Where(o => o.CanteenId == filter.CanteenId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc && o.Status != "Cancelled")
            .ToListAsync();

        var totalOrders = orders.Count;
        var totalSales = orders.Sum(o => o.TotalAmount);

        // 24 Hour Traffic Heatmap
        var hourlyTraffic = new List<HourlyPeakTrafficStatDto>();
        for (int h = 0; h < 24; h++)
        {
            var hOrders = orders.Where(o => o.CreatedAt.Hour == h).ToList();
            var hSales = hOrders.Sum(o => o.TotalAmount);
            var hAov = hOrders.Count > 0 ? Math.Round(hSales / hOrders.Count, 2) : 0m;
            var hLabel = h == 0 ? "12 AM" : h < 12 ? $"{h} AM" : h == 12 ? "12 PM" : $"{h - 12} PM";

            hourlyTraffic.Add(new HourlyPeakTrafficStatDto
            {
                Hour = h,
                HourLabel = hLabel,
                OrdersCount = hOrders.Count,
                GrossSales = hSales,
                AverageOrderValue = hAov,
                TrafficPercentage = totalOrders > 0 ? Math.Round(((decimal)hOrders.Count / totalOrders) * 100m, 1) : 0m,
                IsPeakHour = false
            });
        }

        // Identify peak hour
        var peakHour = hourlyTraffic.OrderByDescending(x => x.OrdersCount).FirstOrDefault();
        if (peakHour != null && peakHour.OrdersCount > 0)
        {
            peakHour.IsPeakHour = true;
        }

        // Day of Week Distribution
        var dayOfWeekNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var dayOfWeekTraffic = new List<DayOfWeekPeakStatDto>();
        for (int d = 0; d < 7; d++)
        {
            var dayOrders = orders.Where(o => (int)o.CreatedAt.DayOfWeek == d).ToList();
            var daySales = dayOrders.Sum(o => o.TotalAmount);
            dayOfWeekTraffic.Add(new DayOfWeekPeakStatDto
            {
                DayOfWeekIndex = d,
                DayName = dayOfWeekNames[d],
                OrdersCount = dayOrders.Count,
                GrossSales = daySales,
                PercentageOfTotal = totalOrders > 0 ? Math.Round(((decimal)dayOrders.Count / totalOrders) * 100m, 1) : 0m
            });
        }

        var busiestDay = dayOfWeekTraffic.OrderByDescending(x => x.OrdersCount).FirstOrDefault()?.DayName ?? "N/A";

        // Table Utilization
        var tableUtilization = orders
            .Where(o => o.Table != null)
            .GroupBy(o => new { o.TableId, o.Table.TableNumber })
            .Select(g => new TableUtilizationPeakDto
            {
                TableId = g.Key.TableId,
                TableNumber = g.Key.TableNumber.ToString(),
                TotalOrdersServed = g.Count(),
                TotalRevenueGenerated = g.Sum(x => x.TotalAmount),
                UtilizationPercentage = totalOrders > 0 ? Math.Round(((decimal)g.Count() / totalOrders) * 100m, 1) : 0m
            })
            .OrderByDescending(x => x.TotalOrdersServed)
            .ToList();

        var avgOrdersPerHour = totalOrders > 0 ? Math.Round((decimal)totalOrders / 24m, 1) : 0m;
        var peakContribution = (peakHour != null && totalSales > 0) ? Math.Round((peakHour.GrossSales / totalSales) * 100m, 1) : 0m;

        return new PeakHourReportDto
        {
            CanteenId = filter.CanteenId,
            CanteenName = canteenName,
            Period = filter.Period,
            FromDate = startUtc,
            ToDate = endUtc,
            BusiestHourLabel = peakHour?.HourLabel ?? "N/A",
            BusiestHourOrdersCount = peakHour?.OrdersCount ?? 0,
            BusiestHourSales = peakHour?.GrossSales ?? 0m,
            BusiestDayOfWeek = busiestDay,
            AverageOrdersPerHour = avgOrdersPerHour,
            PeakHourRevenueContribution = peakContribution,
            HourlyTraffic = hourlyTraffic,
            DayOfWeekTraffic = dayOfWeekTraffic,
            TableUtilization = tableUtilization
        };
    }
}
