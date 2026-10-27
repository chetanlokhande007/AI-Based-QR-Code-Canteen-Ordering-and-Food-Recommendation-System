import { Component, ChangeDetectorRef, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { OrderService } from '../../core/services/order.service';
import { ProductService } from '../../core/services/product.service';
import { CategoryService } from '../../core/services/category.service';
import { SignalRService } from '../../core/services/signalr.service';
import { ThemeService } from '../../core/services/theme.service';
import { CanteenDto, CanteenSummaryDto } from '../../core/models/canteen.models';
import { OrderDto } from '../../core/models/order.models';
import { ProductDto, CreateProductRequest } from '../../core/models/product.models';
import { CategoryDto, CreateCategoryRequest } from '../../core/models/category.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class Dashboard implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly orderService = inject(OrderService);
  private readonly productService = inject(ProductService);
  private readonly categoryService = inject(CategoryService);
  private readonly signalRService = inject(SignalRService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly subscriptions = new Subscription();

  // Current authenticated user from AuthService
  readonly currentUser = this.authService.currentUser;
  readonly activeTheme = this.themeService.activeTheme;

  // Real-time date display
  readonly currentDate = new Date().toLocaleDateString('en-US', {
    weekday: 'long',
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  });

  // State signals
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly canteenDetails = signal<CanteenDto | null>(null);
  readonly orders = signal<OrderDto[]>([]);
  readonly products = signal<ProductDto[]>([]);
  readonly categories = signal<CategoryDto[]>([]);

  // Multi-Canteen signals
  readonly canteens = signal<CanteenSummaryDto[]>([]);
  readonly isCanteenSelectorOpen = signal<boolean>(false);
  readonly isSwitching = signal<boolean>(false);
  readonly currentCanteenName = computed(() => {
    return this.canteenDetails()?.name || (this.currentUser()?.canteenId ? ('Canteen #' + this.currentUser()?.canteenId) : 'Select Canteen');
  });

  // Menu & Food Catalog Modal State
  readonly showMenuModal = signal(false);
  readonly menuModalTab = signal<'categories' | 'products'>('categories');

  // Category form inputs
  newCategoryName = '';
  newCategoryDesc = '';
  readonly categorySaving = signal(false);
  readonly categoryError = signal('');
  readonly categorySuccess = signal('');

  // Food Item form inputs
  newProductName = '';
  newProductCategoryId: number | null = null;
  newProductPrice: number | null = null;
  newProductDesc = '';
  newProductStock = 50;
  newProductAvailable = true;
  readonly productSaving = signal(false);
  readonly productError = signal('');
  readonly productSuccess = signal('');
  readonly selectedCategoryFilter = signal<number | 'all'>('all');

  // Computed Business Metrics based strictly on real API data
  readonly todayOrdersCount = computed(() => {
    const list = this.orders();
    return list.filter((o) => this.isOrderFromToday(o.createdAt)).length;
  });

  readonly todayRevenue = computed(() => {
    const list = this.orders();
    const todayOrders = list.filter((o) => this.isOrderFromToday(o.createdAt));
    return todayOrders
      .filter((o) => {
        const pStatus = (o.paymentStatus || '').toLowerCase();
        return pStatus === 'completed' || pStatus === 'paid';
      })
      .reduce((sum, o) => sum + (Number(o.totalAmount) || 0), 0);
  });

  readonly activeKitchenOrdersCount = computed(() => {
    const list = this.orders();
    return list.filter((o) => {
      const status = (o.status || '').toLowerCase();
      return status === 'pending' || status === 'preparing' || status === 'ready';
    }).length;
  });

  readonly totalProductsCount = computed(() => this.products().length);
  readonly totalCategoriesCount = computed(() => this.categories().length);
  readonly availableProductsCount = computed(() => this.products().filter(p => p.isAvailable).length);

  readonly recentOrdersList = computed(() => {
    return this.orders().slice(0, 10);
  });

  readonly filteredProducts = computed(() => {
    const filter = this.selectedCategoryFilter();
    const list = this.products();
    if (filter === 'all') {
      return list;
    }
    return list.filter(p => p.categoryId === filter);
  });

  get ownerInitials(): string {
    const name = this.currentUser()?.fullName?.trim();
    if (!name) return 'CO';
    const parts = name.split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  ngOnInit(): void {
    this.loadDashboardData();
    this.loadOwnerCanteensList();
    this.listenToRealtimeEvents();

    this.subscriptions.add(
      this.route.queryParams.subscribe(params => {
        if (params['openMenu'] === 'products') {
          this.openMenuModal('products');
        } else if (params['openMenu'] === 'categories') {
          this.openMenuModal('categories');
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.signalRService.stopConnection();
  }

  loadDashboardData(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.cdr.markForCheck();

    // 1. Get latest profile from backend to ensure accurate CanteenId
    this.authService.getProfile().subscribe({
      next: (user) => {
        const canteenId = user.canteenId;
        if (!canteenId) {
          // Owner has no assigned canteen branch yet
          this.canteenDetails.set(null);
          this.orders.set([]);
          this.products.set([]);
          this.categories.set([]);
          this.loading.set(false);
          this.cdr.markForCheck();
          return;
        }

        // 2. Fetch canteen info, orders, products, and categories concurrently via forkJoin
        forkJoin({
          canteen: this.canteenService.getById(canteenId),
          orders: this.orderService.getOrdersByCanteen(canteenId),
          products: this.productService.getByCanteen(canteenId),
          categories: this.categoryService.getAllByCanteen(canteenId)
        }).subscribe({
          next: ({ canteen, orders, products, categories }) => {
            this.canteenDetails.set(canteen);
            this.orders.set(orders || []);
            this.products.set(products || []);
            this.categories.set(categories || []);
            this.loading.set(false);
            this.cdr.markForCheck();

            // Connect to real-time SignalR hub for live kitchen order updates
            this.signalRService.startConnection();
          },
          error: (err: HttpErrorResponse) => {
            this.loading.set(false);
            if (err.status === 403) {
              this.errorMessage.set('Unauthorized tenant access. You can only view your own canteen.');
            } else if (err.status === 404) {
              this.errorMessage.set('Assigned canteen could not be found.');
            } else {
              this.errorMessage.set('Failed to load canteen dashboard data. Please try again.');
            }
            this.cdr.markForCheck();
          }
        });
      },
      error: () => {
        // Fallback to locally stored user if profile endpoint is unreachable
        const user = this.currentUser();
        if (user?.canteenId) {
          this.fetchCanteenData(user.canteenId);
        } else {
          this.loading.set(false);
          this.cdr.markForCheck();
        }
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
    // Clear previous canteen data immediately so no stale data is visible
    this.canteenDetails.set(null);
    this.orders.set([]);
    this.products.set([]);
    this.categories.set([]);
    this.cdr.markForCheck();

    this.authService.switchCanteen(canteen.canteenId).subscribe({
      next: () => {
        this.isSwitching.set(false);
        this.loadDashboardData();
        this.loadOwnerCanteensList();
      },
      error: (err) => {
        this.isSwitching.set(false);
        alert(err.error?.message || 'Failed to switch canteen.');
      }
    });
  }

  goToMyCanteens(): void {
    this.router.navigate(['/my-canteens']);
  }

  addNewCanteen(): void {
    this.router.navigate(['/canteen-setup'], { queryParams: { step: '1', new: 'true' } });
  }

  private fetchCanteenData(canteenId: number): void {
    forkJoin({
      canteen: this.canteenService.getById(canteenId),
      orders: this.orderService.getOrdersByCanteen(canteenId),
      products: this.productService.getByCanteen(canteenId),
      categories: this.categoryService.getAllByCanteen(canteenId)
    }).subscribe({
      next: ({ canteen, orders, products, categories }) => {
        this.canteenDetails.set(canteen);
        this.orders.set(orders || []);
        this.products.set(products || []);
        this.categories.set(categories || []);
        this.loading.set(false);
        this.cdr.markForCheck();
        this.signalRService.startConnection();
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load canteen data from server.');
        this.cdr.markForCheck();
      }
    });
  }

  refreshMenuData(): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    forkJoin({
      products: this.productService.getByCanteen(canteenId),
      categories: this.categoryService.getAllByCanteen(canteenId)
    }).subscribe({
      next: ({ products, categories }) => {
        this.products.set(products || []);
        this.categories.set(categories || []);
        this.cdr.markForCheck();
      }
    });
  }

  private listenToRealtimeEvents(): void {
    // When a customer places a new order via table QR
    this.subscriptions.add(
      this.signalRService.newOrder$.subscribe((notif) => {
        const newOrder: OrderDto = {
          orderId: notif.orderId,
          orderNumber: notif.orderNumber,
          canteenId: notif.canteenId,
          tableId: notif.tableId,
          status: notif.status,
          paymentStatus: 'Pending',
          totalAmount: notif.totalAmount,
          trackingToken: '',
          createdAt: notif.createdAt || new Date().toISOString(),
          items: notif.items.map((i) => ({
            productId: i.productId,
            productName: i.productName,
            quantity: i.quantity,
            unitPrice: i.quantity > 0 ? i.subtotal / i.quantity : 0,
            subtotal: i.subtotal
          }))
        };

        this.orders.update((prev) => [newOrder, ...prev.filter((o) => o.orderId !== newOrder.orderId)]);
        this.cdr.markForCheck();
      })
    );

    // When payment is confirmed via Razorpay
    this.subscriptions.add(
      this.signalRService.paymentConfirmed$.subscribe((notif) => {
        this.orders.update((prev) =>
          prev.map((o) => (o.orderId === notif.orderId ? { ...o, paymentStatus: 'Completed' } : o))
        );
        this.cdr.markForCheck();
      })
    );

    // When order status changes in kitchen
    this.subscriptions.add(
      this.signalRService.orderStatusUpdated$.subscribe((notif) => {
        this.orders.update((prev) =>
          prev.map((o) => (o.orderId === notif.orderId ? { ...o, status: notif.newStatus } : o))
        );
        this.cdr.markForCheck();
      })
    );
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  openSetupWizard(): void {
    this.router.navigate(['/canteen-setup']);
  }

  // Menu Modal controls
  openMenuModal(tab: 'categories' | 'products' = 'categories'): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) {
      this.openSetupWizard();
      return;
    }
    this.menuModalTab.set(tab);
    this.categoryError.set('');
    this.categorySuccess.set('');
    this.productError.set('');
    this.productSuccess.set('');
    this.showMenuModal.set(true);
    this.cdr.markForCheck();
  }

  closeMenuModal(): void {
    this.showMenuModal.set(false);
    this.cdr.markForCheck();
  }

  switchMenuModalTab(tab: 'categories' | 'products'): void {
    this.menuModalTab.set(tab);
    this.categoryError.set('');
    this.categorySuccess.set('');
    this.productError.set('');
    this.productSuccess.set('');
    this.cdr.markForCheck();
  }

  // Category Actions
  saveCategory(): void {
    this.categoryError.set('');
    this.categorySuccess.set('');

    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    const name = this.newCategoryName.trim();
    if (!name || name.length < 2) {
      this.categoryError.set('Category name must be at least 2 characters.');
      this.cdr.markForCheck();
      return;
    }

    this.categorySaving.set(true);
    this.cdr.markForCheck();

    const payload: CreateCategoryRequest = {
      name,
      description: this.newCategoryDesc.trim() || undefined
    };

    this.categoryService.create(canteenId, payload).subscribe({
      next: (created) => {
        this.categorySaving.set(false);
        this.newCategoryName = '';
        this.newCategoryDesc = '';
        this.categorySuccess.set(`Category "${created.name}" created successfully!`);
        this.refreshMenuData();
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        this.categorySaving.set(false);
        this.categoryError.set(err.error?.message || 'Failed to create category. Please try again.');
        this.cdr.markForCheck();
      }
    });
  }

  deleteCategory(categoryId: number): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    if (!confirm('Are you sure you want to delete this category?')) return;

    this.categoryService.delete(canteenId, categoryId).subscribe({
      next: () => {
        this.categorySuccess.set('Category deleted successfully.');
        this.refreshMenuData();
      },
      error: (err: HttpErrorResponse) => {
        this.categoryError.set(err.error?.message || 'Failed to delete category. Ensure no products belong to it.');
        this.cdr.markForCheck();
      }
    });
  }

  getCategoryItemCount(categoryId: number): number {
    return this.products().filter(p => p.categoryId === categoryId).length;
  }

  getCategoryName(categoryId: number): string {
    const cat = this.categories().find(c => c.categoryId === categoryId);
    return cat ? cat.name : `Category #${categoryId}`;
  }

  // Food Item (Product) Actions
  saveProduct(): void {
    this.productError.set('');
    this.productSuccess.set('');

    const name = this.newProductName.trim();
    if (!name || name.length < 2) {
      this.productError.set('Product name must be at least 2 characters.');
      this.cdr.markForCheck();
      return;
    }

    if (!this.newProductCategoryId) {
      this.productError.set('Please select a category for this food item.');
      this.cdr.markForCheck();
      return;
    }

    const price = Number(this.newProductPrice);
    if (isNaN(price) || price <= 0) {
      this.productError.set('Please specify a valid price greater than ₹0.');
      this.cdr.markForCheck();
      return;
    }

    this.productSaving.set(true);
    this.cdr.markForCheck();

    const payload: CreateProductRequest = {
      categoryId: this.newProductCategoryId,
      name,
      description: this.newProductDesc.trim() || undefined,
      price,
      stockQuantity: this.newProductStock || 50
    };

    this.productService.create(payload).subscribe({
      next: (created) => {
        this.productSaving.set(false);
        this.newProductName = '';
        this.newProductPrice = null;
        this.newProductDesc = '';
        this.newProductStock = 50;
        this.productSuccess.set(`Food item "${created.name}" created successfully!`);
        this.refreshMenuData();
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        this.productSaving.set(false);
        this.productError.set(err.error?.message || 'Failed to create food item. Please try again.');
        this.cdr.markForCheck();
      }
    });
  }

  toggleProductAvailability(product: ProductDto): void {
    this.productService.update(product.productId, {
      name: product.name,
      description: product.description,
      price: product.price,
      stockQuantity: product.stockQuantity,
      categoryId: product.categoryId,
      isAvailable: !product.isAvailable
    }).subscribe({
      next: (updated) => {
        this.products.update(prev =>
          prev.map(p => (p.productId === updated.productId ? updated : p))
        );
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        alert(err.error?.message || 'Failed to update item availability.');
      }
    });
  }

  deleteProduct(productId: number): void {
    if (!confirm('Are you sure you want to remove this food item from your catalog?')) return;

    this.productService.delete(productId).subscribe({
      next: () => {
        this.productSuccess.set('Food item removed successfully.');
        this.refreshMenuData();
      },
      error: (err: HttpErrorResponse) => {
        this.productError.set(err.error?.message || 'Failed to remove food item.');
        this.cdr.markForCheck();
      }
    });
  }

  setCategoryFilter(filter: number | 'all'): void {
    this.selectedCategoryFilter.set(filter);
    this.cdr.markForCheck();
  }

  formatOrderItems(order: OrderDto): string {
    if (!order.items || order.items.length === 0) {
      return 'No items listed';
    }
    return order.items.map((i) => `${i.quantity}x ${i.productName}`).join(', ');
  }

  getTimeAgo(dateStr: string): string {
    if (!dateStr) return 'Recently';
    const diffMs = Date.now() - new Date(dateStr).getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    return new Date(dateStr).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  private isOrderFromToday(dateStr: string): boolean {
    if (!dateStr) return false;
    const orderDate = new Date(dateStr);
    const today = new Date();
    return (
      orderDate.getFullYear() === today.getFullYear() &&
      orderDate.getMonth() === today.getMonth() &&
      orderDate.getDate() === today.getDate()
    );
  }

  logout(): void {
    this.signalRService.stopConnection();
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
