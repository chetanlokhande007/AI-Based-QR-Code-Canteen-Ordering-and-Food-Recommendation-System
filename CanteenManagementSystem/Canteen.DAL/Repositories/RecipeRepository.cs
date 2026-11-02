using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class RecipeRepository : IRecipeRepository
{
    private readonly CanteenDbContext _context;

    public RecipeRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<Recipe?> GetByIdAsync(int canteenId, int recipeId, bool asNoTracking = true)
    {
        var query = _context.Recipes
            .Include(r => r.Product)
                .ThenInclude(p => p.Category)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .Where(r => r.CanteenId == canteenId && r.RecipeId == recipeId);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<Recipe?> GetByProductIdAsync(int canteenId, int productId, bool asNoTracking = true)
    {
        var query = _context.Recipes
            .Include(r => r.Product)
                .ThenInclude(p => p.Category)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .Where(r => r.CanteenId == canteenId && r.ProductId == productId);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Recipe>> GetAllByCanteenAsync(int canteenId, bool activeOnly = false)
    {
        var query = _context.Recipes
            .AsNoTracking()
            .Include(r => r.Product)
                .ThenInclude(p => p.Category)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .Where(r => r.CanteenId == canteenId);

        if (activeOnly)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query.OrderBy(r => r.Product.Name).ToListAsync();
    }

    public async Task<Recipe> AddAsync(Recipe recipe)
    {
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        return recipe;
    }

    public async Task<Recipe> UpdateAsync(Recipe recipe)
    {
        _context.Recipes.Update(recipe);
        await _context.SaveChangesAsync();
        return recipe;
    }

    public async Task<bool> DeleteAsync(int canteenId, int recipeId)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.CanteenId == canteenId && r.RecipeId == recipeId);

        if (recipe == null) return false;

        _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ProductHasRecipeAsync(int canteenId, int productId, int? excludeRecipeId = null)
    {
        var query = _context.Recipes
            .Where(r => r.CanteenId == canteenId && r.ProductId == productId);

        if (excludeRecipeId.HasValue)
        {
            query = query.Where(r => r.RecipeId != excludeRecipeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<bool> IsIngredientUsedInAnyRecipeAsync(int canteenId, int ingredientId)
    {
        return await _context.RecipeIngredients
            .AnyAsync(ri => ri.IngredientId == ingredientId && ri.Recipe.CanteenId == canteenId && ri.Recipe.IsActive);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}

