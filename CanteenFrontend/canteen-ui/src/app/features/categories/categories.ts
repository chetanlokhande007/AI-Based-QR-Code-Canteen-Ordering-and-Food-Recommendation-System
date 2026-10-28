import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { CategoryService } from '../../core/services/category.service';
import { ProductService } from '../../core/services/product.service';
import { ThemeService } from '../../core/services/theme.service';
import { CategoryDto, CreateCategoryRequest } from '../../core/models/category.models';
import { ProductDto } from '../../core/models/product.models';
import { CanteenSummaryDto, CanteenDto } from '../../core/models/canteen.models';

@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './categories.html',
  styleUrl: './categories.css'
})
export class Categories implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly categoryService = inject(CategoryService);
  private readonly productService = inject(ProductService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly subscriptions = new Subscription();

  readonly currentUser = this.authService.currentUser;
  readonly activeTheme = this.themeService.activeTheme;

  readonly categories = signal<CategoryDto[]>([]);
  readonly products = signal<ProductDto[]>([]);
  readonly canteens = signal<CanteenSummaryDto[]>([]);
  readonly activeCanteen = signal<CanteenDto | null>(null);

  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');
  readonly isCanteenSelectorOpen = signal<boolean>(false);

  // Search & Filter
  searchTerm = '';

  // Add / Edit Modal
  readonly showModal = signal<boolean>(false);
  readonly isEditMode = signal<boolean>(false);
  editingCategoryId: number | null = null;
  formName = '';
  formDescription = '';
  readonly modalSaving = signal<boolean>(false);
  readonly modalError = signal<string>('');

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

  readonly filteredCategories = computed(() => {
    let list = [...this.categories()];
    const q = this.searchTerm.trim().toLowerCase();
    if (q) {
      list = list.filter(c =>
        c.name.toLowerCase().includes(q) ||
        (c.description && c.description.toLowerCase().includes(q))
      );
    }
    return list;
  });

  ngOnInit(): void {
    this.loadOwnerCanteens();
    this.subscriptions.add(
      this.route.queryParams.subscribe(params => {
        const qCanteenId = params['canteenId'] ? Number(params['canteenId']) : null;
        if (qCanteenId && qCanteenId !== this.currentUser()?.canteenId) {
          this.authService.switchCanteen(qCanteenId).subscribe({
            next: () => {
              this.loadCategoriesData();
              this.loadOwnerCanteens();
            },
            error: () => this.loadCategoriesData()
          });
        } else {
          this.loadCategoriesData();
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

  loadCategoriesData(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.cdr.markForCheck();

    this.authService.getProfile().subscribe({
      next: (user) => {
        const canteenId = user.canteenId;
        if (!canteenId) {
          this.categories.set([]);
          this.products.set([]);
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
            this.errorMessage.set(err.status === 403 ? 'Unauthorized canteen access.' : 'Failed to load categories.');
            this.cdr.markForCheck();
          }
        });
      },
      error: () => {
        this.loading.set(false);
        this.cdr.markForCheck();
      }
    });
  }

  loadOwnerCanteens(): void {
    this.canteenService.getMyCanteens().subscribe({
      next: (list) => {
        this.canteens.set(list || []);
        this.cdr.markForCheck();
      },
      error: () => {}
    });
  }

  getCategoryItemCount(categoryId: number): number {
    return this.products().filter(p => p.categoryId === categoryId).length;
  }

  getCategoryIcon(name: string): string {
    const n = (name || '').toLowerCase();
    if (n.includes('beverage') || n.includes('drink') || n.includes('coffee') || n.includes('tea')) return '🥤';
    if (n.includes('main') || n.includes('course') || n.includes('thali') || n.includes('lunch') || n.includes('dinner')) return '🍲';
    if (n.includes('snack') || n.includes('fast food') || n.includes('chaat') || n.includes('fry') || n.includes('fries')) return '🥨';
    if (n.includes('breakfast') || n.includes('tiffin') || n.includes('dosa') || n.includes('idli')) return '🍳';
    if (n.includes('dessert') || n.includes('sweet') || n.includes('ice cream')) return '🍰';
    return '📁';
  }

  toggleCanteenSelector(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isCanteenSelectorOpen.update(v => !v);
  }

  closeCanteenSelector(): void {
    this.isCanteenSelectorOpen.set(false);
  }

  switchActiveCanteen(canteen: CanteenSummaryDto): void {
    this.closeCanteenSelector();
    if (canteen.canteenId === this.currentUser()?.canteenId) return;

    this.authService.switchCanteen(canteen.canteenId).subscribe({
      next: () => {
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { canteenId: canteen.canteenId },
          queryParamsHandling: 'merge'
        });
        this.loadCategoriesData();
        this.loadOwnerCanteens();
      },
      error: (err) => alert(err.error?.message || 'Failed to switch canteen.')
    });
  }

  openAddModal(): void {
    this.isEditMode.set(false);
    this.editingCategoryId = null;
    this.formName = '';
    this.formDescription = '';
    this.modalError.set('');
    this.showModal.set(true);
    this.cdr.markForCheck();
  }

  openEditModal(cat: CategoryDto): void {
    this.isEditMode.set(true);
    this.editingCategoryId = cat.categoryId;
    this.formName = cat.name;
    this.formDescription = cat.description || '';
    this.modalError.set('');
    this.showModal.set(true);
    this.cdr.markForCheck();
  }

  closeModal(): void {
    this.showModal.set(false);
    this.modalError.set('');
    this.cdr.markForCheck();
  }

  saveCategory(): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    const name = this.formName.trim();
    if (!name || name.length < 2) {
      this.modalError.set('Category name must be at least 2 characters.');
      return;
    }

    this.modalSaving.set(true);
    this.cdr.markForCheck();

    if (this.isEditMode() && this.editingCategoryId) {
      this.categoryService.update(canteenId, this.editingCategoryId, {
        name,
        description: this.formDescription.trim() || undefined
      }).subscribe({
        next: (updated) => {
          this.modalSaving.set(false);
          this.categories.update(prev =>
            prev.map(c => (c.categoryId === updated.categoryId ? updated : c))
          );
          this.closeModal();
          this.cdr.markForCheck();
        },
        error: (err: HttpErrorResponse) => {
          this.modalSaving.set(false);
          this.modalError.set(err.error?.message || 'Failed to update category.');
          this.cdr.markForCheck();
        }
      });
    } else {
      const payload: CreateCategoryRequest = {
        name,
        description: this.formDescription.trim() || undefined
      };

      this.categoryService.create(canteenId, payload).subscribe({
        next: (created) => {
          this.modalSaving.set(false);
          this.categories.update(prev => [created, ...prev]);
          this.closeModal();
          this.cdr.markForCheck();
        },
        error: (err: HttpErrorResponse) => {
          this.modalSaving.set(false);
          this.modalError.set(err.error?.message || 'Failed to create category.');
          this.cdr.markForCheck();
        }
      });
    }
  }

  deleteCategory(cat: CategoryDto): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    const count = this.getCategoryItemCount(cat.categoryId);
    if (count > 0) {
      alert(`Cannot delete "${cat.name}" because it still has ${count} food item(s). Reassign or remove dishes first.`);
      return;
    }

    if (!confirm(`Are you sure you want to delete category "${cat.name}"?`)) return;

    this.categoryService.delete(canteenId, cat.categoryId).subscribe({
      next: () => {
        this.categories.update(prev => prev.filter(c => c.categoryId !== cat.categoryId));
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        alert(err.error?.message || 'Failed to delete category.');
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

