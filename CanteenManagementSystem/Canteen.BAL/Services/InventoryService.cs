using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepo;
    private readonly ICanteenRepository? _canteenRepo;

    public InventoryService(IInventoryRepository inventoryRepo, ICanteenRepository? canteenRepo = null)
    {
        _inventoryRepo = inventoryRepo;
        _canteenRepo = canteenRepo;
    }

    public async Task<IngredientDto> CreateIngredientAsync(int canteenId, CreateIngredientDto dto, int? ownerId = null)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var trimmedName = dto.Name.Trim();
        if (await _inventoryRepo.IngredientNameExistsAsync(canteenId, trimmedName))
        {
            throw new InvalidOperationException($"An ingredient named '{trimmedName}' already exists in this canteen.");
        }

        var ingredient = new InventoryIngredient
        {
            CanteenId = canteenId,
            Name = trimmedName,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
            BaseUnit = NormalizeUnit(dto.BaseUnit),
            CurrentStock = Math.Max(0m, dto.InitialStock),
            ReservedStock = 0m,
            MinimumStockLevel = Math.Max(0m, dto.MinimumStockLevel),
            ReorderLevel = Math.Max(0m, dto.ReorderLevel),
            CostPerBaseUnit = Math.Max(0m, dto.CostPerBaseUnit),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return await _inventoryRepo.ExecuteInTransactionAsync(async () =>
        {
            var created = await _inventoryRepo.AddIngredientAsync(ingredient);

            if (created.CurrentStock > 0)
            {
                var transaction = new StockTransaction
                {
                    CanteenId = canteenId,
                    IngredientId = created.IngredientId,
                    TransactionType = "OpeningStock",
                    Quantity = created.CurrentStock,
                    PreviousStock = 0m,
                    NewStock = created.CurrentStock,
                    Reason = "Initial stock recorded upon ingredient creation",
                    CreatedByOwnerId = ownerId,
                    CreatedAt = DateTime.UtcNow
                };

                await _inventoryRepo.AddStockTransactionAsync(transaction);
            }

            return ToDto(created);
        });
    }

    public async Task<IngredientDto?> UpdateIngredientAsync(int canteenId, int ingredientId, UpdateIngredientDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, ingredientId, asNoTracking: false);
        if (ingredient == null)
        {
            return null;
        }

        var trimmedName = dto.Name.Trim();
        if (await _inventoryRepo.IngredientNameExistsAsync(canteenId, trimmedName, ingredientId))
        {
            throw new InvalidOperationException($"Another ingredient named '{trimmedName}' already exists in this canteen.");
        }

        ingredient.Name = trimmedName;
        ingredient.Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim();
        ingredient.BaseUnit = NormalizeUnit(dto.BaseUnit);
        ingredient.MinimumStockLevel = Math.Max(0m, dto.MinimumStockLevel);
        ingredient.ReorderLevel = Math.Max(0m, dto.ReorderLevel);
        ingredient.CostPerBaseUnit = Math.Max(0m, dto.CostPerBaseUnit);
        ingredient.IsActive = dto.IsActive;
        ingredient.UpdatedAt = DateTime.UtcNow;

        var updated = await _inventoryRepo.UpdateIngredientAsync(ingredient);
        return ToDto(updated);
    }

    public async Task<IngredientDto?> GetIngredientByIdAsync(int canteenId, int ingredientId)
    {
        var entity = await _inventoryRepo.GetIngredientByIdAsync(canteenId, ingredientId, asNoTracking: true);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync(int canteenId, bool activeOnly = false)
    {
        var list = await _inventoryRepo.GetAllIngredientsAsync(canteenId, activeOnly);
        return list.Select(ToDto);
    }

    public async Task<IngredientDto?> AdjustStockAsync(int canteenId, int ingredientId, AdjustStockDto dto, int? ownerId = null)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return await _inventoryRepo.ExecuteInTransactionAsync<IngredientDto?>(async () =>
        {
            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, ingredientId, asNoTracking: false);
            if (ingredient == null)
            {
                return null;
            }

            decimal previousStock = ingredient.CurrentStock;
            decimal newStock;
            decimal transactionQuantity;
            string transType = dto.OperationType;

            if (string.Equals(dto.OperationType, "Purchase", StringComparison.OrdinalIgnoreCase))
            {
                if (dto.Quantity <= 0)
                {
                    throw new ArgumentException("Purchase quantity must be greater than zero.", nameof(dto.Quantity));
                }
                transactionQuantity = dto.Quantity;
                newStock = previousStock + dto.Quantity;
            }
            else // Adjustment
            {
                if (dto.IsAbsoluteCount)
                {
                    if (dto.Quantity < 0)
                    {
                        throw new ArgumentException("Absolute stock count cannot be negative.", nameof(dto.Quantity));
                    }
                    newStock = dto.Quantity;
                    transactionQuantity = newStock - previousStock;
                }
                else
                {
                    transactionQuantity = dto.Quantity;
                    newStock = previousStock + dto.Quantity;
                }
            }

            if (newStock < 0)
            {
                throw new InvalidOperationException($"Cannot reduce stock below zero. Current stock: {previousStock}, attempted reduction to: {newStock}.");
            }

            if (newStock < ingredient.ReservedStock)
            {
                throw new InvalidOperationException($"Cannot adjust stock below currently reserved stock ({ingredient.ReservedStock} {ingredient.BaseUnit}).");
            }

            ingredient.CurrentStock = newStock;
            ingredient.UpdatedAt = DateTime.UtcNow;

            await _inventoryRepo.UpdateIngredientAsync(ingredient);

            var transaction = new StockTransaction
            {
                CanteenId = canteenId,
                IngredientId = ingredient.IngredientId,
                TransactionType = transType,
                Quantity = transactionQuantity,
                PreviousStock = previousStock,
                NewStock = newStock,
                ReferenceNumber = dto.ReferenceNumber?.Trim(),
                Reason = dto.Reason.Trim(),
                CreatedByOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            };

            await _inventoryRepo.AddStockTransactionAsync(transaction);

            return ToDto(ingredient);
        });
    }

    public async Task<IngredientWastageDto?> RecordWastageAsync(int canteenId, int ingredientId, RecordWastageDto dto, int? ownerId = null)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (dto.Quantity <= 0) throw new ArgumentException("Wastage quantity must be greater than zero.", nameof(dto.Quantity));

        return await _inventoryRepo.ExecuteInTransactionAsync<IngredientWastageDto?>(async () =>
        {
            var ingredient = await _inventoryRepo.GetIngredientByIdAsync(canteenId, ingredientId, asNoTracking: false);
            if (ingredient == null)
            {
                return null;
            }

            decimal available = ingredient.CurrentStock - ingredient.ReservedStock;
            if (dto.Quantity > available)
            {
                throw new InvalidOperationException(
                    $"Cannot record wastage of {dto.Quantity} {ingredient.BaseUnit}. Only {available} {ingredient.BaseUnit} is unreserved and available.");
            }

            decimal previousStock = ingredient.CurrentStock;
            decimal newStock = previousStock - dto.Quantity;

            ingredient.CurrentStock = newStock;
            ingredient.UpdatedAt = DateTime.UtcNow;
            await _inventoryRepo.UpdateIngredientAsync(ingredient);

            decimal cost = Math.Round(dto.Quantity * ingredient.CostPerBaseUnit, 2);

            var wastage = new IngredientWastage
            {
                CanteenId = canteenId,
                IngredientId = ingredient.IngredientId,
                Quantity = dto.Quantity,
                Reason = dto.Reason,
                Notes = dto.Notes?.Trim(),
                EstimatedCost = cost,
                RecordedByOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            };

            var createdWastage = await _inventoryRepo.AddWastageAsync(wastage);

            var transaction = new StockTransaction
            {
                CanteenId = canteenId,
                IngredientId = ingredient.IngredientId,
                TransactionType = "Wastage",
                Quantity = -dto.Quantity,
                PreviousStock = previousStock,
                NewStock = newStock,
                Reason = $"Wastage recorded: {dto.Reason}. {dto.Notes}".Trim(),
                CreatedByOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            };

            await _inventoryRepo.AddStockTransactionAsync(transaction);

            return new IngredientWastageDto
            {
                WastageId = createdWastage.WastageId,
                CanteenId = canteenId,
                IngredientId = ingredient.IngredientId,
                IngredientName = ingredient.Name,
                BaseUnit = ingredient.BaseUnit,
                Quantity = createdWastage.Quantity,
                Reason = createdWastage.Reason,
                Notes = createdWastage.Notes,
                EstimatedCost = createdWastage.EstimatedCost,
                RecordedByOwnerId = createdWastage.RecordedByOwnerId,
                CreatedAt = createdWastage.CreatedAt
            };
        });
    }

    public async Task<IEnumerable<StockTransactionDto>> GetTransactionsAsync(int canteenId, int? ingredientId = null, int limit = 50)
    {
        var list = await _inventoryRepo.GetTransactionsAsync(canteenId, ingredientId, limit);
        return list.Select(ToTransactionDto);
    }

    public async Task<IEnumerable<IngredientWastageDto>> GetWastagesAsync(int canteenId, int? ingredientId = null, int limit = 50)
    {
        var list = await _inventoryRepo.GetWastagesAsync(canteenId, ingredientId, limit);
        return list.Select(w => new IngredientWastageDto
        {
            WastageId = w.WastageId,
            CanteenId = w.CanteenId,
            IngredientId = w.IngredientId,
            IngredientName = w.Ingredient?.Name ?? string.Empty,
            BaseUnit = w.Ingredient?.BaseUnit ?? string.Empty,
            Quantity = w.Quantity,
            Reason = w.Reason,
            Notes = w.Notes,
            EstimatedCost = w.EstimatedCost,
            RecordedByOwnerId = w.RecordedByOwnerId,
            CreatedAt = w.CreatedAt
        });
    }

    public async Task<InventoryDashboardSummaryDto> GetDashboardSummaryAsync(int canteenId)
    {
        var allIngredients = (await _inventoryRepo.GetAllIngredientsAsync(canteenId, activeOnly: false)).ToList();
        var recentTransactions = (await _inventoryRepo.GetTransactionsAsync(canteenId, limit: 10)).ToList();

        int totalIngredients = allIngredients.Count;
        decimal totalStockValue = allIngredients.Sum(i => i.CurrentStock * i.CostPerBaseUnit);
        int lowStockCount = allIngredients.Count(i => i.IsActive && (i.CurrentStock - i.ReservedStock) <= i.MinimumStockLevel && (i.CurrentStock - i.ReservedStock) > 0);
        int outOfStockCount = allIngredients.Count(i => i.IsActive && (i.CurrentStock - i.ReservedStock) <= 0);
        decimal totalReservedStock = allIngredients.Sum(i => i.ReservedStock);

        var lowStockList = allIngredients
            .Where(i => i.IsActive && (i.CurrentStock - i.ReservedStock) <= i.MinimumStockLevel)
            .OrderBy(i => (i.CurrentStock - i.ReservedStock))
            .Take(10)
            .Select(ToDto)
            .ToList();

        // Calculate top consumed ingredients from transaction history (Consumption & Wastage)
        var consumedTransactions = await _inventoryRepo.GetTransactionsAsync(canteenId, limit: 200);
        var topConsumed = consumedTransactions
            .Where(t => t.TransactionType is "Consumption" or "Wastage")
            .GroupBy(t => new { t.IngredientId, Name = t.Ingredient?.Name ?? $"Item #{t.IngredientId}", Unit = t.Ingredient?.BaseUnit ?? "unit", Cost = t.Ingredient?.CostPerBaseUnit ?? 0m })
            .Select(g => new TopConsumedIngredientDto
            {
                IngredientId = g.Key.IngredientId,
                IngredientName = g.Key.Name,
                BaseUnit = g.Key.Unit,
                TotalQuantityConsumed = Math.Abs(g.Sum(x => x.Quantity)),
                EstimatedConsumedCost = Math.Round(Math.Abs(g.Sum(x => x.Quantity)) * g.Key.Cost, 2)
            })
            .OrderByDescending(x => x.TotalQuantityConsumed)
            .Take(5)
            .ToList();

        return new InventoryDashboardSummaryDto
        {
            TotalIngredients = totalIngredients,
            TotalStockValue = Math.Round(totalStockValue, 2),
            LowStockCount = lowStockCount,
            OutOfStockCount = outOfStockCount,
            TotalReservedStockUnits = totalReservedStock,
            LowStockIngredients = lowStockList,
            RecentTransactions = recentTransactions.Select(ToTransactionDto).ToList(),
            TopConsumedIngredients = topConsumed
        };
    }

    private static string NormalizeUnit(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return "g";
        var lower = unit.Trim().ToLowerInvariant();
        return lower switch
        {
            "kg" or "kilogram" or "kilograms" => "kg",
            "g" or "gram" or "grams" => "g",
            "l" or "litre" or "litres" or "liter" or "liters" => "L",
            "ml" or "millilitre" or "millilitres" or "milliliter" => "ml",
            "pcs" or "pc" or "piece" or "pieces" => "pcs",
            _ => "g"
        };
    }

    private static IngredientDto ToDto(InventoryIngredient i) => new()
    {
        IngredientId = i.IngredientId,
        CanteenId = i.CanteenId,
        Name = i.Name,
        Category = i.Category,
        BaseUnit = i.BaseUnit,
        CurrentStock = i.CurrentStock,
        ReservedStock = i.ReservedStock,
        AvailableStock = i.AvailableStock,
        MinimumStockLevel = i.MinimumStockLevel,
        ReorderLevel = i.ReorderLevel,
        CostPerBaseUnit = i.CostPerBaseUnit,
        IsActive = i.IsActive,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };

    private static StockTransactionDto ToTransactionDto(StockTransaction t) => new()
    {
        TransactionId = t.TransactionId,
        CanteenId = t.CanteenId,
        IngredientId = t.IngredientId,
        IngredientName = t.Ingredient?.Name ?? string.Empty,
        BaseUnit = t.Ingredient?.BaseUnit ?? string.Empty,
        TransactionType = t.TransactionType,
        Quantity = t.Quantity,
        PreviousStock = t.PreviousStock,
        NewStock = t.NewStock,
        OrderId = t.OrderId,
        ReferenceNumber = t.ReferenceNumber,
        Reason = t.Reason,
        CreatedByOwnerId = t.CreatedByOwnerId,
        CreatedAt = t.CreatedAt
    };
}

