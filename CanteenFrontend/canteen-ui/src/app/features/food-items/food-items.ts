import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { ProductService } from '../../core/services/product.service';
import { CategoryService } from '../../core/services/category.service';
import { ThemeService } from '../../core/services/theme.service';
import { ProductDto, CreateProductRequest, UpdateProductRequest } from '../../core/models/product.models';
import { CategoryDto } from '../../core/models/category.models';
import { CanteenSummaryDto, CanteenDto } from '../../core/models/canteen.models';

@Component({
  selector: 'app-food-items',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './food-items.html',
  styleUrl: './food-items.css'
})
export class FoodItems implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly productService = inject(ProductService);
  private readonly categoryService = inject(CategoryService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly subscriptions = new Subscription();

  // Current session & theme
  readonly currentUser = this.authService.currentUser;
  readonly activeTheme = this.themeService.activeTheme;

  // Data signals
  readonly products = signal<ProductDto[]>([]);
  readonly categories = signal<CategoryDto[]>([]);
  readonly canteens = signal<CanteenSummaryDto[]>([]);
  readonly activeCanteen = signal<CanteenDto | null>(null);

  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');
  readonly isSwitching = signal<boolean>(false);
  readonly isCanteenSelectorOpen = signal<boolean>(false);

  // Filters & Search
  searchTerm = '';
  selectedCategory = signal<number | 'all'>('all');
  selectedType = signal<'all' | 'veg' | 'non-veg'>('all');
  selectedStatus = signal<'all' | 'active' | 'inactive'>('all');
  activeQuickPill = signal<string>('all'); // 'all' | 'veg' | 'non-veg' | 'cat_{id}'

  // Sort State
  sortField = signal<'name' | 'category' | 'type' | 'price' | 'status'>('name');
  sortDirection = signal<'asc' | 'desc'>('asc');

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Row Selection (Checkboxes)
  selectedProductIds = signal<Set<number>>(new Set());

  // Modal State (Add / Edit)
  readonly showModal = signal<boolean>(false);
  readonly isEditMode = signal<boolean>(false);
  editingProductId: number | null = null;

  // Form Fields
  formName = '';
  formCategoryId: number | null = null;
  formPrice: number | null = null;
  formStock = 50;
  formIsAvailable = true;
  formDescription = '';
  formImageUrl = '';

  // Dish Photo Upload State
  readonly imageFileName = signal<string>('');
  readonly isDraggingImage = signal<boolean>(false);
  readonly imageUploadError = signal<string | null>(null);

  readonly modalSaving = signal<boolean>(false);
  readonly modalError = signal<string>('');
  readonly modalSuccess = signal<string>('');

  // Dropdown filter popover states
  readonly isCategoryDropdownOpen = signal<boolean>(false);
  readonly isTypeDropdownOpen = signal<boolean>(false);
  readonly isStatusDropdownOpen = signal<boolean>(false);
  readonly isPageSizeDropdownOpen = signal<boolean>(false);

  readonly currentCanteenName = computed(() => {
    return this.activeCanteen()?.name || (this.currentUser()?.canteenId ? ('Canteen #' + this.currentUser()?.canteenId) : 'Select Canteen');
  });

  get ownerInitials(): string {
    const name = this.currentUser()?.fullName?.trim();
    if (!name) return 'CO';
    const parts = name.split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  // Quick Pill Counts
  readonly totalCount = computed(() => this.products().length);
  readonly vegCount = computed(() => this.products().filter(p => this.isVeg(p)).length);
  readonly nonVegCount = computed(() => this.products().filter(p => !this.isVeg(p)).length);

  // Filtered & Sorted List
  readonly filteredProducts = computed(() => {
    let list = [...this.products()];
    const query = this.searchTerm.trim().toLowerCase();
    const cat = this.selectedCategory();
    const type = this.selectedType();
    const status = this.selectedStatus();

    // 1. Search filter
    if (query) {
      list = list.filter(p =>
        p.name.toLowerCase().includes(query) ||
        (p.description && p.description.toLowerCase().includes(query))
      );
    }

    // 2. Category filter
    if (cat !== 'all') {
      list = list.filter(p => p.categoryId === cat);
    }

    // 3. Type filter (Veg / Non-Veg)
    if (type === 'veg') {
      list = list.filter(p => this.isVeg(p));
    } else if (type === 'non-veg') {
      list = list.filter(p => !this.isVeg(p));
    }

    // 4. Status filter
    if (status === 'active') {
      list = list.filter(p => p.isAvailable);
    } else if (status === 'inactive') {
      list = list.filter(p => !p.isAvailable);
    }

    // 5. Sorting
    const field = this.sortField();
    const dir = this.sortDirection() === 'asc' ? 1 : -1;

    list.sort((a, b) => {
      if (field === 'name') {
        return a.name.localeCompare(b.name) * dir;
      } else if (field === 'price') {
        return (Number(a.price) - Number(b.price)) * dir;
      } else if (field === 'category') {
        const catA = this.getCategoryName(a.categoryId);
        const catB = this.getCategoryName(b.categoryId);
        return catA.localeCompare(catB) * dir;
      } else if (field === 'type') {
        const typeA = this.isVeg(a) ? 'Veg' : 'Non-Veg';
        const typeB = this.isVeg(b) ? 'Veg' : 'Non-Veg';
        return typeA.localeCompare(typeB) * dir;
      } else if (field === 'status') {
        return ((a.isAvailable === b.isAvailable) ? 0 : a.isAvailable ? -1 : 1) * dir;
      }
      return 0;
    });

    return list;
  });

  // Paginated List
  readonly paginatedProducts = computed(() => {
    const list = this.filteredProducts();
    const page = this.currentPage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  readonly totalPages = computed(() => {
    const total = this.filteredProducts().length;
    const size = this.pageSize();
    return Math.max(1, Math.ceil(total / size));
  });

  readonly showingStart = computed(() => {
    const total = this.filteredProducts().length;
    if (total === 0) return 0;
    return (this.currentPage() - 1) * this.pageSize() + 1;
  });

  readonly showingEnd = computed(() => {
    const total = this.filteredProducts().length;
    return Math.min(this.currentPage() * this.pageSize(), total);
  });

  readonly isAllSelected = computed(() => {
    const visible = this.paginatedProducts();
    if (visible.length === 0) return false;
    const selected = this.selectedProductIds();
    return visible.every(p => selected.has(p.productId));
  });

  ngOnInit(): void {
    this.loadOwnerCanteensList();

    this.subscriptions.add(
      this.route.queryParams.subscribe(params => {
        const queryCanteenId = params['canteenId'] ? Number(params['canteenId']) : null;
        if (queryCanteenId && queryCanteenId !== this.currentUser()?.canteenId) {
          this.isSwitching.set(true);
          this.products.set([]);
          this.categories.set([]);
          this.activeCanteen.set(null);
          this.cdr.markForCheck();

          this.authService.switchCanteen(queryCanteenId).subscribe({
            next: () => {
              this.isSwitching.set(false);
              this.loadCanteenFoodData();
              this.loadOwnerCanteensList();
            },
            error: () => {
              this.isSwitching.set(false);
              this.loadCanteenFoodData();
            }
          });
        } else {
          this.loadCanteenFoodData();
        }

        if (params['openAdd'] === 'true') {
          this.openAddModal();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadCanteenFoodData(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.cdr.markForCheck();

    this.authService.getProfile().subscribe({
      next: (user) => {
        const canteenId = user.canteenId;
        if (!canteenId) {
          this.products.set([]);
          this.categories.set([]);
          this.activeCanteen.set(null);
          this.loading.set(false);
          this.cdr.markForCheck();
          return;
        }

        forkJoin({
          canteen: this.canteenService.getById(canteenId),
          categories: this.categoryService.getAllByCanteen(canteenId),
          products: this.productService.getByCanteen(canteenId, false)
        }).subscribe({
          next: ({ canteen, categories, products }) => {
            this.activeCanteen.set(canteen);
            this.categories.set(categories || []);
            this.products.set(products || []);
            this.loading.set(false);
            this.cdr.markForCheck();
          },
          error: (err: HttpErrorResponse) => {
            this.loading.set(false);
            if (err.status === 403) {
              this.errorMessage.set('Unauthorized canteen access. You can only view your own canteens.');
            } else {
              this.errorMessage.set('Failed to load menu items from server. Please try again.');
            }
            this.cdr.markForCheck();
          }
        });
      },
      error: () => {
        const user = this.currentUser();
        if (user?.canteenId) {
          this.fetchDirectCanteenData(user.canteenId);
        } else {
          this.loading.set(false);
          this.cdr.markForCheck();
        }
      }
    });
  }

  private fetchDirectCanteenData(canteenId: number): void {
    forkJoin({
      canteen: this.canteenService.getById(canteenId),
      categories: this.categoryService.getAllByCanteen(canteenId),
      products: this.productService.getByCanteen(canteenId, false)
    }).subscribe({
      next: ({ canteen, categories, products }) => {
        this.activeCanteen.set(canteen);
        this.categories.set(categories || []);
        this.products.set(products || []);
        this.loading.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load menu data.');
        this.cdr.markForCheck();
      }
    });
  }

  loadOwnerCanteensList(): void {
    this.canteenService.getMyCanteens().subscribe({
      next: (list) => {
        this.canteens.set(list || []);
        this.cdr.markForCheck();
      },
      error: () => {}
    });
  }

  toggleCanteenSelector(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isCanteenSelectorOpen.update(v => !v);
    this.closeFilterDropdowns();
  }

  closeCanteenSelector(): void {
    this.isCanteenSelectorOpen.set(false);
  }

  switchActiveCanteen(canteen: CanteenSummaryDto): void {
    this.closeCanteenSelector();
    if (canteen.status === 'Setup Pending') {
      this.router.navigate(['/canteen-setup'], { queryParams: { step: '1', new: 'true' } });
      return;
    }

    if (canteen.canteenId === this.currentUser()?.canteenId) {
      return;
    }

    this.isSwitching.set(true);
    this.products.set([]);
    this.categories.set([]);
    this.activeCanteen.set(null);
    this.cdr.markForCheck();

    this.authService.switchCanteen(canteen.canteenId).subscribe({
      next: () => {
        this.isSwitching.set(false);
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { canteenId: canteen.canteenId },
          queryParamsHandling: 'merge'
        });
        this.loadCanteenFoodData();
        this.loadOwnerCanteensList();
      },
      error: (err) => {
        this.isSwitching.set(false);
        alert(err.error?.message || 'Failed to switch canteen.');
      }
    });
  }

  // Quick Filter Pill Click
  setQuickPill(type: string): void {
    this.activeQuickPill.set(type);
    this.currentPage.set(1);

    if (type === 'all') {
      this.selectedCategory.set('all');
      this.selectedType.set('all');
    } else if (type === 'veg') {
      this.selectedType.set('veg');
    } else if (type === 'non-veg') {
      this.selectedType.set('non-veg');
    } else if (type.startsWith('cat_')) {
      const catId = Number(type.replace('cat_', ''));
      this.selectedCategory.set(catId);
      this.selectedType.set('all');
    }
    this.cdr.markForCheck();
  }

  // Dropdown filter helpers
  toggleCategoryDropdown(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isCategoryDropdownOpen.update(v => !v);
    this.isTypeDropdownOpen.set(false);
    this.isStatusDropdownOpen.set(false);
    this.isPageSizeDropdownOpen.set(false);
  }

  toggleTypeDropdown(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isTypeDropdownOpen.update(v => !v);
    this.isCategoryDropdownOpen.set(false);
    this.isStatusDropdownOpen.set(false);
    this.isPageSizeDropdownOpen.set(false);
  }

  toggleStatusDropdown(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isStatusDropdownOpen.update(v => !v);
    this.isCategoryDropdownOpen.set(false);
    this.isTypeDropdownOpen.set(false);
    this.isPageSizeDropdownOpen.set(false);
  }

  togglePageSizeDropdown(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isPageSizeDropdownOpen.update(v => !v);
  }

  closeFilterDropdowns(): void {
    this.isCategoryDropdownOpen.set(false);
    this.isTypeDropdownOpen.set(false);
    this.isStatusDropdownOpen.set(false);
    this.isPageSizeDropdownOpen.set(false);
  }

  selectCategoryFilter(catId: number | 'all'): void {
    this.selectedCategory.set(catId);
    this.currentPage.set(1);
    this.activeQuickPill.set(catId === 'all' ? 'all' : `cat_${catId}`);
    this.closeFilterDropdowns();
  }

  selectTypeFilter(type: 'all' | 'veg' | 'non-veg'): void {
    this.selectedType.set(type);
    this.currentPage.set(1);
    this.activeQuickPill.set(type);
    this.closeFilterDropdowns();
  }

  selectStatusFilter(status: 'all' | 'active' | 'inactive'): void {
    this.selectedStatus.set(status);
    this.currentPage.set(1);
    this.closeFilterDropdowns();
  }

  setPageSize(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1);
    this.closeFilterDropdowns();
  }

  // Sorting
  toggleSort(field: 'name' | 'category' | 'type' | 'price' | 'status'): void {
    if (this.sortField() === field) {
      this.sortDirection.update(d => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortField.set(field);
      this.sortDirection.set('asc');
    }
  }

  // Selection Checkboxes
  toggleSelectAll(checked: boolean): void {
    const set = new Set<number>(this.selectedProductIds());
    const visible = this.paginatedProducts();
    if (checked) {
      visible.forEach(p => set.add(p.productId));
    } else {
      visible.forEach(p => set.delete(p.productId));
    }
    this.selectedProductIds.set(set);
  }

  toggleSelectProduct(productId: number, checked: boolean): void {
    const set = new Set<number>(this.selectedProductIds());
    if (checked) {
      set.add(productId);
    } else {
      set.delete(productId);
    }
    this.selectedProductIds.set(set);
  }

  isProductSelected(productId: number): boolean {
    return this.selectedProductIds().has(productId);
  }

  // Pagination navigation
  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  // Category & Type Helpers
  getCategoryName(categoryId: number): string {
    const cat = this.categories().find(c => c.categoryId === categoryId);
    return cat ? cat.name : `Category #${categoryId}`;
  }

  getCategoryBadgeClass(categoryId: number): string {
    const name = (this.getCategoryName(categoryId) || '').toLowerCase();
    if (name.includes('main')) return 'badge-main-course';
    if (name.includes('breakfast')) return 'badge-breakfast';
    if (name.includes('beverage') || name.includes('drink')) return 'badge-beverages';
    if (name.includes('snack')) return 'badge-snacks';
    if (name.includes('dessert')) return 'badge-dessert';
    return 'badge-default';
  }

  getCategoryIcon(name: string): string {
    const n = (name || '').toLowerCase();
    if (n.includes('beverage') || n.includes('drink') || n.includes('coffee') || n.includes('tea')) return '🥤';
    if (n.includes('main') || n.includes('course') || n.includes('thali') || n.includes('lunch') || n.includes('dinner')) return '🍲';
    if (n.includes('snack') || n.includes('fast food') || n.includes('chaat') || n.includes('fry') || n.includes('fries')) return '🥨';
    if (n.includes('breakfast') || n.includes('tiffin') || n.includes('dosa') || n.includes('idli')) return '🍳';
    if (n.includes('dessert') || n.includes('sweet') || n.includes('ice cream')) return '🍰';
    return '🍽️';
  }

  getCategoryProductCount(categoryId: number): number {
    return this.products().filter(p => p.categoryId === categoryId).length;
  }

  onImageError(event: Event): void {
    const target = event.target as HTMLImageElement;
    if (target && !target.src.endsWith('/food-placeholder.png')) {
      target.src = '/food-placeholder.png';
    }
  }

  isVeg(product: ProductDto): boolean {
    const text = ((product.name || '') + ' ' + (product.description || '')).toLowerCase();
    if (
      text.includes('chicken') ||
      text.includes('egg') ||
      text.includes('fish') ||
      text.includes('meat') ||
      text.includes('mutton') ||
      text.includes('pork') ||
      text.includes('non-veg') ||
      text.includes('non veg') ||
      text.includes('prawn')
    ) {
      return false;
    }
    return true;
  }

  // Add / Edit Modal Controls
  openAddModal(): void {
    this.isEditMode.set(false);
    this.editingProductId = null;
    this.formName = '';
    this.formCategoryId = this.categories().length > 0 ? this.categories()[0].categoryId : null;
    this.formPrice = null;
    this.formStock = 50;
    this.formIsAvailable = true;
    this.formDescription = '';
    this.formImageUrl = '';
    this.imageFileName.set('');
    this.imageUploadError.set(null);
    this.modalError.set('');
    this.modalSuccess.set('');
    this.showModal.set(true);
    this.cdr.markForCheck();
  }

  openEditModal(product: ProductDto): void {
    this.isEditMode.set(true);
    this.editingProductId = product.productId;
    this.formName = product.name;
    this.formCategoryId = product.categoryId;
    this.formPrice = Number(product.price);
    this.formStock = product.stockQuantity;
    this.formIsAvailable = product.isAvailable;
    this.formDescription = product.description || '';
    this.formImageUrl = product.imageUrl || '';
    this.imageFileName.set(product.imageUrl ? (product.name + '-photo.jpg') : '');
    this.imageUploadError.set(null);
    this.modalError.set('');
    this.modalSuccess.set('');
    this.showModal.set(true);
    this.cdr.markForCheck();
  }

  closeModal(): void {
    this.showModal.set(false);
    this.imageUploadError.set(null);
    this.modalError.set('');
    this.modalSuccess.set('');
    this.cdr.markForCheck();
  }

  // Dish Photo Upload Handlers
  onImageFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.processImageFile(input.files[0]);
    }
  }

  onImageDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingImage.set(true);
  }

  onImageDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingImage.set(false);
  }

  onImageDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingImage.set(false);
    if (event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files.length > 0) {
      this.processImageFile(event.dataTransfer.files[0]);
    }
  }

  processImageFile(file: File): void {
    this.imageUploadError.set(null);

    if (!file.type.startsWith('image/')) {
      this.imageUploadError.set('Please select a valid image file (JPG, PNG, WebP).');
      this.cdr.markForCheck();
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.imageUploadError.set('Image file exceeds the 5MB size limit. Please choose a smaller photo.');
      this.cdr.markForCheck();
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      const rawDataUrl = e.target?.result as string;
      if (!rawDataUrl) return;

      const img = new Image();
      img.onload = () => {
        const maxWidth = 800;
        const maxHeight = 800;
        let width = img.width;
        let height = img.height;

        if (width > maxWidth || height > maxHeight) {
          if (width / height > maxWidth / maxHeight) {
            height = Math.round((height * maxWidth) / width);
            width = maxWidth;
          } else {
            width = Math.round((width * maxHeight) / height);
            height = maxHeight;
          }
        }

        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.drawImage(img, 0, 0, width, height);
          const compressedDataUrl = canvas.toDataURL('image/jpeg', 0.85);
          this.formImageUrl = compressedDataUrl;
          this.imageFileName.set(file.name);
        } else {
          this.formImageUrl = rawDataUrl;
          this.imageFileName.set(file.name);
        }
        this.cdr.markForCheck();
      };
      img.onerror = () => {
        this.formImageUrl = rawDataUrl;
        this.imageFileName.set(file.name);
        this.cdr.markForCheck();
      };
      img.src = rawDataUrl;
    };
    reader.onerror = () => {
      this.imageUploadError.set('Failed to read image file. Please try again.');
      this.cdr.markForCheck();
    };
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    this.formImageUrl = '';
    this.imageFileName.set('');
    this.imageUploadError.set(null);
    this.cdr.markForCheck();
  }

  saveProduct(): void {
    this.modalError.set('');
    this.modalSuccess.set('');

    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) {
      this.modalError.set('No active canteen selected.');
      return;
    }

    const name = this.formName.trim();
    if (!name || name.length < 2) {
      this.modalError.set('Product name must be between 2 and 100 characters.');
      return;
    }

    if (!this.formCategoryId) {
      this.modalError.set('Please select a category for this dish.');
      return;
    }

    const price = Number(this.formPrice);
    if (isNaN(price) || price <= 0) {
      this.modalError.set('Please provide a valid price greater than ₹0.');
      return;
    }

    this.modalSaving.set(true);
    this.cdr.markForCheck();

    if (this.isEditMode() && this.editingProductId) {
      const payload: UpdateProductRequest = {
        canteenId,
        categoryId: this.formCategoryId,
        name,
        description: this.formDescription.trim() || undefined,
        price,
        stockQuantity: Number(this.formStock) || 0,
        isAvailable: this.formIsAvailable,
        imageUrl: this.formImageUrl.trim() || undefined
      };

      this.productService.update(this.editingProductId, payload, canteenId).subscribe({
        next: (updated) => {
          this.modalSaving.set(false);
          this.products.update(prev =>
            prev.map(p => (p.productId === updated.productId ? updated : p))
          );
          this.closeModal();
          this.cdr.markForCheck();
        },
        error: (err: HttpErrorResponse) => {
          this.modalSaving.set(false);
          this.modalError.set(err.error?.message || 'Failed to update food item. Please try again.');
          this.cdr.markForCheck();
        }
      });
    } else {
      const payload: CreateProductRequest = {
        canteenId,
        categoryId: this.formCategoryId,
        name,
        description: this.formDescription.trim() || undefined,
        price,
        stockQuantity: Number(this.formStock) || 50,
        imageUrl: this.formImageUrl.trim() || undefined
      };

      this.productService.create(payload, canteenId).subscribe({
        next: (created) => {
          this.modalSaving.set(false);
          this.products.update(prev => [created, ...prev]);
          this.closeModal();
          this.cdr.markForCheck();
        },
        error: (err: HttpErrorResponse) => {
          this.modalSaving.set(false);
          this.modalError.set(err.error?.message || 'Failed to create food item. Please try again.');
          this.cdr.markForCheck();
        }
      });
    }
  }

  // Toggle Availability
  toggleAvailability(product: ProductDto): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    const previousStatus = product.isAvailable;
    const newStatus = !previousStatus;

    // Optimistic UI update
    this.products.update(prev =>
      prev.map(p => (p.productId === product.productId ? { ...p, isAvailable: newStatus } : p))
    );
    this.cdr.markForCheck();

    const payload: UpdateProductRequest = {
      canteenId,
      categoryId: product.categoryId,
      name: product.name,
      description: product.description,
      price: product.price,
      stockQuantity: product.stockQuantity,
      isAvailable: newStatus,
      imageUrl: product.imageUrl
    };

    this.productService.update(product.productId, payload, canteenId).subscribe({
      next: (updated) => {
        this.products.update(prev =>
          prev.map(p => (p.productId === updated.productId ? updated : p))
        );
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        // Revert on failure
        this.products.update(prev =>
          prev.map(p => (p.productId === product.productId ? { ...p, isAvailable: previousStatus } : p))
        );
        this.cdr.markForCheck();
        alert(err.error?.message || 'Failed to update item availability. Reverted.');
      }
    });
  }

  // Delete Product
  deleteProduct(product: ProductDto): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    if (!confirm(`Are you sure you want to delete "${product.name}" from your menu?`)) {
      return;
    }

    this.productService.delete(product.productId, canteenId).subscribe({
      next: () => {
        this.products.update(prev => prev.filter(p => p.productId !== product.productId));
        const set = new Set<number>(this.selectedProductIds());
        set.delete(product.productId);
        this.selectedProductIds.set(set);
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        alert(err.error?.message || 'Failed to delete food item.');
      }
    });
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
