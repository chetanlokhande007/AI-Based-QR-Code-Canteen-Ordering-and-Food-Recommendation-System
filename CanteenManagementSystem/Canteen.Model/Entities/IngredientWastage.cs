using System;

namespace Canteen.Model.Entities;

/// <summary>
/// Tracks kitchen ingredient spoilage, preparation waste, expiration, or transit damage.
/// </summary>
public class IngredientWastage
{
    public int WastageId { get; set; }

    public int CanteenId { get; set; }

    public int IngredientId { get; set; }

    /// <summary>Quantity wasted in base units (g, ml, pcs).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Reason code: Expired, Spoiled, PrepWaste, Damaged, Burnt.</summary>
    public string Reason { get; set; } = "Spoiled";

    public string? Notes { get; set; }

    /// <summary>Financial impact calculated at time of wastage (Quantity * CostPerBaseUnit).</summary>
    public decimal EstimatedCost { get; set; }

    public int? RecordedByOwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Canteen Canteen { get; set; } = null!;

    public InventoryIngredient Ingredient { get; set; } = null!;
}

