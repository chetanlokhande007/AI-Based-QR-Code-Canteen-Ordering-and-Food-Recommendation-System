export interface CreateRecipeIngredientDto {
  ingredientId: number;
  quantity: number;
  unit: string;
}

export interface CreateRecipeDto {
  productId: number;
  servingSize: number;
  preparationNotes?: string | null;
  ingredients: CreateRecipeIngredientDto[];
}

export interface UpdateRecipeDto {
  servingSize: number;
  preparationNotes?: string | null;
  isActive: boolean;
  ingredients: CreateRecipeIngredientDto[];
}

export interface RecipeIngredientDto {
  recipeIngredientId: number;
  ingredientId: number;
  ingredientName: string;
  category: string;
  quantity: number;
  unit: string;
  ingredientBaseUnit: string;
  costPerBaseUnit: number;
  normalizedQuantityInBaseUnit: number;
  lineCost: number;
  availableStock: number;
  maxPortionsForThisIngredient: number;
}

export interface RecipeDto {
  recipeId: number;
  canteenId: number;
  productId: number;
  productName: string;
  productPrice: number;
  productCategory: string;
  foodType: string;
  servingSize: number;
  preparationNotes?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  ingredients: RecipeIngredientDto[];
  totalFoodCostPerServing: number;
  grossMarginAmount: number;
  grossMarginPercentage: number;
  marginStatus: 'High' | 'Healthy' | 'Low' | 'Critical' | string;
  maxPortionsAvailable: number;
  limitingIngredientId?: number | null;
  limitingIngredientName?: string | null;
}

export interface RecipeCostingSummaryDto {
  productId: number;
  productName: string;
  sellingPrice: number;
  category: string;
  foodType: string;
  recipeId?: number | null;
  hasRecipe: boolean;
  foodCostPerServing: number;
  grossMarginAmount: number;
  grossMarginPercentage: number;
  marginStatus: 'High' | 'Healthy' | 'Low' | 'Critical' | 'No Recipe' | string;
  maxPortionsAvailable: number;
  limitingIngredient?: string | null;
}

export interface IngredientAvailabilityItemDto {
  ingredientId: number;
  ingredientName: string;
  requiredPerPortion: number;
  unit: string;
  availableStock: number;
  baseUnit: string;
  portionsPossible: number;
}

export interface RecipeAvailabilityDto {
  productId: number;
  productName: string;
  maxPortionsAvailable: number;
  limitingIngredientId?: number | null;
  limitingIngredientName?: string | null;
  ingredientBreakdown: IngredientAvailabilityItemDto[];
}

