import {
  Component,
  OnInit,
  OnDestroy,
  signal,
  computed,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { OrderService } from '../../core/services/order.service';
import { SignalRService } from '../../core/services/signalr.service';
import { AudioAlertService } from '../../core/services/audio-alert.service';
import { TvDeviceService } from '../../core/services/tv-device.service';
import { OrderDto, KitchenTicket } from '../../core/models/order.models';
import { TvDeviceDto, TvPairingSessionResponseDto } from '../../core/models/tv-device.models';

type KitchenFilterTab = 'All Active' | 'Pending' | 'Preparing' | 'Ready';

@Component({
  selector: 'app-kitchen-display',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './kitchen-display.html',
  styleUrl: './kitchen-display.css'
})
export class KitchenDisplayComponent implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly orderService = inject(OrderService);
  private readonly signalRService = inject(SignalRService);
  readonly audioAlert = inject(AudioAlertService);
  readonly tvDeviceService = inject(TvDeviceService);

  private readonly subscriptions = new Subscription();
  private timerInterval: ReturnType<typeof setInterval> | null = null;
  private countdownInterval: ReturnType<typeof setInterval> | null = null;

  // TV Pairing & Device Modal
  showTvModal = signal(false);
  tvModalTab = signal<'connected' | 'connect'>('connected');
  tvDevices = signal<TvDeviceDto[]>([]);
  loadingDevices = signal(false);
  activePairingSession = signal<TvPairingSessionResponseDto | null>(null);
  isGeneratingCode = signal(false);
  pairingCountdownSeconds = signal(300);
  newTvName = signal('Dining Hall TV');
  newTvStation = signal<'All' | 'Food' | 'Beverage'>('All');
  tvActionError = signal<string | null>(null);
  tvActionSuccess = signal<string | null>(null);

  tickets = signal<KitchenTicket[]>([]);
  loading = signal(false);
  errorMessage = signal('');
  selectedTab = signal<KitchenFilterTab>('All Active');
  isLiveConnected = signal(false);
  activeOrderIdAction = signal<number | null>(null);
  announcingOrderId = signal<number | null>(null);
  newOrderBanner = signal<string | null>(null);
  isFullscreen = signal(false);

  // Plating checklist stored locally per order
  itemCheckedState = signal<{ [key: string]: boolean }>({});

  readonly tabs: KitchenFilterTab[] = ['All Active', 'Pending', 'Preparing', 'Ready'];

  // Metrics summary
  readonly pendingCount = computed(() => {
    return this.tickets().filter(t => this.isPending(t.status)).length;
  });

  readonly preparingCount = computed(() => {
    return this.tickets().filter(t => this.isPreparing(t.status)).length;
  });

  readonly readyCount = computed(() => {
    return this.tickets().filter(t => this.isReady(t.status)).length;
  });

  readonly avgPrepTime = computed(() => {
    const active = this.tickets();
    if (active.length === 0) return '0 min';
    // Cap outlier test tickets from days ago so metrics display realistic prep times
    const sum = active.reduce((acc, t) => acc + Math.min(t.elapsedMinutes, 120), 0);
    const avg = Math.round((sum / active.length) * 10) / 10;
    return `${avg} min`;
  });

  readonly filteredTickets = computed(() => {
    const tab = this.selectedTab();
    const all = this.tickets();

    switch (tab) {
      case 'Pending':
        return all.filter(t => this.isPending(t.status));
      case 'Preparing':
        return all.filter(t => this.isPreparing(t.status));
      case 'Ready':
        return all.filter(t => this.isReady(t.status));
      case 'All Active':
      default:
        return all.filter(t => !this.isCompletedOrCancelled(t.status));
    }
  });

  ngOnInit(): void {
    this.loadTickets();
    this.setupSignalR();

    // Start timer ticker to update elapsed minutes and urgency badges every 10 seconds
    this.timerInterval = setInterval(() => {
      this.recalculateTicketTimers();
    }, 10000);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }
    if (this.countdownInterval) {
      clearInterval(this.countdownInterval);
    }
  }

  getCurrentCanteenId(): number {
    return this.authService.currentUser()?.canteenId ?? 1;
  }

  loadTickets(): void {
    const canteenId = this.getCurrentCanteenId();
    this.loading.set(true);
    this.errorMessage.set('');

    this.orderService.getOrdersByCanteen(canteenId).subscribe({
      next: (orders) => {
        const active = orders
          .filter(o => !this.isCompletedOrCancelled(o.status))
          .map(o => this.mapToTicket(o));

        // Sort by creation time (FIFO: oldest orders first)
        active.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
        this.tickets.set(active);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || 'Failed to load kitchen tickets.');
        this.loading.set(false);
      }
    });
  }

  private setupSignalR(): void {
    this.signalRService.startConnection().then(() => {
      this.isLiveConnected.set(true);
    }).catch(() => {
      this.isLiveConnected.set(false);
    });

    // 1. Listen for new incoming orders
    this.subscriptions.add(
      this.signalRService.newOrder$.subscribe((data) => {
        const currentCanteenId = this.getCurrentCanteenId();
        if (data.canteenId === currentCanteenId) {
          this.audioAlert.playNewOrderChime();
          this.newOrderBanner.set(`🔔 New Order #${data.orderNumber} received!`);
          setTimeout(() => this.newOrderBanner.set(null), 6000);
          this.loadTickets();
        }
      })
    );

    // 2. Listen for order status updates
    this.subscriptions.add(
      this.signalRService.orderStatusUpdated$.subscribe((data) => {
        if (this.isCompletedOrCancelled(data.newStatus)) {
          // Remove from active kitchen queue
          this.tickets.update(list => list.filter(t => t.orderId !== data.orderId));
        } else {
          this.tickets.update(list => list.map(t => {
            if (t.orderId === data.orderId) {
              return {
                ...t,
                status: data.newStatus
              };
            }
            return t;
          }));
        }
      })
    );

    // 3. Listen for payment confirmations
    this.subscriptions.add(
      this.signalRService.paymentConfirmed$.subscribe((data) => {
        const currentCanteenId = this.getCurrentCanteenId();
        if (data.canteenId === currentCanteenId) {
          this.tickets.update(list => list.map(t => {
            if (t.orderId === data.orderId) {
              return { ...t, paymentStatus: 'Paid' };
            }
            return t;
          }));
        }
      })
    );
  }

  // Action: Start cooking (Pending -> InPreparation)
  startCooking(ticket: KitchenTicket): void {
    const canteenId = this.getCurrentCanteenId();
    this.activeOrderIdAction.set(ticket.orderId);

    this.orderService.updateOrderStatus(canteenId, ticket.orderId, 'InPreparation').subscribe({
      next: (updated) => {
        this.activeOrderIdAction.set(null);
        this.tickets.update(list => list.map(t => t.orderId === ticket.orderId ? this.mapToTicket(updated) : t));
      },
      error: (err) => {
        this.activeOrderIdAction.set(null);
        alert(err?.error?.message || 'Could not update order to InPreparation.');
      }
    });
  }

  // Action: Mark Ready (InPreparation -> Ready)
  markReady(ticket: KitchenTicket): void {
    const canteenId = this.getCurrentCanteenId();
    this.activeOrderIdAction.set(ticket.orderId);

    this.orderService.updateOrderStatus(canteenId, ticket.orderId, 'Ready').subscribe({
      next: (updated) => {
        this.activeOrderIdAction.set(null);
        this.tickets.update(list => list.map(t => t.orderId === ticket.orderId ? this.mapToTicket(updated) : t));
        // Announce ready token
        this.audioAlert.speakToken(ticket.tokenNumber);
      },
      error: (err) => {
        this.activeOrderIdAction.set(null);
        alert(err?.error?.message || 'Could not mark order as Ready.');
      }
    });
  }

  // Action: Bump / Complete order (Ready -> Completed)
  bumpComplete(ticket: KitchenTicket): void {
    const canteenId = this.getCurrentCanteenId();
    this.activeOrderIdAction.set(ticket.orderId);

    this.orderService.updateOrderStatus(canteenId, ticket.orderId, 'Completed').subscribe({
      next: () => {
        this.activeOrderIdAction.set(null);
        this.tickets.update(list => list.filter(t => t.orderId !== ticket.orderId));
      },
      error: (err) => {
        this.activeOrderIdAction.set(null);
        alert(err?.error?.message || 'Could not complete order.');
      }
    });
  }

  // Action: Recall voice announcement on TV
  announceOnTv(ticket: KitchenTicket): void {
    const canteenId = this.getCurrentCanteenId();
    this.announcingOrderId.set(ticket.orderId);

    this.orderService.announceOrderOnTv(canteenId, ticket.orderId).subscribe({
      next: () => {
        this.announcingOrderId.set(null);
        this.audioAlert.speakToken(ticket.tokenNumber);
      },
      error: () => {
        this.announcingOrderId.set(null);
        // Fallback local speech
        this.audioAlert.speakToken(ticket.tokenNumber);
      }
    });
  }

  // Plating checklist toggle
  toggleItemPlated(orderId: number, productId: number): void {
    const key = `${orderId}_${productId}`;
    const current = this.itemCheckedState();
    this.itemCheckedState.set({
      ...current,
      [key]: !current[key]
    });
  }

  isItemPlated(orderId: number, productId: number): boolean {
    const key = `${orderId}_${productId}`;
    return !!this.itemCheckedState()[key];
  }

  openTvDisplay(): void {
    const canteenId = this.getCurrentCanteenId();
    const url = `/kitchen-display/tv?canteenId=${canteenId}`;
    window.open(url, '_blank', 'noopener,noreferrer');
  }

  openTvModal(): void {
    this.showTvModal.set(true);
    this.tvActionError.set(null);
    this.tvActionSuccess.set(null);
    this.loadTvDevices();
  }

  closeTvModal(): void {
    this.showTvModal.set(false);
    if (this.countdownInterval) {
      clearInterval(this.countdownInterval);
      this.countdownInterval = null;
    }
  }

  loadTvDevices(): void {
    const canteenId = this.getCurrentCanteenId();
    this.loadingDevices.set(true);
    this.tvDeviceService.getDevicesByCanteen(canteenId).subscribe({
      next: (devices) => {
        this.tvDevices.set(devices);
        this.loadingDevices.set(false);
      },
      error: () => {
        this.loadingDevices.set(false);
      }
    });
  }

  generatePairingCode(): void {
    const canteenId = this.getCurrentCanteenId();
    this.isGeneratingCode.set(true);
    this.tvActionError.set(null);
    this.tvActionSuccess.set(null);

    this.tvDeviceService.createPairingSession(canteenId, {
      deviceName: this.newTvName().trim() || 'Smart TV',
      stationScope: this.newTvStation()
    }).subscribe({
      next: (session) => {
        this.activePairingSession.set(session);
        this.isGeneratingCode.set(false);
        this.startPairingCountdown(session.expiresInSeconds || 300);
      },
      error: (err) => {
        this.isGeneratingCode.set(false);
        this.tvActionError.set(err?.error?.message || 'Failed to generate TV pairing PIN.');
      }
    });
  }

  private startPairingCountdown(seconds: number): void {
    if (this.countdownInterval) {
      clearInterval(this.countdownInterval);
    }
    this.pairingCountdownSeconds.set(seconds);

    this.countdownInterval = setInterval(() => {
      const remaining = this.pairingCountdownSeconds() - 1;
      if (remaining <= 0) {
        this.pairingCountdownSeconds.set(0);
        if (this.countdownInterval) {
          clearInterval(this.countdownInterval);
          this.countdownInterval = null;
        }
      } else {
        this.pairingCountdownSeconds.set(remaining);
      }
    }, 1000);
  }

  formattedPairingCountdown(): string {
    const total = this.pairingCountdownSeconds();
    const mins = Math.floor(total / 60);
    const secs = total % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }

  revokeTvDevice(device: TvDeviceDto): void {
    if (!confirm(`Are you sure you want to disconnect "${device.deviceName}"? The TV screen will immediately reset to pairing mode.`)) {
      return;
    }

    const canteenId = this.getCurrentCanteenId();
    this.tvDeviceService.revokeDevice(canteenId, device.tvDeviceId).subscribe({
      next: () => {
        this.tvActionSuccess.set(`"${device.deviceName}" successfully disconnected.`);
        this.loadTvDevices();
        setTimeout(() => this.tvActionSuccess.set(null), 4000);
      },
      error: (err) => {
        this.tvActionError.set(err?.error?.message || 'Failed to revoke TV device.');
      }
    });
  }

  toggleFullscreen(): void {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen().catch(() => {});
      this.isFullscreen.set(true);
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen().catch(() => {});
        this.isFullscreen.set(false);
      }
    }
  }

  testAudio(): void {
    this.audioAlert.playNewOrderChime();
    setTimeout(() => {
      this.audioAlert.speakToken('99', 'Counter 1');
    }, 1200);
  }

  private mapToTicket(order: OrderDto): KitchenTicket {
    const parts = (order.orderNumber || '').split('-');
    const token = parts.length > 0 ? parts[parts.length - 1] : order.orderNumber;
    const elapsed = this.calculateElapsedMinutes(order.createdAt);
    const urgency = elapsed > 10 ? 'critical' : elapsed >= 5 ? 'warning' : 'normal';

    return {
      ...order,
      tokenNumber: token || `#${order.orderId}`,
      elapsedMinutes: elapsed,
      urgency,
      checkedItems: {}
    };
  }

  private recalculateTicketTimers(): void {
    this.tickets.update(list => list.map(t => {
      const elapsed = this.calculateElapsedMinutes(t.createdAt);
      const urgency: 'normal' | 'warning' | 'critical' = elapsed > 10 ? 'critical' : elapsed >= 5 ? 'warning' : 'normal';
      return {
        ...t,
        elapsedMinutes: elapsed,
        urgency
      };
    }));
  }

  private calculateElapsedMinutes(createdAt: string): number {
    if (!createdAt) return 0;
    const created = new Date(createdAt).getTime();
    const now = Date.now();
    const diffMs = Math.max(0, now - created);
    return Math.floor(diffMs / 60000);
  }

  formatElapsed(minutes: number): string {
    if (minutes < 60) return `${minutes}m elapsed`;
    const hours = Math.floor(minutes / 60);
    const rem = minutes % 60;
    return `${hours}h ${rem}m`;
  }

  isPending(status: string): boolean {
    const s = (status || '').toLowerCase();
    return s === 'pending' || s === 'received' || s === 'confirmed';
  }

  isPreparing(status: string): boolean {
    const s = (status || '').toLowerCase();
    return s === 'inpreparation' || s === 'preparing';
  }

  isReady(status: string): boolean {
    return (status || '').toLowerCase() === 'ready';
  }

  isCompletedOrCancelled(status: string): boolean {
    const s = (status || '').toLowerCase();
    return s === 'completed' || s === 'cancelled';
  }
}

