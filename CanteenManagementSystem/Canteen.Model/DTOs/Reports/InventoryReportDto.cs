using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class InventoryReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Aggregate Stock & Waste Metrics
    public int TotalIngredientsTracked { get; set; }
    public decimal TotalInventoryValuation { get; set; } // Sum of CurrentStock * CostPerBaseUnit
    public int LowStockAlertsCount { get; set; }
    public decimal TotalWastageCost { get; set; }
    public decimal TotalWastageQuantity { get; set; }
    public int TotalStockTransactionsCount { get; set; }

    // Low Stock Alert Items
    public List<LowStockAlertItemDto> LowStockItems { get; set; } = new();

    // Wastage Breakdown by Reason
    public List<WastageReasonSummaryDto> WastageByReason { get; set; } = new();

    // Stock Movement Categories
    public List<StockMovementSummaryDto> StockMovementsByType { get; set; } = new();

    // Paginated Detailed Ingredients Inventory
    public ReportPagedResult<IngredientInventoryItemDto> Ingredients { get; set; } = new();
}

public class LowStockAlertItemDto
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = "g";
    public decimal CurrentStock { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal CostPerUnit { get; set; }
    public decimal DeficitQuantity { get; set; }
}

public class WastageReasonSummaryDto
{
    public string Reason { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    public decimal TotalQuantityWasted { get; set; }
    public decimal TotalCost { get; set; }
    public decimal PercentageOfCost { get; set; }
}

public class StockMovementSummaryDto
{
    public string TransactionType { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalQuantityMoved { get; set; }
}

public class IngredientInventoryItemDto
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = "g";
    public decimal CurrentStock { get; set; }
    public decimal ReservedStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public decimal CostPerBaseUnit { get; set; }
    public decimal Valuation { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
}

