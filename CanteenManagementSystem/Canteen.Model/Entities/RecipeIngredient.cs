namespace Canteen.Model.Entities;

/// <summary>
/// An ingredient line item within a Recipe defining required quantity per portion.
/// </summary>
public class RecipeIngredient
{
    public int RecipeIngredientId { get; set; }

    public int RecipeId { get; set; }

    public int IngredientId { get; set; }

    /// <summary>Quantity required per serving in standard base units (e.g. 200g, 20ml, 1pcs).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Display unit (e.g. g, ml, pcs).</summary>
    public string Unit { get; set; } = "g";

    // Navigation properties
    public Recipe Recipe { get; set; } = null!;

    public InventoryIngredient Ingredient { get; set; } = null!;
}

