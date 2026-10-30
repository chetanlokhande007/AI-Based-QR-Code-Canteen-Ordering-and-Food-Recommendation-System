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
import { PaymentService } from '../../core/services/payment.service';
import { PublicCanteenDto } from '../../core/models/canteen.models';
import { CategoryDto } from '../../core/models/category.models';
import { ProductDto } from '../../core/models/product.models';
import { OrderDto } from '../../core/models/order.models';

export interface CartItem {
  product: ProductDto;
  quantity: number;
}

const ACTIVE_ORDER_STORAGE_KEY = 'canteen_hub_active_order';

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
  private readonly paymentService = inject(PaymentService);

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

  // Payment Options & Processing State
  readonly selectedPaymentMethod = signal<'online' | 'counter'>('online');
  readonly isPaymentProcessing = signal<boolean>(false);
  readonly isCancellingOrder = signal<boolean>(false);
  readonly paymentError = signal<string | null>(null);

  // Active Placed Order (Tracking View)
  readonly activeOrder = signal<OrderDto | null>(null);
  readonly paymentSuccessNotification = signal<{
    orderNumber: string;
    amount: number;
    paidAt: string;
  } | null>(null);

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

  readonly cartSubtotal = computed(() => {
    return this.cart().reduce((sum, item) => sum + item.product.price * item.quantity, 0);
  });

  readonly cartTaxAmount = computed(() => {
    const taxRate = this.canteen()?.taxPercentage || 0;
    return Math.round((this.cartSubtotal() * (taxRate / 100)) * 100) / 100;
  });

  readonly platformFeeAmount = computed(() => {
    return 5.00;
  });

  readonly isPlatformFeeAbsorbed = computed(() => {
    return this.canteen()?.feeAbsorptionPolicy === 'CanteenAbsorbs';
  });

  readonly totalCartAmount = computed(() => {
    const subtotal = this.cartSubtotal();
    if (subtotal === 0) return 0;
    const tax = this.cartTaxAmount();
    const fee = this.isPlatformFeeAbsorbed() ? 0 : this.platformFeeAmount();
    return Math.round((subtotal + tax + fee) * 100) / 100;
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

    // Check for an active unfinished order in localStorage
    this.resumeSavedOrderIfAvailable();

    // Listen to real-time payment confirmation notifications
    this.subscriptions.add(
      this.signalRService.paymentConfirmed$.subscribe({
        next: (notification) => {
          const current = this.activeOrder();
          if (current && current.orderId === notification.orderId) {
            this.activeOrder.set({
              ...current,
              paymentStatus: 'Paid',
              status: current.status === 'Pending' ? 'Confirmed' : current.status
            });
            this.paymentSuccessNotification.set({
              orderNumber: notification.orderNumber,
              amount: notification.amount,
              paidAt: notification.paidAt
            });
            this.paymentError.set(null);
          }
        }
      })
    );

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

  private resumeSavedOrderIfAvailable(): void {
    try {
      const savedRaw = localStorage.getItem(ACTIVE_ORDER_STORAGE_KEY);
      if (!savedRaw) return;

      const saved = JSON.parse(savedRaw);
      if (saved && saved.trackingToken) {
        this.orderService.trackOrder(saved.trackingToken).subscribe({
          next: (order) => {
            if (order && order.status !== 'Completed' && order.status !== 'Cancelled') {
              this.activeOrder.set(order);
              if (saved.paymentMethod) {
                this.selectedPaymentMethod.set(saved.paymentMethod);
              }
              this.signalRService.startTrackingConnection(order.orderId, order.trackingToken);
            } else {
              localStorage.removeItem(ACTIVE_ORDER_STORAGE_KEY);
            }
          },
          error: () => {
            localStorage.removeItem(ACTIVE_ORDER_STORAGE_KEY);
          }
        });
      }
    } catch {
      localStorage.removeItem(ACTIVE_ORDER_STORAGE_KEY);
    }
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

    const chosenPaymentMethod = this.selectedPaymentMethod();

    this.orderService.createOrder(this.qrToken(), payload).subscribe({
      next: (order) => {
        this.isSubmittingOrder.set(false);
        this.isCartDrawerOpen.set(false);
        this.cart.set([]);
        this.activeOrder.set(order);

        // Persist active order in localStorage for accidental tab reload recovery
        try {
          localStorage.setItem(
            ACTIVE_ORDER_STORAGE_KEY,
            JSON.stringify({
              orderId: order.orderId,
              trackingToken: order.trackingToken,
              canteenId: order.canteenId,
              tableId: order.tableId,
              paymentMethod: chosenPaymentMethod,
              createdAt: order.createdAt
            })
          );
        } catch {
          // Ignore localStorage quota warnings
        }

        // Join SignalR real-time tracking group
        this.signalRService.startTrackingConnection(order.orderId, order.trackingToken);

        // Process payment according to customer selection
        if (chosenPaymentMethod === 'online') {
          this.initiateOnlinePayment(order.orderId);
        } else {
          this.paymentService.payAtCounter(order.orderId).subscribe({
            next: () => {},
            error: (err) => {
              console.warn('Pay at counter registration warning:', err);
            }
          });
        }
      },
      error: (err) => {
        this.isSubmittingOrder.set(false);
        console.error('Order creation error:', err);
        const msg = err?.error?.message || 'Could not place order. Please review your cart and try again.';
        this.orderError.set(msg);
      }
    });
  }

  initiateOnlinePayment(orderId: number): void {
    this.isPaymentProcessing.set(true);
    this.paymentError.set(null);

    this.paymentService.createPaymentOrder(orderId).subscribe({
      next: async (paymentOrder) => {
        // If backend returned a mock order in development, simulate instant success
        if (paymentOrder.razorpayOrderId.startsWith('order_mock_') || paymentOrder.keyId.includes('mock')) {
          this.verifyOnlinePayment(orderId, {
            razorpay_payment_id: `pay_mock_${Date.now()}`,
            razorpay_order_id: paymentOrder.razorpayOrderId,
            razorpay_signature: 'mock_signature_valid'
          });
          return;
        }

        const loaded = await this.paymentService.loadRazorpayScript();
        if (!loaded || !window.Razorpay) {
          this.isPaymentProcessing.set(false);
          this.paymentError.set('Payment gateway could not be loaded. You can pay with cash at the counter.');
          return;
        }

        const options = {
          key: paymentOrder.keyId,
          amount: paymentOrder.amountInPaise,
          currency: paymentOrder.currency,
          name: this.canteen()?.name || 'Smart Canteen',
          description: `Order #${paymentOrder.orderNumber}`,
          order_id: paymentOrder.razorpayOrderId,
          handler: (response: any) => {
            this.verifyOnlinePayment(orderId, response);
          },
          modal: {
            ondismiss: () => {
              this.isPaymentProcessing.set(false);
              this.paymentError.set('Payment was cancelled. You can retry online or pay cash at the counter.');
            }
          },
          theme: {
            color: '#0d9488'
          }
        };

        try {
          const rzp = new window.Razorpay(options);
          rzp.on('payment.failed', (response: any) => {
            this.isPaymentProcessing.set(false);
            this.paymentError.set(response?.error?.description || 'Payment failed. Please try again.');
          });
          rzp.open();
        } catch (err: any) {
          this.isPaymentProcessing.set(false);
          this.paymentError.set('Could not initialize payment window. Please try again.');
        }
      },
      error: (err) => {
        this.isPaymentProcessing.set(false);
        console.error('Create payment order error:', err);
        this.paymentError.set(err?.error?.message || 'Could not initiate online payment. You can pay cash at the counter.');
      }
    });
  }

  private verifyOnlinePayment(orderId: number, razorpayResponse: any): void {
    const payload = {
      orderId,
      razorpayPaymentId: razorpayResponse.razorpay_payment_id,
      razorpayOrderId: razorpayResponse.razorpay_order_id,
      razorpaySignature: razorpayResponse.razorpay_signature
    };

    this.paymentService.verifyPayment(payload).subscribe({
      next: (payment) => {
        this.isPaymentProcessing.set(false);
        this.paymentError.set(null);

        const current = this.activeOrder();
        if (current) {
          this.activeOrder.set({
            ...current,
            paymentStatus: 'Paid',
            status: current.status === 'Pending' ? 'Confirmed' : current.status
          });
          this.paymentSuccessNotification.set({
            orderNumber: current.orderNumber,
            amount: payment.amount,
            paidAt: payment.paidAt || new Date().toISOString()
          });
        }
      },
      error: (err) => {
        this.isPaymentProcessing.set(false);
        console.error('Payment verification error:', err);
        this.paymentError.set(err?.error?.message || 'Payment verification failed. Please ask staff for assistance.');
      }
    });
  }

  switchToCounterPayment(): void {
    const current = this.activeOrder();
    if (!current) return;

    this.selectedPaymentMethod.set('counter');
    this.paymentError.set(null);

    this.paymentService.payAtCounter(current.orderId).subscribe({
      next: () => {
        try {
          const raw = localStorage.getItem(ACTIVE_ORDER_STORAGE_KEY);
          if (raw) {
            const parsed = JSON.parse(raw);
            parsed.paymentMethod = 'counter';
            localStorage.setItem(ACTIVE_ORDER_STORAGE_KEY, JSON.stringify(parsed));
          }
        } catch {}
      },
      error: (err) => {
        console.warn('Switch to counter error:', err);
      }
    });
  }

  cancelOrder(): void {
    const current = this.activeOrder();
    if (!current) return;

    if (!confirm('Are you sure you want to cancel this order?')) {
      return;
    }

    this.isCancellingOrder.set(true);
    this.paymentError.set(null);

    this.orderService.cancelOrder(current.trackingToken).subscribe({
      next: () => {
        this.isCancellingOrder.set(false);
        this.activeOrder.set(null);
        this.paymentError.set(null);
        this.paymentSuccessNotification.set(null);
        try {
          localStorage.removeItem(ACTIVE_ORDER_STORAGE_KEY);
        } catch {}

        if (this.slug()) {
          this.loadMenuDataBySlug(this.slug());
        } else {
          this.loadMenuData(this.canteenId());
        }
      },
      error: (err) => {
        this.isCancellingOrder.set(false);
        const msg = err?.error?.message || 'Could not cancel order. Please ask canteen staff.';
        this.paymentError.set(msg);
      }
    });
  }

  backToMenu(): void {
    this.activeOrder.set(null);
    this.paymentError.set(null);
    this.paymentSuccessNotification.set(null);
    if (this.slug()) {
      this.loadMenuDataBySlug(this.slug());
    } else {
      this.loadMenuData(this.canteenId());
    }
  }

  placeNewOrder(): void {
    this.activeOrder.set(null);
    this.orderError.set(null);
    this.paymentError.set(null);
    this.paymentSuccessNotification.set(null);
    try {
      localStorage.removeItem(ACTIVE_ORDER_STORAGE_KEY);
    } catch {}

    if (this.slug()) {
      this.loadMenuDataBySlug(this.slug());
    } else {
      this.loadMenuData(this.canteenId());
    }
  }
}
