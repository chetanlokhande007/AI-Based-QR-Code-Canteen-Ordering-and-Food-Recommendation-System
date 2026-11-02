using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IInventoryService
{
    Task<IngredientDto> CreateIngredientAsync(int canteenId, CreateIngredientDto dto, int? ownerId = null);

    Task<IngredientDto?> UpdateIngredientAsync(int canteenId, int ingredientId, UpdateIngredientDto dto);

    Task<IngredientDto?> GetIngredientByIdAsync(int canteenId, int ingredientId);

    Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync(int canteenId, bool activeOnly = false);

    Task<IngredientDto?> AdjustStockAsync(int canteenId, int ingredientId, AdjustStockDto dto, int? ownerId = null);

    Task<IngredientWastageDto?> RecordWastageAsync(int canteenId, int ingredientId, RecordWastageDto dto, int? ownerId = null);

    Task<IEnumerable<StockTransactionDto>> GetTransactionsAsync(int canteenId, int? ingredientId = null, int limit = 50);

    Task<IEnumerable<IngredientWastageDto>> GetWastagesAsync(int canteenId, int? ingredientId = null, int limit = 50);

    Task<InventoryDashboardSummaryDto> GetDashboardSummaryAsync(int canteenId);
}

