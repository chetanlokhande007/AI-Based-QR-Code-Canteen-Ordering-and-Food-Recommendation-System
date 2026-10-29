import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { CanteenService } from '../../core/services/canteen.service';
import { CategoryService } from '../../core/services/category.service';
import { ProductService } from '../../core/services/product.service';
import { OrderService } from '../../core/services/order.service';
import { SignalRService } from '../../core/services/signalr.service';
import { PublicCanteenDto } from '../../core/models/canteen.models';
import { CategoryDto } from '../../core/models/category.models';
import { ProductDto } from '../../core/models/product.models';
import { OrderDto } from '../../core/models/order.models';

export interface CartItem {
  product: ProductDto;
  quantity: number;
}

@Component({
  selector: 'app-customer-menu',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customer-menu.html',
  styleUrl: './customer-menu.css'
})
export class CustomerMenu implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly canteenService = inject(CanteenService);
  private readonly categoryService = inject(CategoryService);
  private readonly productService = inject(ProductService);
  private readonly orderService = inject(OrderService);
  private readonly signalRService = inject(SignalRService);

  private readonly subscriptions = new Subscription();

  // Query / Route Parameters
  readonly canteenId = signal<number>(0);
  readonly tableId = signal<number>(0);
  readonly qrToken = signal<string>('');
  readonly slug = signal<string>('');

  // State Signals
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly canteen = signal<PublicCanteenDto | null>(null);
  readonly categories = signal<CategoryDto[]>([]);
  readonly products = signal<ProductDto[]>([]);
  readonly selectedCategory = signal<number | 'all'>('all');
  readonly searchQuery = signal<string>('');

  // Cart State
  readonly cart = signal<CartItem[]>([]);
  readonly isCartDrawerOpen = signal<boolean>(false);
  readonly isSubmittingOrder = signal<boolean>(false);
  readonly orderError = signal<string | null>(null);

  // Active Placed Order (Tracking View)
  readonly activeOrder = signal<OrderDto | null>(null);

  // Computed Values
  readonly filteredProducts = computed(() => {
    let list = this.products();
    const cat = this.selectedCategory();
    const query = this.searchQuery().trim().toLowerCase();

    if (cat !== 'all') {
      list = list.filter((p) => p.categoryId === cat);
    }

    if (query) {
      list = list.filter(
        (p) =>
          p.name.toLowerCase().includes(query) ||
          (p.description && p.description.toLowerCase().includes(query))
      );
    }

    return list;
  });

  readonly totalCartItems = computed(() => {
    return this.cart().reduce((sum, item) => sum + item.quantity, 0);
  });

  readonly totalCartAmount = computed(() => {
    return this.cart().reduce((sum, item) => sum + item.product.price * item.quantity, 0);
  });

  ngOnInit(): void {
    const slugParam = this.route.snapshot.paramMap.get('slug');
    const params = this.route.snapshot.queryParamMap;
    const cId = Number(params.get('canteenId'));
    const tId = Number(params.get('tableId'));
    const token = params.get('token') || '';

    this.tableId.set(tId);
    this.qrToken.set(token);

    if (slugParam) {
      this.slug.set(slugParam);
      this.loadMenuDataBySlug(slugParam);
    } else if (cId) {
      this.canteenId.set(cId);
      this.loadMenuData(cId);
    } else {
      this.errorMessage.set(
        'Invalid or expired QR code. Please scan the official table QR code again.'
      );
      this.loading.set(false);
      return;
    }

    // Listen to real-time status updates for tracking active order
    this.subscriptions.add(
      this.signalRService.orderStatusUpdated$.subscribe({
        next: (notification) => {
          const current = this.activeOrder();
          if (current && current.orderId === notification.orderId) {
            this.activeOrder.set({
              ...current,
              status: notification.newStatus
            });
          }
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.signalRService.stopConnection();
  }

  loadMenuDataBySlug(slug: string): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.canteenService.getPublicInfoBySlug(slug).subscribe({
      next: (canteen) => {
        this.canteen.set(canteen);
        this.canteenId.set(canteen.canteenId);

        forkJoin({
          categories: this.categoryService.getAllByCanteen(canteen.canteenId),
          products: this.productService.getByCanteen(canteen.canteenId, true)
        }).subscribe({
          next: ({ categories, products }: { categories: CategoryDto[]; products: ProductDto[] }) => {
            this.categories.set(categories || []);
            this.products.set((products || []).filter((p: ProductDto) => p.isAvailable));
            this.loading.set(false);
          },
          error: (err) => {
            console.error('Failed to load menu data by slug:', err);
            this.errorMessage.set('Unable to load canteen menu items.');
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        console.error('Failed to load canteen by slug:', err);
        this.errorMessage.set('Canteen not found or currently unavailable.');
        this.loading.set(false);
      }
    });
  }

  loadMenuData(canteenId: number): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      canteen: this.canteenService.getPublicInfo(canteenId),
      categories: this.categoryService.getAllByCanteen(canteenId),
      products: this.productService.getByCanteen(canteenId, true)
    }).subscribe({
      next: ({ canteen, categories, products }: { canteen: PublicCanteenDto; categories: CategoryDto[]; products: ProductDto[] }) => {
        this.canteen.set(canteen);
        this.categories.set(categories || []);
        this.products.set((products || []).filter((p: ProductDto) => p.isAvailable));
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load menu data:', err);
        this.errorMessage.set(
          'Unable to load canteen menu. Please verify your internet connection or ask the canteen staff.'
        );
        this.loading.set(false);
      }
    });
  }

  selectCategory(id: number | 'all'): void {
    this.selectedCategory.set(id);
  }

  getItemQuantityInCart(productId: number): number {
    const item = this.cart().find((c) => c.product.productId === productId);
    return item ? item.quantity : 0;
  }

  addToCart(product: ProductDto): void {
    if (!product.isAvailable || product.stockQuantity <= 0) return;

    const currentCart = [...this.cart()];
    const existing = currentCart.find((c) => c.product.productId === product.productId);

    if (existing) {
      if (existing.quantity < product.stockQuantity) {
        existing.quantity += 1;
        this.cart.set(currentCart);
      }
    } else {
      currentCart.push({ product, quantity: 1 });
      this.cart.set(currentCart);
    }
  }

  removeFromCart(productId: number): void {
    const currentCart = [...this.cart()];
    const index = currentCart.findIndex((c) => c.product.productId === productId);

    if (index > -1) {
      if (currentCart[index].quantity > 1) {
        currentCart[index].quantity -= 1;
      } else {
        currentCart.splice(index, 1);
      }
      this.cart.set(currentCart);
    }
  }

  openCartDrawer(): void {
    if (this.cart().length > 0) {
      this.isCartDrawerOpen.set(true);
    }
  }

  closeCartDrawer(): void {
    this.isCartDrawerOpen.set(false);
  }

  submitOrder(): void {
    if (this.cart().length === 0) return;

    if (!this.qrToken()) {
      this.orderError.set('Please scan the QR code physically placed on your dining table to submit an order.');
      return;
    }

    this.isSubmittingOrder.set(true);
    this.orderError.set(null);

    const payload = {
      tableId: this.tableId(),
      items: this.cart().map((item) => ({
        productId: item.product.productId,
        quantity: item.quantity
      }))
    };

    this.orderService.createOrder(this.qrToken(), payload).subscribe({
      next: (order) => {
        this.isSubmittingOrder.set(false);
        this.isCartDrawerOpen.set(false);
        this.cart.set([]);
        this.activeOrder.set(order);

        // Join SignalR real-time tracking group
        this.signalRService.startTrackingConnection(order.orderId, order.trackingToken);
      },
      error: (err) => {
        this.isSubmittingOrder.set(false);
        console.error('Order creation error:', err);
        const msg = err?.error?.message || 'Could not place order. Please review your cart and try again.';
        this.orderError.set(msg);
      }
    });
  }

  placeNewOrder(): void {
    this.activeOrder.set(null);
    this.orderError.set(null);
    if (this.slug()) {
      this.loadMenuDataBySlug(this.slug());
    } else {
      this.loadMenuData(this.canteenId());
    }
  }
}
