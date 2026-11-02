using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(int canteenId, int recipeId, bool asNoTracking = true);

    Task<Recipe?> GetByProductIdAsync(int canteenId, int productId, bool asNoTracking = true);

    Task<IEnumerable<Recipe>> GetAllByCanteenAsync(int canteenId, bool activeOnly = false);

    Task<Recipe> AddAsync(Recipe recipe);

    Task<Recipe> UpdateAsync(Recipe recipe);

    Task<bool> DeleteAsync(int canteenId, int recipeId);

    Task<bool> ProductHasRecipeAsync(int canteenId, int productId, int? excludeRecipeId = null);

    Task<bool> IsIngredientUsedInAnyRecipeAsync(int canteenId, int ingredientId);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation);
}

