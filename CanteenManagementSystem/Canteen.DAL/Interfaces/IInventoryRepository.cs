using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryIngredient?> GetIngredientByIdAsync(int canteenId, int ingredientId, bool asNoTracking = true);

    Task<IEnumerable<InventoryIngredient>> GetAllIngredientsAsync(int canteenId, bool activeOnly = false);

    Task<IEnumerable<InventoryIngredient>> GetLowStockIngredientsAsync(int canteenId);

    Task<bool> IngredientNameExistsAsync(int canteenId, string name, int? excludeId = null);

    Task<InventoryIngredient> AddIngredientAsync(InventoryIngredient ingredient);

    Task<InventoryIngredient> UpdateIngredientAsync(InventoryIngredient ingredient);

    Task<StockTransaction> AddStockTransactionAsync(StockTransaction transaction);

    Task<IEnumerable<StockTransaction>> GetTransactionsAsync(int canteenId, int? ingredientId = null, int limit = 50);

    Task<IngredientWastage> AddWastageAsync(IngredientWastage wastage);

    Task<IEnumerable<IngredientWastage>> GetWastagesAsync(int canteenId, int? ingredientId = null, int limit = 50);

    Task<StockReservation> AddReservationAsync(StockReservation reservation);

    Task<StockReservation?> GetReservationByOrderIdAsync(int canteenId, int orderId, bool asNoTracking = true);

    Task<StockReservation> UpdateReservationAsync(StockReservation reservation);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation);
}
