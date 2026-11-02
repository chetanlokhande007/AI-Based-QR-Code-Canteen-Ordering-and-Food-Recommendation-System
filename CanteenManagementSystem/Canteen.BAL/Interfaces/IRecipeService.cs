using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IRecipeService
{
    Task<RecipeDto> CreateRecipeAsync(int canteenId, CreateRecipeDto dto);

    Task<RecipeDto> UpdateRecipeAsync(int canteenId, int recipeId, UpdateRecipeDto dto);

    Task<RecipeDto?> GetRecipeByIdAsync(int canteenId, int recipeId);

    Task<RecipeDto?> GetRecipeByProductIdAsync(int canteenId, int productId);

    Task<IEnumerable<RecipeDto>> GetAllRecipesAsync(int canteenId, bool activeOnly = false);

    Task<bool> DeleteRecipeAsync(int canteenId, int recipeId);

    Task<IEnumerable<RecipeCostingSummaryDto>> GetCostingSummariesAsync(int canteenId);

    Task<RecipeAvailabilityDto?> GetPortionAvailabilityAsync(int canteenId, int productId);
}

