using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class ProductReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Summary Highlights
    public int TotalProductsInCatalog { get; set; }
    public int ActiveProductsCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int TotalUnitsSold { get; set; }
    public decimal TotalRevenueGenerated { get; set; }
    public string TopSellingProductName { get; set; } = string.Empty;

    // Leaderboards
    public List<ProductPerformanceDetailDto> TopSellingProducts { get; set; } = new();
    public List<ProductPerformanceDetailDto> SlowMovingProducts { get; set; } = new();
    public List<CategoryPerformanceStatDto> CategoryPerformance { get; set; } = new();

    // Paginated Detailed Product Catalog Performance
    public ReportPagedResult<ProductPerformanceDetailDto> Products { get; set; } = new();
}

public class ProductPerformanceDetailDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string FoodType { get; set; } = "Veg"; // Veg or Non-Veg
    public decimal UnitPrice { get; set; }
    public int QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public int CurrentStock { get; set; }
    public bool IsAvailable { get; set; }
    public decimal RevenueSharePercentage { get; set; }
}

public class CategoryPerformanceStatDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int UnitsSold { get; set; }
    public decimal TotalSales { get; set; }
    public decimal RevenueSharePercentage { get; set; }
}

