import {
  Component,
  Input,
  OnInit,
  OnDestroy,
  OnChanges,
  SimpleChanges,
  signal,
  computed,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { OrderService } from '../../core/services/order.service';
import { PaymentService } from '../../core/services/payment.service';
import { SignalRService } from '../../core/services/signalr.service';
import { OrderDto } from '../../core/models/order.models';

type OrderStatus =
  | 'Pending'
  | 'Received'
  | 'InPreparation'
  | 'Preparing'
  | 'Ready'
  | 'Completed'
  | 'Cancelled';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './orders.html',
  styleUrl: './orders.css'
})
export class Orders implements OnInit, OnDestroy, OnChanges {
  @Input() canteenId?: number;

  private readonly authService = inject(AuthService);
  private readonly orderService = inject(OrderService);
  private readonly paymentService = inject(PaymentService);
  private readonly signalRService = inject(SignalRService);

  private readonly subscriptions = new Subscription();

  orders = signal<OrderDto[]>([]);
  loading = signal(false);
  updatingOrderId = signal<number | null>(null);
  collectingPaymentOrderId = signal<number | null>(null);
  searchText = signal('');
  selectedTab = signal('All Orders');
  errorMessage = signal('');
  isLiveConnected = signal(false);
  lastNewOrderAlert = signal<string | null>(null);

  // Cancellation Modal Signals
  cancellingOrder = signal<OrderDto | null>(null);
  canteenCancelReason = signal<string>('Item out of stock');
  canteenCustomReason = signal<string>('');
  readonly predefinedCanteenCancelReasons = [
    'Item out of stock',
    'Kitchen too busy / capacity exceeded',
    'Kitchen equipment malfunction',
    'Customer requested counter cancellation',
    'Other'
  ];

  tabs = [
    'All Orders',
    'Received',
    'Preparing',
    'Ready',
    'Completed',
    'Cancelled'
  ];

  filteredOrders = computed(() => {
    const search = this.searchText().toLowerCase().trim();
    const tab = this.selectedTab();

    return this.orders().filter((order) => {
      const statusLower = (order.status || '').toLowerCase();
      let matchesTab = false;

      if (tab === 'All Orders') {
        matchesTab = true;
      } else if (tab === 'Received') {
        matchesTab = statusLower === 'pending' || statusLower === 'received' || statusLower === 'confirmed';
      } else if (tab === 'Preparing') {
        matchesTab = statusLower === 'inpreparation' || statusLower === 'preparing';
      } else if (tab === 'Ready') {
        matchesTab = statusLower === 'ready';
      } else if (tab === 'Completed') {
        matchesTab = statusLower === 'completed';
      } else if (tab === 'Cancelled') {
        matchesTab = statusLower === 'cancelled';
      }

      const matchesSearch =
        !search ||
        (order.orderNumber && order.orderNumber.toLowerCase().includes(search)) ||
        String(order.orderId).includes(search) ||
        String(order.tableId).includes(search) ||
        (order.items && order.items.some((i) => i.productName && i.productName.toLowerCase().includes(search)));

      return matchesTab && matchesSearch;
    });
  });

  ngOnInit(): void {
    if (!this.canteenId) {
      const user = this.authService.currentUser();
      if (user?.canteenId) {
        this.canteenId = user.canteenId;
      }
    }
    this.connectSignalR();
    if (this.canteenId) {
      this.loadOrders();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['canteenId'] && this.canteenId) {
      this.loadOrders();
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private connectSignalR(): void {
    this.signalRService
      .startConnection()
      .then(() => {
        this.isLiveConnected.set(true);
      })
      .catch(() => {
        this.isLiveConnected.set(false);
      });

    // Listen for incoming new orders placed by customers
    this.subscriptions.add(
      this.signalRService.newOrder$.subscribe((notif) => {
        if (notif.canteenId === this.effectiveCanteenId) {
          this.lastNewOrderAlert.set(notif.orderNumber);
          setTimeout(() => {
            if (this.lastNewOrderAlert() === notif.orderNumber) {
              this.lastNewOrderAlert.set(null);
            }
          }, 6000);

          this.loadOrders();
        }
      })
    );

    // Listen for real-time order status updates
    this.subscriptions.add(
      this.signalRService.orderStatusUpdated$.subscribe((notif) => {
        this.orders.update((list) =>
          list.map((o) =>
            o.orderId === notif.orderId ? { ...o, status: notif.newStatus } : o
          )
        );
      })
    );

    // Listen for real-time payment confirmations
    this.subscriptions.add(
      this.signalRService.paymentConfirmed$.subscribe((notif) => {
        if (notif.canteenId === this.effectiveCanteenId) {
          this.orders.update((list) =>
            list.map((o) =>
              o.orderId === notif.orderId ? { ...o, paymentStatus: 'Paid' } : o
            )
          );
        }
      })
    );
  }

  get effectiveCanteenId(): number | null {
    return this.canteenId || this.authService.currentUser()?.canteenId || null;
  }

  loadOrders(): void {
    const cId = this.effectiveCanteenId;
    if (!cId) return;

    this.loading.set(true);
    this.errorMessage.set('');

    this.orderService.getOrdersByCanteen(cId).subscribe({
      next: (response) => {
        this.orders.set(response);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Unable to load orders:', error);
        this.errorMessage.set('Unable to load orders. Please try again.');
        this.loading.set(false);
      }
    });
  }

  selectTab(tab: string): void {
    this.selectedTab.set(tab);
  }

  getNextStatus(status: string): OrderStatus | null {
    const s = (status || '').toLowerCase();
    if (s === 'pending' || s === 'received' || s === 'confirmed') {
      return 'InPreparation';
    }
    if (s === 'inpreparation' || s === 'preparing') {
      return 'Ready';
    }
    if (s === 'ready') {
      return 'Completed';
    }
    return null;
  }

  getActionLabel(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'pending' || s === 'received' || s === 'confirmed') {
      return 'Start Preparing';
    }
    if (s === 'inpreparation' || s === 'preparing') {
      return 'Mark Ready';
    }
    if (s === 'ready') {
      return 'Complete Order';
    }
    return 'View';
  }

  getDisplayStatus(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'inpreparation') return 'Preparing';
    if (s === 'pending') return 'Received';
    return status;
  }

  getStatusClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'inpreparation') return 'preparing';
    return s;
  }

  updateStatus(order: OrderDto): void {
    const nextStatus = this.getNextStatus(order.status);
    if (!nextStatus || this.updatingOrderId() !== null) {
      return;
    }

    this.updatingOrderId.set(order.orderId);

    const cId = this.effectiveCanteenId;
    if (!cId) return;
    this.orderService
      .updateOrderStatus(cId, order.orderId, nextStatus)
      .subscribe({
        next: (updatedOrder) => {
          this.orders.update((list) =>
            list.map((o) =>
              o.orderId === order.orderId ? { ...o, status: updatedOrder.status } : o
            )
          );
          this.updatingOrderId.set(null);
        },
        error: (error) => {
          console.error('Unable to update order status:', error);
          this.errorMessage.set('Could not update order status.');
          this.updatingOrderId.set(null);
        }
      });
  }

  openCanteenCancelModal(order: OrderDto): void {
    this.cancellingOrder.set(order);
    this.canteenCancelReason.set(this.predefinedCanteenCancelReasons[0]);
    this.canteenCustomReason.set('');
  }

  closeCanteenCancelModal(): void {
    if (this.updatingOrderId() !== null) return;
    this.cancellingOrder.set(null);
  }

  cancelOrder(order: OrderDto): void {
    this.openCanteenCancelModal(order);
  }

  confirmCanteenCancel(): void {
    const order = this.cancellingOrder();
    if (!order || this.updatingOrderId() !== null) return;

    const cId = this.effectiveCanteenId;
    if (!cId) return;

    const selected = this.canteenCancelReason();
    const reason = selected === 'Other'
      ? (this.canteenCustomReason().trim() || 'Kitchen cancellation')
      : selected;

    this.updatingOrderId.set(order.orderId);

    this.orderService.cancelOrderFromCanteen(cId, order.orderId, reason).subscribe({
      next: (cancelledOrder) => {
        this.orders.update((list) =>
          list.map((o) =>
            o.orderId === order.orderId
              ? {
                  ...o,
                  status: cancelledOrder.status,
                  paymentStatus: cancelledOrder.paymentStatus,
                  refundedAmount: cancelledOrder.refundedAmount,
                  cancellationNote: cancelledOrder.cancellationNote
                }
              : o
          )
        );
        this.updatingOrderId.set(null);
        this.cancellingOrder.set(null);
      },
      error: (error) => {
        console.error('Unable to cancel order from canteen:', error);
        this.errorMessage.set(error?.error?.message || 'Could not cancel order.');
        this.updatingOrderId.set(null);
      }
    });
  }

  collectCashPayment(order: OrderDto): void {
    if (this.collectingPaymentOrderId() !== null) return;

    this.collectingPaymentOrderId.set(order.orderId);

    const cId = this.effectiveCanteenId;
    if (!cId) return;
    this.paymentService.confirmCounterPayment(cId, order.orderId).subscribe({
      next: () => {
        this.orders.update((list) =>
          list.map((o) =>
            o.orderId === order.orderId ? { ...o, paymentStatus: 'Paid' } : o
          )
        );
        this.collectingPaymentOrderId.set(null);
      },
      error: (error) => {
        console.error('Failed to confirm cash payment:', error);
        alert(error?.error?.message || 'Failed to record cash payment.');
        this.collectingPaymentOrderId.set(null);
      }
    });
  }

  getTabCount(tab: string): number {
    if (tab === 'All Orders') {
      return this.orders().length;
    }

    return this.orders().filter((order) => {
      const s = (order.status || '').toLowerCase();
      if (tab === 'Received') {
        return s === 'pending' || s === 'received' || s === 'confirmed';
      }
      if (tab === 'Preparing') {
        return s === 'inpreparation' || s === 'preparing';
      }
      if (tab === 'Ready') {
        return s === 'ready';
      }
      if (tab === 'Completed') {
        return s === 'completed';
      }
      if (tab === 'Cancelled') {
        return s === 'cancelled';
      }
      return false;
    }).length;
  }
}
