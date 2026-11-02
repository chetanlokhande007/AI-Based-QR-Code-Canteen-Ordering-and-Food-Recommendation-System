using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities;

/// <summary>
/// Bill of Materials (BOM) linking a finished menu product to its required raw culinary ingredients.
/// </summary>
public class Recipe
{
    public int RecipeId { get; set; }

    public int CanteenId { get; set; }

    public int ProductId { get; set; }

    public int ServingSize { get; set; } = 1;

    public string? PreparationNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Canteen Canteen { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}

