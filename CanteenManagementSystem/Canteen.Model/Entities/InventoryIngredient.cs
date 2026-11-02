using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities;

/// <summary>
/// Represents a raw culinary ingredient or stock item managed by a specific canteen.
/// </summary>
public class InventoryIngredient
{
    public int IngredientId { get; set; }

    public int CanteenId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = "General"; // e.g. Grains, Dairy, Produce, Meat, Spices, Oils

    public string BaseUnit { get; set; } = "g"; // "g", "kg", "ml", "L", "pcs"

    /// <summary>Total physical stock currently held on premises.</summary>
    public decimal CurrentStock { get; set; }

    /// <summary>Stock quantity locked for pending customer orders not yet prepared.</summary>
    public decimal ReservedStock { get; set; }

    /// <summary>Calculated unreserved stock available for new orders.</summary>
    public decimal AvailableStock => Math.Max(0m, CurrentStock - ReservedStock);

    /// <summary>Threshold below which the ingredient is marked as Low Stock.</summary>
    public decimal MinimumStockLevel { get; set; }

    /// <summary>Recommended reorder benchmark level.</summary>
    public decimal ReorderLevel { get; set; }

    /// <summary>Cost per base unit in local currency (₹ per g, ₹ per ml, or ₹ per piece).</summary>
    public decimal CostPerBaseUnit { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Canteen Canteen { get; set; } = null!;

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();

    public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();

    public ICollection<IngredientWastage> Wastages { get; set; } = new List<IngredientWastage>();

    public ICollection<StockReservationItem> ReservationItems { get; set; } = new List<StockReservationItem>();
}

