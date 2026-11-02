import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { InventoryService } from '../../core/services/inventory.service';
import {
  IngredientDto,
  CreateIngredientDto,
  UpdateIngredientDto,
  AdjustStockDto,
  RecordWastageDto,
  StockTransactionDto,
  IngredientWastageDto,
  InventoryDashboardSummaryDto,
  BaseUnit
} from '../../core/models/inventory.models';

type InventoryTab = 'ingredients' | 'transactions' | 'wastages' | 'alerts';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './inventory.html',
  styleUrl: './inventory.css'
})
export class InventoryComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly inventoryService = inject(InventoryService);

  // Active canteen context
  readonly currentUser = this.authService.currentUser;
  get effectiveCanteenId(): number | null {
    return this.currentUser()?.canteenId ?? null;
  }

  // State signals
  readonly activeTab = signal<InventoryTab>('ingredients');
  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Data signals
  readonly ingredients = signal<IngredientDto[]>([]);
  readonly summary = signal<InventoryDashboardSummaryDto | null>(null);
  readonly transactions = signal<StockTransactionDto[]>([]);
  readonly wastages = signal<IngredientWastageDto[]>([]);

  // Filter signals
  readonly searchQuery = signal<string>('');
  readonly selectedCategory = signal<string>('all');
  readonly selectedStockFilter = signal<string>('all'); // 'all' | 'low' | 'out' | 'active'

  // Modal visibility signals
  readonly showIngredientModal = signal<boolean>(false);
  readonly showAdjustModal = signal<boolean>(false);
  readonly showWastageModal = signal<boolean>(false);

  // Modal form models
  isEditingIngredient = false;
  editingIngredientId: number | null = null;
  ingredientForm: CreateIngredientDto = {
    name: '',
    category: 'General',
    baseUnit: 'g',
    initialStock: 0,
    minimumStockLevel: 0,
    reorderLevel: 0,
    costPerBaseUnit: 0
  };

  selectedIngredientForAdjust: IngredientDto | null = null;
  adjustForm: AdjustStockDto = {
    operationType: 'Purchase',
    quantity: 1,
    isAbsoluteCount: false,
    reason: '',
    referenceNumber: ''
  };

  selectedIngredientForWastage: IngredientDto | null = null;
  wastageForm: RecordWastageDto = {
    quantity: 1,
    reason: 'Spoiled',
    notes: ''
  };

  readonly categoriesList = computed(() => {
    const set = new Set<string>();
    this.ingredients().forEach((i) => {
      if (i.category) set.add(i.category);
    });
    return Array.from(set).sort();
  });

  readonly filteredIngredients = computed(() => {
    let list = this.ingredients();
    const query = this.searchQuery().trim().toLowerCase();
    const cat = this.selectedCategory();
    const stockFilter = this.selectedStockFilter();

    if (query) {
      list = list.filter(
        (i) =>
          i.name.toLowerCase().includes(query) ||
          i.category.toLowerCase().includes(query)
      );
    }

    if (cat !== 'all') {
      list = list.filter((i) => i.category.toLowerCase() === cat.toLowerCase());
    }

    if (stockFilter === 'low') {
      list = list.filter((i) => i.isLowStock);
    } else if (stockFilter === 'out') {
      list = list.filter((i) => i.isOutOfStock);
    } else if (stockFilter === 'active') {
      list = list.filter((i) => i.isActive);
    }

    return list;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.inventoryService.getDashboardSummary(canteenId).subscribe({
      next: (summary) => {
        this.summary.set(summary);
      },
      error: () => {}
    });

    this.inventoryService.getAllIngredients(canteenId).subscribe({
      next: (data) => {
        this.ingredients.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(
          err.error?.message || 'Failed to load inventory ingredients.'
        );
      }
    });

    if (this.activeTab() === 'transactions') {
      this.loadTransactions();
    } else if (this.activeTab() === 'wastages') {
      this.loadWastages();
    }
  }

  setTab(tab: InventoryTab): void {
    this.activeTab.set(tab);
    if (tab === 'transactions' && this.transactions().length === 0) {
      this.loadTransactions();
    } else if (tab === 'wastages' && this.wastages().length === 0) {
      this.loadWastages();
    }
  }

  loadTransactions(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.inventoryService.getTransactions(canteenId, undefined, 100).subscribe({
      next: (data) => {
        this.transactions.set(data);
      },
      error: () => {}
    });
  }

  loadWastages(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.inventoryService.getWastages(canteenId, undefined, 100).subscribe({
      next: (data) => {
        this.wastages.set(data);
      },
      error: () => {}
    });
  }

  // --- Modal Operations ---
  openAddIngredientModal(): void {
    this.isEditingIngredient = false;
    this.editingIngredientId = null;
    this.ingredientForm = {
      name: '',
      category: 'General',
      baseUnit: 'g',
      initialStock: 0,
      minimumStockLevel: 0,
      reorderLevel: 0,
      costPerBaseUnit: 0
    };
    this.showIngredientModal.set(true);
  }

  openEditIngredientModal(item: IngredientDto): void {
    this.isEditingIngredient = true;
    this.editingIngredientId = item.ingredientId;
    this.ingredientForm = {
      name: item.name,
      category: item.category,
      baseUnit: item.baseUnit,
      initialStock: item.currentStock,
      minimumStockLevel: item.minimumStockLevel,
      reorderLevel: item.reorderLevel,
      costPerBaseUnit: item.costPerBaseUnit
    };
    this.showIngredientModal.set(true);
  }

  saveIngredient(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId || !this.ingredientForm.name.trim()) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (this.isEditingIngredient && this.editingIngredientId) {
      const updateDto: UpdateIngredientDto = {
        name: this.ingredientForm.name.trim(),
        category: this.ingredientForm.category,
        baseUnit: this.ingredientForm.baseUnit,
        minimumStockLevel: this.ingredientForm.minimumStockLevel,
        reorderLevel: this.ingredientForm.reorderLevel,
        costPerBaseUnit: this.ingredientForm.costPerBaseUnit,
        isActive: true
      };

      this.inventoryService.updateIngredient(canteenId, this.editingIngredientId, updateDto).subscribe({
        next: (updated) => {
          this.loading.set(false);
          this.showIngredientModal.set(false);
          this.successMessage.set(`Ingredient "${updated.name}" updated successfully.`);
          this.loadData();
        },
        error: (err) => {
          this.loading.set(false);
          this.errorMessage.set(err.error?.message || 'Failed to update ingredient.');
        }
      });
    } else {
      this.inventoryService.createIngredient(canteenId, this.ingredientForm).subscribe({
        next: (created) => {
          this.loading.set(false);
          this.showIngredientModal.set(false);
          this.successMessage.set(`Ingredient "${created.name}" created successfully.`);
          this.loadData();
        },
        error: (err) => {
          this.loading.set(false);
          this.errorMessage.set(err.error?.message || 'Failed to create ingredient.');
        }
      });
    }
  }

  openAdjustStockModal(item: IngredientDto): void {
    this.selectedIngredientForAdjust = item;
    this.adjustForm = {
      operationType: 'Purchase',
      quantity: 1,
      isAbsoluteCount: false,
      reason: '',
      referenceNumber: ''
    };
    this.showAdjustModal.set(true);
  }

  submitAdjustStock(): void {
    const canteenId = this.effectiveCanteenId;
    const ingredient = this.selectedIngredientForAdjust;
    if (!canteenId || !ingredient || !this.adjustForm.reason.trim()) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.inventoryService.adjustStock(canteenId, ingredient.ingredientId, this.adjustForm).subscribe({
      next: (updated) => {
        this.loading.set(false);
        this.showAdjustModal.set(false);
        this.successMessage.set(`Stock for "${updated.name}" updated to ${updated.currentStock} ${updated.baseUnit}.`);
        this.loadData();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to adjust stock.');
      }
    });
  }

  openRecordWastageModal(item?: IngredientDto): void {
    if (item) {
      this.selectedIngredientForWastage = item;
    } else if (this.ingredients().length > 0) {
      this.selectedIngredientForWastage = this.ingredients()[0];
    }
    this.wastageForm = {
      quantity: 1,
      reason: 'Spoiled',
      notes: ''
    };
    this.showWastageModal.set(true);
  }

  submitWastage(): void {
    const canteenId = this.effectiveCanteenId;
    const ingredient = this.selectedIngredientForWastage;
    if (!canteenId || !ingredient || this.wastageForm.quantity <= 0) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.inventoryService.recordWastage(canteenId, ingredient.ingredientId, this.wastageForm).subscribe({
      next: (waste) => {
        this.loading.set(false);
        this.showWastageModal.set(false);
        this.successMessage.set(`Recorded ${waste.quantity} ${waste.baseUnit} wastage for "${waste.ingredientName}". Write-off: ₹${waste.estimatedCost.toFixed(2)}.`);
        this.loadData();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to record wastage.');
      }
    });
  }

  closeModals(): void {
    this.showIngredientModal.set(false);
    this.showAdjustModal.set(false);
    this.showWastageModal.set(false);
  }
}

