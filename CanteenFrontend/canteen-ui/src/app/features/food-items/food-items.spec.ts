import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { describe, it, expect, beforeEach } from 'vitest';

import { FoodItems } from './food-items';
import { ProductDto } from '../../core/models/product.models';
import { CategoryDto } from '../../core/models/category.models';

describe('FoodItems', () => {
  let component: FoodItems;
  let fixture: ComponentFixture<FoodItems>;

  const mockCategories: CategoryDto[] = [
    { categoryId: 1, canteenId: 1, name: 'Main Course', description: 'Curries & Rice', isActive: true },
    { categoryId: 2, canteenId: 1, name: 'Beverages', description: 'Drinks & Shakes', isActive: true }
  ];

  const mockProducts: ProductDto[] = [
    {
      productId: 101,
      canteenId: 1,
      categoryId: 1,
      name: 'Paneer Butter Masala',
      description: 'Rich creamy paneer curry',
      price: 120,
      stockQuantity: 50,
      isAvailable: true
    },
    {
      productId: 102,
      canteenId: 1,
      categoryId: 1,
      name: 'Chicken Biryani',
      description: 'Aromatic basmati rice with tender chicken',
      price: 140,
      stockQuantity: 25,
      isAvailable: true
    },
    {
      productId: 103,
      canteenId: 1,
      categoryId: 2,
      name: 'Cold Coffee',
      description: 'Chilled coffee with ice cream',
      price: 60,
      stockQuantity: 10,
      isAvailable: false
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FoodItems],
      providers: [
        provideHttpClient(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FoodItems);
    component = fixture.componentInstance;
    component.categories.set(mockCategories);
    component.products.set(mockProducts);
    await fixture.whenStable();
  }, 30000);

  it('should create FoodItems component', () => {
    expect(component).toBeTruthy();
  });

  it('should calculate total, veg, and non-veg counts correctly', () => {
    expect(component.totalCount()).toBe(3);
    // Paneer Butter Masala & Cold Coffee are veg; Chicken Biryani is non-veg
    expect(component.vegCount()).toBe(2);
    expect(component.nonVegCount()).toBe(1);
  });

  it('should filter items by search query', () => {
    component.searchTerm = 'Biryani';
    const filtered = component.filteredProducts();
    expect(filtered.length).toBe(1);
    expect(filtered[0].name).toBe('Chicken Biryani');
  });

  it('should filter items by category', () => {
    component.selectedCategory.set(2);
    const filtered = component.filteredProducts();
    expect(filtered.length).toBe(1);
    expect(filtered[0].name).toBe('Cold Coffee');
  });

  it('should filter items by type (veg vs non-veg)', () => {
    component.selectedType.set('veg');
    expect(component.filteredProducts().length).toBe(2);

    component.selectedType.set('non-veg');
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].name).toBe('Chicken Biryani');
  });

  it('should filter items by availability status', () => {
    component.selectedStatus.set('active');
    expect(component.filteredProducts().length).toBe(2);

    component.selectedStatus.set('inactive');
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].name).toBe('Cold Coffee');
  });

  it('should switch quick pills appropriately', () => {
    component.setQuickPill('veg');
    expect(component.activeQuickPill()).toBe('veg');
    expect(component.selectedType()).toBe('veg');

    component.setQuickPill('all');
    expect(component.activeQuickPill()).toBe('all');
    expect(component.selectedType()).toBe('all');
  });

  it('should open and populate add modal', () => {
    component.openAddModal();
    expect(component.showModal()).toBe(true);
    expect(component.isEditMode()).toBe(false);
    expect(component.formName).toBe('');
    expect(component.formCategoryId).toBe(1);
  });

  it('should open and populate edit modal with existing product data', () => {
    component.openEditModal(mockProducts[0]);
    expect(component.showModal()).toBe(true);
    expect(component.isEditMode()).toBe(true);
    expect(component.formName).toBe('Paneer Butter Masala');
    expect(component.formPrice).toBe(120);
    expect(component.editingProductId).toBe(101);
  });
});
