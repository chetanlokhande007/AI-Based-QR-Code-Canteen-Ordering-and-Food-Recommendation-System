import {
  Component,
  OnInit,
  OnDestroy,
  signal,
  inject,
  HostListener
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { OrderService } from '../../core/services/order.service';
import { SignalRService } from '../../core/services/signalr.service';
import { AudioAlertService } from '../../core/services/audio-alert.service';
import { AuthService } from '../../core/services/auth.service';
import { TvDeviceService } from '../../core/services/tv-device.service';
import { TvTokenDto, CanteenTvDisplayDto } from '../../core/models/order.models';

@Component({
  selector: 'app-tv-display',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tv-display.html',
  styleUrl: './tv-display.css'
})
export class TvDisplayComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly orderService = inject(OrderService);
  private readonly signalRService = inject(SignalRService);
  private readonly authService = inject(AuthService);
  readonly tvDeviceService = inject(TvDeviceService);
  readonly audioAlert = inject(AudioAlertService);

  private readonly subscriptions = new Subscription();
  private clockInterval: ReturnType<typeof setInterval> | null = null;
  private wakeLockSentinel: unknown = null;

  // Pairing & Device State
  isPaired = signal<boolean>(false);
  pairingCode = signal<string>('');
  pairingError = signal<string | null>(null);
  isClaiming = signal<boolean>(false);
  tvDeviceId = signal<number | null>(null);
  deviceName = signal<string>('Dining Hall TV');
  stationScope = signal<string>('All');

  // Display & Token State
  canteenId = signal<number>(1);
  canteenName = signal<string>('Smart Canteen');
  preparingOrders = signal<TvTokenDto[]>([]);
  readyOrders = signal<TvTokenDto[]>([]);
  newlyReadyToken = signal<string | null>(null);
  activeAnnouncement = signal<string | null>(null);
  currentTime = signal<string>('');
  currentDate = signal<string>('');
  isLiveConnected = signal<boolean>(false);
  isFullscreen = signal<boolean>(false);
  loading = signal<boolean>(true);

  // TV remote numpad keys
  readonly numpadKeys: (string | number)[] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 'Clear', 0, '⌫'];

  ngOnInit(): void {
    // 1. Start digital clock
    this.updateClock();
    this.clockInterval = setInterval(() => this.updateClock(), 1000);

    // 2. Request Screen Wake Lock so TV does not sleep
    this.requestWakeLock();

    // 3. Determine auth/device token state
    this.initDeviceState();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.clockInterval) {
      clearInterval(this.clockInterval);
    }
    this.releaseWakeLock();
  }

  private initDeviceState(): void {
    const token = this.tvDeviceService.getStoredDeviceToken();
    if (token) {
      this.verifyAndLoadByToken(token);
      return;
    }

    // Check query params / logged-in user for quick browser preview
    const paramId = this.route.snapshot.paramMap.get('canteenId');
    const queryId = this.route.snapshot.queryParamMap.get('canteenId');
    const userCanteenId = this.authService.currentUser()?.canteenId;

    const directId = paramId ? parseInt(paramId, 10) :
                     queryId ? parseInt(queryId, 10) :
                     userCanteenId;

    if (directId) {
      this.canteenId.set(directId);
      this.isPaired.set(true);
      this.loadDisplayDataDirect(directId);
      this.setupTvSignalR(directId);
    } else {
      // Unpaired standalone Smart TV mode
      this.isPaired.set(false);
      this.loading.set(false);
    }
  }

  verifyAndLoadByToken(deviceToken: string): void {
    this.loading.set(true);
    this.tvDeviceService.getTvDisplayByToken(deviceToken).subscribe({
      next: (data: CanteenTvDisplayDto) => {
        this.canteenId.set(data.canteenId);
        this.canteenName.set(data.canteenName || 'Smart Canteen');
        this.stationScope.set(data.stationScope || 'All');
        this.deviceName.set(data.deviceName || 'Smart TV');
        this.preparingOrders.set(data.preparingOrders || []);
        this.readyOrders.set(data.readyOrders || []);
        this.isPaired.set(true);
        this.loading.set(false);
        this.setupTvSignalR(data.canteenId);
      },
      error: () => {
        // Token invalid, expired, or revoked
        this.tvDeviceService.clearStoredDeviceToken();
        this.isPaired.set(false);
        this.loading.set(false);
        this.pairingError.set('Previous device session expired or was revoked. Please pair again.');
      }
    });
  }

  private loadDisplayDataDirect(id: number): void {
    this.loading.set(true);
    this.orderService.getTvDisplayData(id).subscribe({
      next: (data: CanteenTvDisplayDto) => {
        this.canteenName.set(data.canteenName || 'Smart Canteen');
        this.preparingOrders.set(data.preparingOrders || []);
        this.readyOrders.set(data.readyOrders || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  refreshDisplay(): void {
    const token = this.tvDeviceService.getStoredDeviceToken();
    if (token) {
      this.tvDeviceService.getTvDisplayByToken(token).subscribe({
        next: (data) => {
          this.preparingOrders.set(data.preparingOrders || []);
          this.readyOrders.set(data.readyOrders || []);
        }
      });
    } else {
      this.loadDisplayDataDirect(this.canteenId());
    }
  }

  // --- Remote & Numpad Code Input ---
  @HostListener('window:keydown', ['$event'])
  handleKeyboardInput(event: KeyboardEvent): void {
    if (this.isPaired()) return;

    if (event.key >= '0' && event.key <= '9') {
      this.appendDigit(event.key);
      event.preventDefault();
    } else if (event.key === 'Backspace') {
      this.backspaceDigit();
      event.preventDefault();
    } else if (event.key === 'Enter') {
      if (this.pairingCode().length === 6) {
        this.submitPairing();
      }
      event.preventDefault();
    } else if (event.key === 'Escape') {
      this.clearCode();
      event.preventDefault();
    }
  }

  onNumpadClick(key: string | number): void {
    if (typeof key === 'number' || (typeof key === 'string' && key.length === 1 && key >= '0' && key <= '9')) {
      this.appendDigit(key.toString());
    } else if (key === '⌫' || key === 'Backspace') {
      this.backspaceDigit();
    } else if (key === 'Clear') {
      this.clearCode();
    }
  }

  appendDigit(digit: string): void {
    const current = this.pairingCode();
    if (current.length < 6) {
      const updated = current + digit;
      this.pairingCode.set(updated);
      this.pairingError.set(null);

      if (updated.length === 6) {
        // Auto-submit when all 6 digits are typed!
        this.submitPairing();
      }
    }
  }

  backspaceDigit(): void {
    const current = this.pairingCode();
    if (current.length > 0) {
      this.pairingCode.set(current.slice(0, -1));
      this.pairingError.set(null);
    }
  }

  clearCode(): void {
    this.pairingCode.set('');
    this.pairingError.set(null);
  }

  submitPairing(): void {
    const code = this.pairingCode();
    if (code.length !== 6) {
      this.pairingError.set('Please enter the full 6-digit PIN.');
      return;
    }

    this.isClaiming.set(true);
    this.pairingError.set(null);

    const deviceId = this.tvDeviceService.getOrCreateDeviceIdentifier();

    this.tvDeviceService.claimPairingCode({
      pairingCode: code,
      deviceIdentifier: deviceId,
      deviceName: 'Smart TV Screen'
    }).subscribe({
      next: (result) => {
        this.isClaiming.set(false);
        if (result.success && result.deviceToken) {
          this.tvDeviceService.setStoredDeviceToken(result.deviceToken);
          this.tvDeviceId.set(result.tvDeviceId);
          this.canteenId.set(result.canteenId);
          this.canteenName.set(result.canteenName || 'Smart Canteen');
          this.stationScope.set(result.stationScope || 'All');
          this.deviceName.set(result.deviceName || 'Smart TV');
          this.isPaired.set(true);

          // Announce connection
          this.audioAlert.announceCustom('Smart TV connected to ' + (result.canteenName || 'canteen'));

          // Load display data and start SignalR
          this.verifyAndLoadByToken(result.deviceToken);
        } else {
          this.pairingError.set(result.message || 'Failed to claim pairing code.');
        }
      },
      error: (err) => {
        this.isClaiming.set(false);
        this.pairingError.set(err?.error?.message || 'Invalid or expired code. Please generate a new code.');
      }
    });
  }

  unpairDevice(customReason?: string): void {
    this.tvDeviceService.clearStoredDeviceToken();
    this.isPaired.set(false);
    this.pairingCode.set('');
    this.tvDeviceId.set(null);
    this.preparingOrders.set([]);
    this.readyOrders.set([]);
    this.isLiveConnected.set(false);
    if (customReason) {
      this.pairingError.set(customReason);
    }
  }

  // --- SignalR Setup ---
  private setupTvSignalR(canteenId: number): void {
    this.signalRService.startTvConnection(canteenId).then(() => {
      this.isLiveConnected.set(true);
    }).catch(() => {
      this.isLiveConnected.set(false);
    });

    // 1. Listen for status updates
    this.subscriptions.add(
      this.signalRService.orderStatusUpdated$.subscribe((data) => {
        const token = this.extractToken(data.orderNumber);
        const status = (data.newStatus || '').toLowerCase();

        if (status === 'inpreparation' || status === 'preparing') {
          this.readyOrders.update(list => list.filter(o => o.orderId !== data.orderId));
          this.preparingOrders.update(list => {
            if (!list.some(o => o.orderId === data.orderId)) {
              return [...list, {
                orderId: data.orderId,
                orderNumber: data.orderNumber,
                tokenNumber: token,
                status: data.newStatus,
                createdAt: data.updatedAt,
                updatedAt: data.updatedAt,
                itemCount: 1
              }];
            }
            return list;
          });
        } else if (status === 'ready') {
          this.preparingOrders.update(list => list.filter(o => o.orderId !== data.orderId));
          this.readyOrders.update(list => {
            const filtered = list.filter(o => o.orderId !== data.orderId);
            return [{
              orderId: data.orderId,
              orderNumber: data.orderNumber,
              tokenNumber: token,
              status: data.newStatus,
              createdAt: data.updatedAt,
              updatedAt: data.updatedAt,
              itemCount: 1
            }, ...filtered];
          });

          // Flash newly ready token & announce via Voice TTS
          this.flashReadyToken(token);
          this.audioAlert.speakToken(token);
        } else if (status === 'completed' || status === 'cancelled') {
          this.preparingOrders.update(list => list.filter(o => o.orderId !== data.orderId));
          this.readyOrders.update(list => list.filter(o => o.orderId !== data.orderId));
        }
      })
    );

    // 2. Listen for explicit TV voice announcements
    this.subscriptions.add(
      this.signalRService.tvVoiceAnnouncement$.subscribe((announcement) => {
        if (announcement.canteenId === this.canteenId()) {
          this.flashReadyToken(announcement.tokenNumber);
          this.activeAnnouncement.set(announcement.message || `Token #${announcement.tokenNumber} is ready for pickup!`);
          this.audioAlert.speakToken(announcement.tokenNumber);
          setTimeout(() => this.activeAnnouncement.set(null), 8000);
        }
      })
    );

    // 3. Listen for new orders
    this.subscriptions.add(
      this.signalRService.newOrder$.subscribe((order) => {
        if (order.canteenId === this.canteenId()) {
          const status = (order.status || '').toLowerCase();
          if (status === 'inpreparation' || status === 'preparing') {
            const token = this.extractToken(order.orderNumber);
            this.preparingOrders.update(list => [...list, {
              orderId: order.orderId,
              orderNumber: order.orderNumber,
              tokenNumber: token,
              status: order.status,
              createdAt: order.createdAt,
              updatedAt: order.createdAt,
              itemCount: order.items?.length || 1
            }]);
          }
        }
      })
    );

    // 4. Listen for Device Revocation from Kitchen Admin
    this.subscriptions.add(
      this.signalRService.tvDeviceRevoked$.subscribe((event) => {
        if (event.canteenId === this.canteenId()) {
          const currentDevId = this.tvDeviceId();
          if (!currentDevId || currentDevId === event.tvDeviceId) {
            this.unpairDevice('This TV display was disconnected by the kitchen manager.');
          }
        }
      })
    );
  }

  private flashReadyToken(token: string): void {
    this.newlyReadyToken.set(token);
    setTimeout(() => {
      if (this.newlyReadyToken() === token) {
        this.newlyReadyToken.set(null);
      }
    }, 45000);
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
    this.audioAlert.playOrderReadyChime();
    setTimeout(() => {
      this.audioAlert.speakToken('104', 'Counter 1');
    }, 1200);
  }

  private updateClock(): void {
    const now = new Date();
    this.currentTime.set(now.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true }));
    this.currentDate.set(now.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' }));
  }

  private extractToken(orderNumber: string): string {
    if (!orderNumber) return '';
    const parts = orderNumber.split('-');
    return parts.length > 0 ? parts[parts.length - 1] : orderNumber;
  }

  private async requestWakeLock(): Promise<void> {
    if (typeof navigator !== 'undefined' && 'wakeLock' in navigator) {
      try {
        const nav = navigator as unknown as { wakeLock: { request: (type: string) => Promise<unknown> } };
        this.wakeLockSentinel = await nav.wakeLock.request('screen');
      } catch {
        // Wake lock optional
      }
    }
  }

  private releaseWakeLock(): void {
    if (this.wakeLockSentinel) {
      try {
        const sentinel = this.wakeLockSentinel as { release: () => Promise<void> };
        sentinel.release().catch(() => {});
        this.wakeLockSentinel = null;
      } catch {
        // Ignored
      }
    }
  }
}
