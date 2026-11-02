import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { RecipesComponent } from './recipes';
import { RecipeService } from '../../core/services/recipe.service';
import { InventoryService } from '../../core/services/inventory.service';
import { ProductService } from '../../core/services/product.service';
import { AuthService } from '../../core/services/auth.service';
import { RecipeCostingSummaryDto, RecipeDto, RecipeAvailabilityDto } from '../../core/models/recipe.models';
import { IngredientDto } from '../../core/models/inventory.models';
import { ProductDto } from '../../core/models/product.models';

describe('RecipesComponent', () => {
  let component: RecipesComponent;
  let fixture: ComponentFixture<RecipesComponent>;

  const mockCostingSummaries: RecipeCostingSummaryDto[] = [
    {
      productId: 1,
      productName: 'Paneer Butter Masala',
      sellingPrice: 180.0,
      category: 'Main Course',
      foodType: 'Veg',
      recipeId: 10,
      hasRecipe: true,
      foodCostPerServing: 59.0,
      grossMarginAmount: 121.0,
      grossMarginPercentage: 67.22,
      marginStatus: 'High',
      maxPortionsAvailable: 10,
      limitingIngredient: 'Paneer'
    },
    {
      productId: 2,
      productName: 'Cold Drink Can',
      sellingPrice: 40.0,
      category: 'Beverages',
      foodType: 'Veg',
      recipeId: null,
      hasRecipe: false,
      foodCostPerServing: 0,
      grossMarginAmount: 40.0,
      grossMarginPercentage: 100,
      marginStatus: 'No Recipe',
      maxPortionsAvailable: 30,
      limitingIngredient: null
    }
  ];

  const mockRecipes: RecipeDto[] = [
    {
      recipeId: 10,
      canteenId: 1,
      productId: 1,
      productName: 'Paneer Butter Masala',
      productPrice: 180.0,
      productCategory: 'Main Course',
      foodType: 'Veg',
      servingSize: 1,
      preparationNotes: 'Cook gravy and add paneer',
      isActive: true,
      createdAt: '2026-11-02T00:00:00Z',
      ingredients: [
        {
          recipeIngredientId: 101,
          ingredientId: 1,
          ingredientName: 'Basmati Rice',
          category: 'Grains',
          quantity: 200,
          unit: 'g',
          ingredientBaseUnit: 'g',
          costPerBaseUnit: 0.08,
          normalizedQuantityInBaseUnit: 200,
          lineCost: 16.0,
          availableStock: 5000,
          maxPortionsForThisIngredient: 25
        },
        {
          recipeIngredientId: 102,
          ingredientId: 2,
          ingredientName: 'Paneer',
          category: 'Dairy',
          quantity: 100,
          unit: 'g',
          ingredientBaseUnit: 'g',
          costPerBaseUnit: 0.40,
          normalizedQuantityInBaseUnit: 100,
          lineCost: 40.0,
          availableStock: 1000,
          maxPortionsForThisIngredient: 10
        }
      ],
      totalFoodCostPerServing: 56.0,
      grossMarginAmount: 124.0,
      grossMarginPercentage: 68.89,
      marginStatus: 'High',
      maxPortionsAvailable: 10,
      limitingIngredientId: 2,
      limitingIngredientName: 'Paneer'
    }
  ];

  const mockIngredients: IngredientDto[] = [
    {
      ingredientId: 1,
      canteenId: 1,
      name: 'Basmati Rice',
      category: 'Grains',
      baseUnit: 'g',
      currentStock: 5000,
      reservedStock: 0,
      availableStock: 5000,
      minimumStockLevel: 500,
      reorderLevel: 1000,
      costPerBaseUnit: 0.08,
      totalStockValue: 400,
      isLowStock: false,
      isOutOfStock: false,
      isActive: true,
      createdAt: '2026-11-02T00:00:00Z'
    },
    {
      ingredientId: 2,
      canteenId: 1,
      name: 'Paneer',
      category: 'Dairy',
      baseUnit: 'g',
      currentStock: 1000,
      reservedStock: 0,
      availableStock: 1000,
      minimumStockLevel: 200,
      reorderLevel: 400,
      costPerBaseUnit: 0.40,
      totalStockValue: 400,
      isLowStock: false,
      isOutOfStock: false,
      isActive: true,
      createdAt: '2026-11-02T00:00:00Z'
    }
  ];

  const mockProducts: ProductDto[] = [
    {
      productId: 1,
      canteenId: 1,
      categoryId: 1,
      name: 'Paneer Butter Masala',
      description: 'Cottage cheese curry',
      price: 180.0,
      stockQuantity: 50,
      isAvailable: true,
      foodType: 'Veg',
      },

    {
      productId: 2,
      canteenId: 1,
      categoryId: 2,
      name: 'Cold Drink Can',
      description: 'Packaged beverage',
      price: 40.0,
      stockQuantity: 30,
      isAvailable: true,
      foodType: 'Veg',
      }

  ];

  const mockAvailability: RecipeAvailabilityDto = {
    productId: 1,
    productName: 'Paneer Butter Masala',
    maxPortionsAvailable: 10,
    limitingIngredientId: 2,
    limitingIngredientName: 'Paneer',
    ingredientBreakdown: [
      {
        ingredientId: 1,
        ingredientName: 'Basmati Rice',
        requiredPerPortion: 200,
        unit: 'g',
        availableStock: 5000,
        baseUnit: 'g',
        portionsPossible: 25
      },
      {
        ingredientId: 2,
        ingredientName: 'Paneer',
        requiredPerPortion: 100,
        unit: 'g',
        availableStock: 1000,
        baseUnit: 'g',
        portionsPossible: 10
      }
    ]
  };

  const mockRecipeService = {
    getCostingSummaries: () => of(mockCostingSummaries),
    getAllRecipes: () => of(mockRecipes),
    getPortionAvailability: () => of(mockAvailability),
    createRecipe: () => of(mockRecipes[0]),
    updateRecipe: () => of(mockRecipes[0]),
    deleteRecipe: () => of(undefined)
  };

  const mockInventoryService = {
    getAllIngredients: () => of(mockIngredients)
  };

  const mockProductService = {
    getByCanteen: () => of(mockProducts)
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
      imports: [RecipesComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RecipeService, useValue: mockRecipeService },
        { provide: InventoryService, useValue: mockInventoryService },
        { provide: ProductService, useValue: mockProductService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RecipesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should load costing summaries and compute KPIs correctly', () => {
    expect(component.costingSummaries().length).toBe(2);
    expect(component.totalProductsCount()).toBe(2);
    expect(component.recipesCount()).toBe(1);
    expect(component.recipeCoveragePercent()).toBe(50);
    expect(component.avgGrossMargin()).toBe(67.2);
  });

  it('should filter dishes by search query', () => {
    component.searchQuery.set('Drink');
    const filtered = component.filteredSummaries();
    expect(filtered.length).toBe(1);
    expect(filtered[0].productName).toBe('Cold Drink Can');
  });

  it('should filter dishes by margin status', () => {
    component.selectedMarginFilter.set('high');
    const filtered = component.filteredSummaries();
    expect(filtered.length).toBe(1);
    expect(filtered[0].productName).toBe('Paneer Butter Masala');
  });

  it('should identify products without recipes for recipe creation', () => {
    const productsWithoutRecipe = component.productsWithoutRecipe();
    expect(productsWithoutRecipe.length).toBe(1);
    expect(productsWithoutRecipe[0].productId).toBe(2);
  });

  it('should open and populate portion availability modal', () => {
    component.checkPortionAvailability(1);
    expect(component.showAvailabilityModal()).toBe(true);
    expect(component.selectedAvailability()?.maxPortionsAvailable).toBe(10);
    expect(component.selectedAvailability()?.limitingIngredientName).toBe('Paneer');
  });
});
