using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class CreateRecipeIngredientDto
{
    [Required(ErrorMessage = "Ingredient is required.")]
    public int IngredientId { get; set; }

    [Required(ErrorMessage = "Quantity is required.")]
    [Range(0.001, 100_000, ErrorMessage = "Quantity must be greater than 0.")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    [RegularExpression("^(g|kg|ml|L|pcs)$", ErrorMessage = "Unit must be one of: g, kg, ml, L, pcs.")]
    public string Unit { get; set; } = "g";
}

public class CreateRecipeDto
{
    [Required(ErrorMessage = "Product is required.")]
    public int ProductId { get; set; }

    [Range(1, 1000, ErrorMessage = "Serving size must be between 1 and 1000.")]
    public int ServingSize { get; set; } = 1;

    [StringLength(1000, ErrorMessage = "Preparation notes cannot exceed 1000 characters.")]
    public string? PreparationNotes { get; set; }

    [Required(ErrorMessage = "At least one ingredient is required.")]
    [MinLength(1, ErrorMessage = "A recipe must contain at least one ingredient.")]
    public List<CreateRecipeIngredientDto> Ingredients { get; set; } = new();
}

public class UpdateRecipeDto
{
    [Range(1, 1000, ErrorMessage = "Serving size must be between 1 and 1000.")]
    public int ServingSize { get; set; } = 1;

    [StringLength(1000, ErrorMessage = "Preparation notes cannot exceed 1000 characters.")]
    public string? PreparationNotes { get; set; }

    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "At least one ingredient is required.")]
    [MinLength(1, ErrorMessage = "A recipe must contain at least one ingredient.")]
    public List<CreateRecipeIngredientDto> Ingredients { get; set; } = new();
}

public class RecipeIngredientDto
{
    public int RecipeIngredientId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string IngredientBaseUnit { get; set; } = string.Empty;
    public decimal CostPerBaseUnit { get; set; }
    public decimal NormalizedQuantityInBaseUnit { get; set; }
    public decimal LineCost { get; set; }
    public decimal AvailableStock { get; set; }
    public int MaxPortionsForThisIngredient { get; set; }
}

public class RecipeDto
{
    public int RecipeId { get; set; }
    public int CanteenId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public string ProductCategory { get; set; } = string.Empty;
    public string FoodType { get; set; } = "Veg";
    public int ServingSize { get; set; } = 1;
    public string? PreparationNotes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    public decimal TotalFoodCostPerServing { get; set; }
    public decimal GrossMarginAmount { get; set; }
    public decimal GrossMarginPercentage { get; set; }
    public string MarginStatus { get; set; } = "Healthy";
    public int MaxPortionsAvailable { get; set; }
    public int? LimitingIngredientId { get; set; }
    public string? LimitingIngredientName { get; set; }
}

public class RecipeCostingSummaryDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FoodType { get; set; } = "Veg";
    public int? RecipeId { get; set; }
    public bool HasRecipe { get; set; }
    public decimal FoodCostPerServing { get; set; }
    public decimal GrossMarginAmount { get; set; }
    public decimal GrossMarginPercentage { get; set; }
    public string MarginStatus { get; set; } = "Healthy"; // High (>=65%), Healthy (45-64%), Low (20-44%), Critical (<20%)
    public int MaxPortionsAvailable { get; set; }
    public string? LimitingIngredient { get; set; }
}

public class IngredientAvailabilityItemDto
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal RequiredPerPortion { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal AvailableStock { get; set; }
    public string BaseUnit { get; set; } = string.Empty;
    public int PortionsPossible { get; set; }
}

public class RecipeAvailabilityDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int MaxPortionsAvailable { get; set; }
    public int? LimitingIngredientId { get; set; }
    public string? LimitingIngredientName { get; set; }
    public List<IngredientAvailabilityItemDto> IngredientBreakdown { get; set; } = new();
}

