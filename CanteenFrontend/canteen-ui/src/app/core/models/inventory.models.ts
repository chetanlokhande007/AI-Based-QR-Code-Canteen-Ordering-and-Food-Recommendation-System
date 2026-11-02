export type BaseUnit = 'g' | 'kg' | 'ml' | 'L' | 'pcs';

export interface IngredientDto {
  ingredientId: number;
  canteenId: number;
  name: string;
  category: string;
  baseUnit: BaseUnit;
  currentStock: number;
  reservedStock: number;
  availableStock: number;
  minimumStockLevel: number;
  reorderLevel: number;
  costPerBaseUnit: number;
  totalStockValue: number;
  isLowStock: boolean;
  isOutOfStock: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateIngredientDto {
  name: string;
  category: string;
  baseUnit: BaseUnit;
  initialStock: number;
  minimumStockLevel: number;
  reorderLevel: number;
  costPerBaseUnit: number;
}

export interface UpdateIngredientDto {
  name: string;
  category: string;
  baseUnit: BaseUnit;
  minimumStockLevel: number;
  reorderLevel: number;
  costPerBaseUnit: number;
  isActive: boolean;
}

export interface AdjustStockDto {
  operationType: 'Purchase' | 'Adjustment';
  quantity: number;
  isAbsoluteCount: boolean;
  reason: string;
  referenceNumber?: string | null;
}

export interface RecordWastageDto {
  quantity: number;
  reason: 'Expired' | 'Spoiled' | 'PrepWaste' | 'Damaged' | 'Burnt';
  notes?: string | null;
}

export interface IngredientWastageDto {
  wastageId: number;
  canteenId: number;
  ingredientId: number;
  ingredientName: string;
  baseUnit: string;
  quantity: number;
  reason: string;
  notes?: string | null;
  estimatedCost: number;
  recordedByOwnerId?: number | null;
  createdAt: string;
}

export interface StockTransactionDto {
  transactionId: number;
  canteenId: number;
  ingredientId: number;
  ingredientName: string;
  baseUnit: string;
  transactionType: string;
  quantity: number;
  previousStock: number;
  newStock: number;
  orderId?: number | null;
  referenceNumber?: string | null;
  reason: string;
  createdByOwnerId?: number | null;
  createdAt: string;
}

export interface TopConsumedIngredientDto {
  ingredientId: number;
  ingredientName: string;
  baseUnit: string;
  totalQuantityConsumed: number;
  estimatedConsumedCost: number;
}

export interface InventoryDashboardSummaryDto {
  totalIngredients: number;
  totalStockValue: number;
  lowStockCount: number;
  outOfStockCount: number;
  totalReservedStockUnits: number;
  lowStockIngredients: IngredientDto[];
  recentTransactions: StockTransactionDto[];
  topConsumedIngredients: TopConsumedIngredientDto[];
}

