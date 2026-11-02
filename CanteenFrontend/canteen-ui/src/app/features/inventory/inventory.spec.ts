import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { InventoryComponent } from './inventory';
import { InventoryService } from '../../core/services/inventory.service';
import { AuthService } from '../../core/services/auth.service';
import { IngredientDto, InventoryDashboardSummaryDto } from '../../core/models/inventory.models';

describe('InventoryComponent', () => {
  let component: InventoryComponent;
  let fixture: ComponentFixture<InventoryComponent>;

  const mockIngredients: IngredientDto[] = [
    {
      ingredientId: 1,
      canteenId: 1,
      name: 'Basmati Rice',
      category: 'Grains',
      baseUnit: 'kg',
      currentStock: 25.5,
      reservedStock: 2.0,
      availableStock: 23.5,
      minimumStockLevel: 5.0,
      reorderLevel: 10.0,
      costPerBaseUnit: 45.0,
      totalStockValue: 1147.5,
      isLowStock: false,
      isOutOfStock: false,
      isActive: true,
      createdAt: '2026-11-02T00:00:00Z'
    },
    {
      ingredientId: 2,
      canteenId: 1,
      name: 'Cooking Oil',
      category: 'Oils',
      baseUnit: 'L',
      currentStock: 3.0,
      reservedStock: 0,
      availableStock: 3.0,
      minimumStockLevel: 5.0,
      reorderLevel: 8.0,
      costPerBaseUnit: 120.0,
      totalStockValue: 360.0,
      isLowStock: true,
      isOutOfStock: false,
      isActive: true,
      createdAt: '2026-11-02T00:00:00Z'
    }
  ];

  const mockSummary: InventoryDashboardSummaryDto = {
    totalIngredients: 2,
    totalStockValue: 1507.5,
    lowStockCount: 1,
    outOfStockCount: 0,
    totalReservedStockUnits: 2.0,
    lowStockIngredients: [mockIngredients[1]],
    recentTransactions: [],
    topConsumedIngredients: []
  };

  const mockInventoryService = {
    getAllIngredients: () => of(mockIngredients),
    getDashboardSummary: () => of(mockSummary),
    getTransactions: () => of([]),
    getWastages: () => of([]),
    createIngredient: () => of(mockIngredients[0]),
    updateIngredient: () => of(mockIngredients[0]),
    adjustStock: () => of(mockIngredients[0]),
    recordWastage: () => of({
      wastageId: 1,
      canteenId: 1,
      ingredientId: 1,
      ingredientName: 'Basmati Rice',
      baseUnit: 'kg',
      quantity: 1.0,
      reason: 'Spoiled',
      estimatedCost: 45.0,
      createdAt: '2026-11-02T00:00:00Z'
    })
  };

  const mockAuthService = {
    currentUser: () => ({
      ownerId: 8,
      fullName: 'Test Owner',
      email: 'owner@canteen.com',
      role: 'CanteenOwner',
      canteenId: 1
    })
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InventoryComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: InventoryService, useValue: mockInventoryService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InventoryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should load ingredients and summary on initialization', () => {
    expect(component.ingredients().length).toBe(2);
    expect(component.summary()?.totalIngredients).toBe(2);
    expect(component.summary()?.lowStockCount).toBe(1);
  });

  it('should filter ingredients by search query', () => {
    component.searchQuery.set('Rice');
    const filtered = component.filteredIngredients();
    expect(filtered.length).toBe(1);
    expect(filtered[0].name).toBe('Basmati Rice');
  });

  it('should filter ingredients by low stock status', () => {
    component.selectedStockFilter.set('low');
    const filtered = component.filteredIngredients();
    expect(filtered.length).toBe(1);
    expect(filtered[0].name).toBe('Cooking Oil');
  });
});

