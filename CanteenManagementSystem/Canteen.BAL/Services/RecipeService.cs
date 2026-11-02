using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class RecipeService : IRecipeService
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IProductRepository _productRepo;

    public RecipeService(
        IRecipeRepository recipeRepo,
        IInventoryRepository inventoryRepo,
        IProductRepository productRepo)
    {
        _recipeRepo = recipeRepo;
        _inventoryRepo = inventoryRepo;
        _productRepo = productRepo;
    }

    public async Task<RecipeDto> CreateRecipeAsync(int canteenId, CreateRecipeDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (dto.ServingSize < 1) throw new ArgumentException("Serving size must be at least 1 portion.", nameof(dto.ServingSize));
        if (dto.Ingredients == null || !dto.Ingredients.Any())
        {
            throw new ArgumentException("A recipe must contain at least one ingredient.", nameof(dto.Ingredients));
        }

        // Verify product exists and belongs to this canteen
        var product = await _productRepo.GetByIdAsync(canteenId, dto.ProductId);
        if (product == null)
        {
            throw new KeyNotFoundException($"Product with ID {dto.ProductId} was not found in canteen {canteenId}.");
        }

        // Check if recipe already exists for this product
        if (await _recipeRepo.ProductHasRecipeAsync(canteenId, dto.ProductId))
        {
            throw new InvalidOperationException($"A recipe already exists for product '{product.Name}' (ID {dto.ProductId}). Edit the existing recipe instead.");
        }

        // Prevent duplicate ingredients in the same recipe
        if (dto.Ingredients.GroupBy(i => i.IngredientId).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Recipe cannot contain duplicate ingredients. Combine the quantities into a single line item.");
        }

        // Validate each ingredient exists, belongs to the canteen, and has compatible units
        var recipeIngredients = new List<RecipeIngredient>();
        foreach (var item in dto.Ingredients)
        {
            if (item.Quantity <= 0)
            {
                throw new ArgumentException($"Quantity for ingredient ID {item.IngredientId} must be greater than zero.");
            }

            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, item.IngredientId);
            if (ingredient == null)
            {
                throw new KeyNotFoundException($"Ingredient with ID {item.IngredientId} does not belong to canteen {canteenId}.");
            }

            var normUnit = NormalizeUnit(item.Unit);
            // Validates unit compatibility; throws if incompatible
            _ = GetUnitConversionMultiplier(normUnit, ingredient.BaseUnit, ingredient.Name);

            recipeIngredients.Add(new RecipeIngredient
            {
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                Unit = normUnit
            });
        }

        var recipe = new Recipe
        {
            CanteenId = canteenId,
            ProductId = dto.ProductId,
            ServingSize = dto.ServingSize,
            PreparationNotes = dto.PreparationNotes?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Ingredients = recipeIngredients
        };

        var created = await _recipeRepo.AddAsync(recipe);

        // Fetch reloaded recipe with full navigations
        var fullRecipe = await _recipeRepo.GetByIdAsync(canteenId, created.RecipeId);
        return MapToRecipeDto(fullRecipe!);
    }

    public async Task<RecipeDto> UpdateRecipeAsync(int canteenId, int recipeId, UpdateRecipeDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (dto.ServingSize < 1) throw new ArgumentException("Serving size must be at least 1 portion.", nameof(dto.ServingSize));
        if (dto.Ingredients == null || !dto.Ingredients.Any())
        {
            throw new ArgumentException("A recipe must contain at least one ingredient.", nameof(dto.Ingredients));
        }

        var existing = await _recipeRepo.GetByIdAsync(canteenId, recipeId, asNoTracking: false);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Recipe with ID {recipeId} was not found in canteen {canteenId}.");
        }

        if (dto.Ingredients.GroupBy(i => i.IngredientId).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Recipe cannot contain duplicate ingredients. Combine the quantities into a single line item.");
        }

        // Validate each ingredient
        var updatedIngredients = new List<RecipeIngredient>();
        foreach (var item in dto.Ingredients)
        {
            if (item.Quantity <= 0)
            {
                throw new ArgumentException($"Quantity for ingredient ID {item.IngredientId} must be greater than zero.");
            }

            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, item.IngredientId);
            if (ingredient == null)
            {
                throw new KeyNotFoundException($"Ingredient with ID {item.IngredientId} does not belong to canteen {canteenId}.");
            }

            var normUnit = NormalizeUnit(item.Unit);
            _ = GetUnitConversionMultiplier(normUnit, ingredient.BaseUnit, ingredient.Name);

            updatedIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipeId,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                Unit = normUnit
            });
        }

        existing.ServingSize = dto.ServingSize;
        existing.PreparationNotes = dto.PreparationNotes?.Trim();
        existing.IsActive = dto.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        // Replace ingredients collection
        existing.Ingredients.Clear();
        foreach (var ri in updatedIngredients)
        {
            existing.Ingredients.Add(ri);
        }

        await _recipeRepo.UpdateAsync(existing);

        var fullRecipe = await _recipeRepo.GetByIdAsync(canteenId, recipeId);
        return MapToRecipeDto(fullRecipe!);
    }

    public async Task<RecipeDto?> GetRecipeByIdAsync(int canteenId, int recipeId)
    {
        var recipe = await _recipeRepo.GetByIdAsync(canteenId, recipeId);
        return recipe == null ? null : MapToRecipeDto(recipe);
    }

    public async Task<RecipeDto?> GetRecipeByProductIdAsync(int canteenId, int productId)
    {
        var recipe = await _recipeRepo.GetByProductIdAsync(canteenId, productId);
        return recipe == null ? null : MapToRecipeDto(recipe);
    }

    public async Task<IEnumerable<RecipeDto>> GetAllRecipesAsync(int canteenId, bool activeOnly = false)
    {
        var recipes = await _recipeRepo.GetAllByCanteenAsync(canteenId, activeOnly);
        return recipes.Select(MapToRecipeDto).ToList();
    }

    public async Task<bool> DeleteRecipeAsync(int canteenId, int recipeId)
    {
        return await _recipeRepo.DeleteAsync(canteenId, recipeId);
    }

    public async Task<IEnumerable<RecipeCostingSummaryDto>> GetCostingSummariesAsync(int canteenId)
    {
        var products = await _productRepo.GetAllByCanteenAsync(canteenId);
        var recipes = await _recipeRepo.GetAllByCanteenAsync(canteenId, activeOnly: false);
        var recipeMap = recipes.ToDictionary(r => r.ProductId);

        var summaries = new List<RecipeCostingSummaryDto>();

        foreach (var product in products.OrderBy(p => p.Name))
        {
            if (recipeMap.TryGetValue(product.ProductId, out var recipe))
            {
                var dto = MapToRecipeDto(recipe);
                summaries.Add(new RecipeCostingSummaryDto
                {
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    SellingPrice = product.Price,
                    Category = recipe.Product.Category?.Name ?? "General",
                    FoodType = product.FoodType,
                    RecipeId = recipe.RecipeId,
                    HasRecipe = true,
                    FoodCostPerServing = dto.TotalFoodCostPerServing,
                    GrossMarginAmount = dto.GrossMarginAmount,
                    GrossMarginPercentage = dto.GrossMarginPercentage,
                    MarginStatus = dto.MarginStatus,
                    MaxPortionsAvailable = dto.MaxPortionsAvailable,
                    LimitingIngredient = dto.LimitingIngredientName
                });
            }
            else
            {
                summaries.Add(new RecipeCostingSummaryDto
                {
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    SellingPrice = product.Price,
                    Category = "General",
                    FoodType = product.FoodType,
                    RecipeId = null,
                    HasRecipe = false,
                    FoodCostPerServing = 0m,
                    GrossMarginAmount = product.Price,
                    GrossMarginPercentage = product.Price > 0 ? 100m : 0m,
                    MarginStatus = "No Recipe",
                    MaxPortionsAvailable = product.StockQuantity,
                    LimitingIngredient = null
                });
            }
        }

        return summaries;
    }

    public async Task<RecipeAvailabilityDto?> GetPortionAvailabilityAsync(int canteenId, int productId)
    {
        var recipe = await _recipeRepo.GetByProductIdAsync(canteenId, productId);
        if (recipe == null) return null;

        var dto = MapToRecipeDto(recipe);

        return new RecipeAvailabilityDto
        {
            ProductId = productId,
            ProductName = recipe.Product?.Name ?? string.Empty,
            MaxPortionsAvailable = dto.MaxPortionsAvailable,
            LimitingIngredientId = dto.LimitingIngredientId,
            LimitingIngredientName = dto.LimitingIngredientName,
            IngredientBreakdown = dto.Ingredients.Select(i => new IngredientAvailabilityItemDto
            {
                IngredientId = i.IngredientId,
                IngredientName = i.IngredientName,
                RequiredPerPortion = Math.Round(i.NormalizedQuantityInBaseUnit / (recipe.ServingSize > 0 ? recipe.ServingSize : 1), 3),
                Unit = i.IngredientBaseUnit,
                AvailableStock = i.AvailableStock,
                BaseUnit = i.IngredientBaseUnit,
                PortionsPossible = i.MaxPortionsForThisIngredient
            }).ToList()
        };
    }

    // =========================================================================
    // Private Helper & Calculation Methods
    // =========================================================================

    public static string NormalizeUnit(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return "g";
        return unit.Trim().ToLowerInvariant() switch
        {
            "kg" or "kilogram" or "kilograms" => "kg",
            "g" or "gram" or "grams" => "g",
            "l" or "litre" or "litres" or "liter" or "liters" => "L",
            "ml" or "millilitre" or "millilitres" or "milliliter" or "milliliters" => "ml",
            "pcs" or "pc" or "piece" or "pieces" => "pcs",
            _ => "g"
        };
    }

    public static decimal GetUnitConversionMultiplier(string recipeUnit, string baseUnit, string? ingredientName = null)
    {
        var normRecipe = NormalizeUnit(recipeUnit);
        var normBase = NormalizeUnit(baseUnit);

        if (normRecipe == normBase) return 1.0m;

        // Weight conversions
        if (normRecipe == "kg" && normBase == "g") return 1000m;
        if (normRecipe == "g" && normBase == "kg") return 0.001m;

        // Volume conversions
        if (normRecipe == "L" && normBase == "ml") return 1000m;
        if (normRecipe == "ml" && normBase == "L") return 0.001m;

        var namePart = string.IsNullOrWhiteSpace(ingredientName) ? "" : $" for ingredient '{ingredientName}'";
        throw new InvalidOperationException($"Cannot convert recipe unit '{recipeUnit}' to base unit '{baseUnit}'{namePart}. Weight, volume, and discrete units cannot be converted into one another.");
    }

    private static RecipeDto MapToRecipeDto(Recipe recipe)
    {
        var servingSize = recipe.ServingSize > 0 ? recipe.ServingSize : 1;
        var ingredientDtos = new List<RecipeIngredientDto>();

        decimal totalBatchCost = 0m;
        int minPortions = int.MaxValue;
        int? limitingIngredientId = null;
        string? limitingIngredientName = null;

        foreach (var ri in recipe.Ingredients.OrderBy(ri => ri.IngredientId))
        {
            var ingredient = ri.Ingredient;
            var ingredientBaseUnit = ingredient?.BaseUnit ?? ri.Unit;
            var costPerBase = ingredient?.CostPerBaseUnit ?? 0m;
            var availableStock = ingredient?.AvailableStock ?? 0m;

            decimal multiplier = 1.0m;
            try
            {
                multiplier = GetUnitConversionMultiplier(ri.Unit, ingredientBaseUnit, ingredient?.Name);
            }
            catch
            {
                multiplier = 1.0m;
            }

            var normalizedQty = ri.Quantity * multiplier;
            var lineCost = Math.Round(normalizedQty * costPerBase, 2);
            totalBatchCost += lineCost;

            var requiredPerPortion = normalizedQty / servingSize;
            int portionsForIngredient = requiredPerPortion > 0
                ? (int)Math.Floor(availableStock / requiredPerPortion)
                : 999999;

            if (portionsForIngredient < minPortions)
            {
                minPortions = portionsForIngredient;
                limitingIngredientId = ri.IngredientId;
                limitingIngredientName = ingredient?.Name ?? $"Ingredient #{ri.IngredientId}";
            }

            ingredientDtos.Add(new RecipeIngredientDto
            {
                RecipeIngredientId = ri.RecipeIngredientId,
                IngredientId = ri.IngredientId,
                IngredientName = ingredient?.Name ?? $"Ingredient #{ri.IngredientId}",
                Category = ingredient?.Category ?? "General",
                Quantity = ri.Quantity,
                Unit = ri.Unit,
                IngredientBaseUnit = ingredientBaseUnit,
                CostPerBaseUnit = costPerBase,
                NormalizedQuantityInBaseUnit = Math.Round(normalizedQty, 3),
                LineCost = lineCost,
                AvailableStock = availableStock,
                MaxPortionsForThisIngredient = portionsForIngredient
            });
        }

        if (!recipe.Ingredients.Any())
        {
            minPortions = 0;
        }

        var totalFoodCostPerServing = Math.Round(totalBatchCost / servingSize, 2);
        var productPrice = recipe.Product?.Price ?? 0m;
        var grossMarginAmount = Math.Round(productPrice - totalFoodCostPerServing, 2);
        var grossMarginPercentage = productPrice > 0
            ? Math.Round((grossMarginAmount / productPrice) * 100m, 2)
            : 0m;

        string marginStatus;
        if (productPrice <= 0)
        {
            marginStatus = "Critical";
        }
        else if (grossMarginPercentage >= 65m)
        {
            marginStatus = "High";
        }
        else if (grossMarginPercentage >= 45m)
        {
            marginStatus = "Healthy";
        }
        else if (grossMarginPercentage >= 20m)
        {
            marginStatus = "Low";
        }
        else
        {
            marginStatus = "Critical";
        }

        return new RecipeDto
        {
            RecipeId = recipe.RecipeId,
            CanteenId = recipe.CanteenId,
            ProductId = recipe.ProductId,
            ProductName = recipe.Product?.Name ?? string.Empty,
            ProductPrice = productPrice,
            ProductCategory = recipe.Product?.Category?.Name ?? "General",
            FoodType = recipe.Product?.FoodType ?? "Veg",
            ServingSize = servingSize,
            PreparationNotes = recipe.PreparationNotes,
            IsActive = recipe.IsActive,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt,
            Ingredients = ingredientDtos,
            TotalFoodCostPerServing = totalFoodCostPerServing,
            GrossMarginAmount = grossMarginAmount,
            GrossMarginPercentage = grossMarginPercentage,
            MarginStatus = marginStatus,
            MaxPortionsAvailable = minPortions == int.MaxValue ? 0 : Math.Max(0, minPortions),
            LimitingIngredientId = limitingIngredientId,
            LimitingIngredientName = limitingIngredientName
        };
    }
}
