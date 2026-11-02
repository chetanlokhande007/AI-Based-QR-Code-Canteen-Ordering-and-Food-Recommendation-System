import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { RecipeService } from '../../core/services/recipe.service';
import { InventoryService } from '../../core/services/inventory.service';
import { ProductService } from '../../core/services/product.service';
import {
  RecipeDto,
  RecipeCostingSummaryDto,
  RecipeAvailabilityDto,
  CreateRecipeDto,
  UpdateRecipeDto,
  CreateRecipeIngredientDto
} from '../../core/models/recipe.models';
import { IngredientDto } from '../../core/models/inventory.models';
import { ProductDto } from '../../core/models/product.models';

type RecipeTab = 'costing' | 'recipes';

interface RecipeIngredientFormRow {
  ingredientId: number;
  quantity: number;
  unit: string;
}

@Component({
  selector: 'app-recipes',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './recipes.html',
  styleUrl: './recipes.css'
})
export class RecipesComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly recipeService = inject(RecipeService);
  private readonly inventoryService = inject(InventoryService);
  private readonly productService = inject(ProductService);

  readonly currentUser = this.authService.currentUser;
  get effectiveCanteenId(): number | null {
    return this.currentUser()?.canteenId ?? null;
  }

  // State signals
  readonly activeTab = signal<RecipeTab>('costing');
  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Data signals
  readonly costingSummaries = signal<RecipeCostingSummaryDto[]>([]);
  readonly recipes = signal<RecipeDto[]>([]);
  readonly availableIngredients = signal<IngredientDto[]>([]);
  readonly availableProducts = signal<ProductDto[]>([]);

  // Filter signals
  readonly searchQuery = signal<string>('');
  readonly selectedMarginFilter = signal<string>('all'); // 'all' | 'high' | 'healthy' | 'low' | 'critical' | 'norecipe'

  // Modal signals
  readonly showRecipeModal = signal<boolean>(false);
  readonly showAvailabilityModal = signal<boolean>(false);
  readonly showDeleteModal = signal<boolean>(false);

  // Modal form models
  isEditing = false;
  editingRecipeId: number | null = null;
  selectedProductId: number | null = null;
  servingSize = 1;
  preparationNotes = '';
  ingredientRows: RecipeIngredientFormRow[] = [];

  // Portion availability view
  readonly selectedAvailability = signal<RecipeAvailabilityDto | null>(null);
  readonly availabilityLoading = signal<boolean>(false);

  // Delete modal state
  deletingRecipeId: number | null = null;
  deletingDishName = '';

  // KPI Computations
  readonly totalProductsCount = computed(() => this.costingSummaries().length);

  readonly recipesCount = computed(() => this.costingSummaries().filter(s => s.hasRecipe).length);

  readonly recipeCoveragePercent = computed(() => {
    const total = this.totalProductsCount();
    if (total === 0) return 0;
    return Math.round((this.recipesCount() / total) * 100);
  });

  readonly avgGrossMargin = computed(() => {
    const itemsWithRecipe = this.costingSummaries().filter(s => s.hasRecipe && s.sellingPrice > 0);
    if (itemsWithRecipe.length === 0) return 0;
    const sum = itemsWithRecipe.reduce((acc, curr) => acc + curr.grossMarginPercentage, 0);
    return Math.round((sum / itemsWithRecipe.length) * 10) / 10;
  });

  readonly lowStockDishesCount = computed(() => {
    return this.costingSummaries().filter(s => s.hasRecipe && s.maxPortionsAvailable <= 5).length;
  });

  // Filtered Costing Summaries
  readonly filteredSummaries = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const marginF = this.selectedMarginFilter();

    return this.costingSummaries().filter(item => {
      const matchesSearch = !q ||
        item.productName.toLowerCase().includes(q) ||
        item.category.toLowerCase().includes(q);

      if (!matchesSearch) return false;

      if (marginF === 'all') return true;
      if (marginF === 'high') return item.marginStatus.toLowerCase() === 'high';
      if (marginF === 'healthy') return item.marginStatus.toLowerCase() === 'healthy';
      if (marginF === 'low') return item.marginStatus.toLowerCase() === 'low';
      if (marginF === 'critical') return item.marginStatus.toLowerCase() === 'critical';
      if (marginF === 'norecipe') return !item.hasRecipe;

      return true;
    });
  });

  // Products available to create a recipe (exclude products that already have a recipe)
  readonly productsWithoutRecipe = computed(() => {
    const existingRecipeProductIds = new Set(this.recipes().map(r => r.productId));
    return this.availableProducts().filter(p => !existingRecipeProductIds.has(p.productId));
  });

  ngOnInit(): void {
    this.loadAllData();
  }

  loadAllData(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) {
      this.errorMessage.set('No active canteen context found. Please ensure you are logged in to a canteen.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    // 1. Load Costing Summaries
    this.recipeService.getCostingSummaries(canteenId).subscribe({
      next: (summaries) => {
        this.costingSummaries.set(summaries);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message ?? 'Failed to load recipe costing summaries.');
        this.loading.set(false);
      }
    });

    // 2. Load Full Recipes
    this.recipeService.getAllRecipes(canteenId).subscribe({
      next: (recipesList) => this.recipes.set(recipesList),
      error: () => {}
    });

    // 3. Load Ingredients for BOM builder
    this.inventoryService.getAllIngredients(canteenId, true).subscribe({
      next: (ingredients) => this.availableIngredients.set(ingredients),
      error: () => {}
    });

    // 4. Load Products for selection
    this.productService.getByCanteen(canteenId).subscribe({
      next: (products) => this.availableProducts.set(products),
      error: () => {}
    });
  }

  setTab(tab: RecipeTab): void {
    this.activeTab.set(tab);
  }

  // ==========================================
  // Recipe Modal: Create / Edit
  // ==========================================

  openCreateModal(preselectedProductId?: number): void {
    this.isEditing = false;
    this.editingRecipeId = null;
    this.selectedProductId = preselectedProductId ?? (this.productsWithoutRecipe()[0]?.productId ?? null);
    this.servingSize = 1;
    this.preparationNotes = '';
    this.ingredientRows = [
      {
        ingredientId: this.availableIngredients()[0]?.ingredientId ?? 0,
        quantity: 100,
        unit: this.availableIngredients()[0]?.baseUnit ?? 'g'
      }
    ];
    this.showRecipeModal.set(true);
  }

  openEditModal(recipe: RecipeDto): void {
    this.isEditing = true;
    this.editingRecipeId = recipe.recipeId;
    this.selectedProductId = recipe.productId;
    this.servingSize = recipe.servingSize > 0 ? recipe.servingSize : 1;
    this.preparationNotes = recipe.preparationNotes ?? '';
    this.ingredientRows = recipe.ingredients.map(i => ({
      ingredientId: i.ingredientId,
      quantity: i.quantity,
      unit: i.unit
    }));
    this.showRecipeModal.set(true);
  }

  closeRecipeModal(): void {
    this.showRecipeModal.set(false);
  }

  addIngredientRow(): void {
    const firstIng = this.availableIngredients()[0];
    this.ingredientRows.push({
      ingredientId: firstIng?.ingredientId ?? 0,
      quantity: 50,
      unit: firstIng?.baseUnit ?? 'g'
    });
  }

  removeIngredientRow(index: number): void {
    if (this.ingredientRows.length > 1) {
      this.ingredientRows.splice(index, 1);
    }
  }

  onIngredientSelected(row: RecipeIngredientFormRow): void {
    const ingredient = this.availableIngredients().find(i => i.ingredientId === +row.ingredientId);
    if (ingredient) {
      row.unit = ingredient.baseUnit;
    }
  }

  // Estimated Live Preview Calculation in Modal
  get modalEstimatedFoodCost(): number {
    let total = 0;
    for (const row of this.ingredientRows) {
      const ing = this.availableIngredients().find(i => i.ingredientId === +row.ingredientId);
      if (!ing) continue;

      let multiplier = 1;
      const rUnit = row.unit.toLowerCase();
      const bUnit = ing.baseUnit.toLowerCase();

      if (rUnit === 'kg' && bUnit === 'g') multiplier = 1000;
      else if (rUnit === 'g' && bUnit === 'kg') multiplier = 0.001;
      else if (rUnit === 'l' && bUnit === 'ml') multiplier = 1000;
      else if (rUnit === 'ml' && bUnit === 'l') multiplier = 0.001;

      const normQty = (+row.quantity || 0) * multiplier;
      total += normQty * ing.costPerBaseUnit;
    }
    const portions = Math.max(1, this.servingSize);
    return Math.round((total / portions) * 100) / 100;
  }

  get modalEstimatedMargin(): { amount: number; pct: number } {
    const product = this.availableProducts().find(p => p.productId === this.selectedProductId);
    const price = product?.price ?? 0;
    const cost = this.modalEstimatedFoodCost;
    const amount = Math.round((price - cost) * 100) / 100;
    const pct = price > 0 ? Math.round((amount / price) * 10000) / 100 : 0;
    return { amount, pct };
  }

  saveRecipe(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    if (!this.selectedProductId) {
      this.errorMessage.set('Please select a menu dish.');
      return;
    }

    if (this.ingredientRows.length === 0) {
      this.errorMessage.set('Recipe must have at least one ingredient.');
      return;
    }

    for (const row of this.ingredientRows) {
      if (!row.ingredientId || row.quantity <= 0) {
        this.errorMessage.set('Each ingredient must have a valid quantity greater than 0.');
        return;
      }
    }

    // Check duplicate ingredients
    const ids = this.ingredientRows.map(r => +r.ingredientId);
    if (new Set(ids).size !== ids.length) {
      this.errorMessage.set('Duplicate ingredients selected. Please combine quantities into a single line.');
      return;
    }

    this.loading.set(true);

    if (this.isEditing && this.editingRecipeId) {
      const updateDto: UpdateRecipeDto = {
        servingSize: this.servingSize,
        preparationNotes: this.preparationNotes,
        isActive: true,
        ingredients: this.ingredientRows.map(r => ({
          ingredientId: +r.ingredientId,
          quantity: +r.quantity,
          unit: r.unit
        }))
      };

      this.recipeService.updateRecipe(canteenId, this.editingRecipeId, updateDto).subscribe({
        next: () => {
          this.closeRecipeModal();
          this.successMessage.set('Recipe updated successfully!');
          this.loadAllData();
          setTimeout(() => this.successMessage.set(null), 3500);
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.message ?? 'Failed to update recipe.');
          this.loading.set(false);
        }
      });
    } else {
      const createDto: CreateRecipeDto = {
        productId: this.selectedProductId,
        servingSize: this.servingSize,
        preparationNotes: this.preparationNotes,
        ingredients: this.ingredientRows.map(r => ({
          ingredientId: +r.ingredientId,
          quantity: +r.quantity,
          unit: r.unit
        }))
      };

      this.recipeService.createRecipe(canteenId, createDto).subscribe({
        next: () => {
          this.closeRecipeModal();
          this.successMessage.set('Recipe created successfully!');
          this.loadAllData();
          setTimeout(() => this.successMessage.set(null), 3500);
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.message ?? 'Failed to create recipe.');
          this.loading.set(false);
        }
      });
    }
  }

  // ==========================================
  // Portion Availability Modal
  // ==========================================

  checkPortionAvailability(productId: number): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.availabilityLoading.set(true);
    this.selectedAvailability.set(null);
    this.showAvailabilityModal.set(true);

    this.recipeService.getPortionAvailability(canteenId, productId).subscribe({
      next: (avail) => {
        this.selectedAvailability.set(avail);
        this.availabilityLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message ?? 'Failed to check portion availability.');
        this.availabilityLoading.set(false);
        this.showAvailabilityModal.set(false);
      }
    });
  }

  closeAvailabilityModal(): void {
    this.showAvailabilityModal.set(false);
    this.selectedAvailability.set(null);
  }

  // ==========================================
  // Delete Recipe Modal
  // ==========================================

  promptDeleteRecipe(recipeId: number, dishName: string): void {
    this.deletingRecipeId = recipeId;
    this.deletingDishName = dishName;
    this.showDeleteModal.set(true);
  }

  closeDeleteModal(): void {
    this.showDeleteModal.set(false);
    this.deletingRecipeId = null;
    this.deletingDishName = '';
  }

  confirmDeleteRecipe(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId || !this.deletingRecipeId) return;

    this.loading.set(true);
    this.recipeService.deleteRecipe(canteenId, this.deletingRecipeId).subscribe({
      next: () => {
        this.closeDeleteModal();
        this.successMessage.set(`Recipe for "${this.deletingDishName}" deleted successfully.`);
        this.loadAllData();
        setTimeout(() => this.successMessage.set(null), 3500);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message ?? 'Failed to delete recipe.');
        this.loading.set(false);
      }
    });
  }

  getRecipeByProductId(productId: number): RecipeDto | undefined {
    return this.recipes().find(r => r.productId === productId);
  }
}

