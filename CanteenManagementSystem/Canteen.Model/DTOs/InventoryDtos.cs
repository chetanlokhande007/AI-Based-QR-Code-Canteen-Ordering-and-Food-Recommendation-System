using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class CreateIngredientDto
{
    [Required(ErrorMessage = "Ingredient name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters.")]
    public string Category { get; set; } = "General";

    [Required(ErrorMessage = "Base unit is required.")]
    [RegularExpression("^(g|kg|ml|L|pcs)$", ErrorMessage = "Base unit must be one of: g, kg, ml, L, pcs.")]
    public string BaseUnit { get; set; } = "g";

    [Range(0, 1_000_000, ErrorMessage = "Initial stock must be greater than or equal to 0.")]
    public decimal InitialStock { get; set; } = 0;

    [Range(0, 1_000_000, ErrorMessage = "Minimum stock level must be greater than or equal to 0.")]
    public decimal MinimumStockLevel { get; set; } = 0;

    [Range(0, 1_000_000, ErrorMessage = "Reorder level must be greater than or equal to 0.")]
    public decimal ReorderLevel { get; set; } = 0;

    [Range(0, 1_000_000, ErrorMessage = "Cost per base unit must be greater than or equal to 0.")]
    public decimal CostPerBaseUnit { get; set; } = 0;
}

public class UpdateIngredientDto
{
    [Required(ErrorMessage = "Ingredient name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters.")]
    public string Category { get; set; } = "General";

    [Required(ErrorMessage = "Base unit is required.")]
    [RegularExpression("^(g|kg|ml|L|pcs)$", ErrorMessage = "Base unit must be one of: g, kg, ml, L, pcs.")]
    public string BaseUnit { get; set; } = "g";

    [Range(0, 1_000_000, ErrorMessage = "Minimum stock level must be greater than or equal to 0.")]
    public decimal MinimumStockLevel { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Reorder level must be greater than or equal to 0.")]
    public decimal ReorderLevel { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Cost per base unit must be greater than or equal to 0.")]
    public decimal CostPerBaseUnit { get; set; }

    public bool IsActive { get; set; } = true;
}

public class IngredientDto
{
    public int IngredientId { get; set; }
    public int CanteenId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReservedStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal CostPerBaseUnit { get; set; }
    public decimal TotalStockValue => Math.Round(CurrentStock * CostPerBaseUnit, 2);
    public bool IsLowStock => AvailableStock <= MinimumStockLevel && AvailableStock > 0;
    public bool IsOutOfStock => AvailableStock <= 0;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AdjustStockDto
{
    [Required]
    [RegularExpression("^(Purchase|Adjustment)$", ErrorMessage = "Adjustment type must be either 'Purchase' (add stock) or 'Adjustment' (set/adjust stock).")]
    public string OperationType { get; set; } = "Purchase"; // "Purchase" (positive increment) or "Adjustment" (set absolute or manual delta)

    [Required(ErrorMessage = "Quantity is required.")]
    public decimal Quantity { get; set; }

    public bool IsAbsoluteCount { get; set; } = false; // If true, replaces CurrentStock with Quantity

    [Required(ErrorMessage = "Reason for stock adjustment is required.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Reason must be between 3 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ReferenceNumber { get; set; } // PO #, Receipt #, or Invoice #
}

public class RecordWastageDto
{
    [Required(ErrorMessage = "Quantity is required.")]
    [Range(0.001, 1_000_000, ErrorMessage = "Quantity must be greater than 0.")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "Reason code is required.")]
    [RegularExpression("^(Expired|Spoiled|PrepWaste|Damaged|Burnt)$", ErrorMessage = "Reason must be one of: Expired, Spoiled, PrepWaste, Damaged, Burnt.")]
    public string Reason { get; set; } = "Spoiled";

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class IngredientWastageDto
{
    public int WastageId { get; set; }
    public int CanteenId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal EstimatedCost { get; set; }
    public int? RecordedByOwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StockTransactionDto
{
    public int TransactionId { get; set; }
    public int CanteenId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal NewStock { get; set; }
    public int? OrderId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? CreatedByOwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InventoryDashboardSummaryDto
{
    public int TotalIngredients { get; set; }
    public decimal TotalStockValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public decimal TotalReservedStockUnits { get; set; }
    public List<IngredientDto> LowStockIngredients { get; set; } = new();
    public List<StockTransactionDto> RecentTransactions { get; set; } = new();
    public List<TopConsumedIngredientDto> TopConsumedIngredients { get; set; } = new();
}

public class TopConsumedIngredientDto
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
    public decimal TotalQuantityConsumed { get; set; }
    public decimal EstimatedConsumedCost { get; set; }
}

