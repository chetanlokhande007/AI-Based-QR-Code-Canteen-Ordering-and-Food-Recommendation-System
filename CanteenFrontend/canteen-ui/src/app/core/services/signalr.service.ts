import { Injectable, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { OrderDto } from '../models/order.models';

export interface NewOrderNotification {
  orderId: number;
  orderNumber: string;
  canteenId: number;
  tableId: number;
  status: string;
  totalAmount: number;
  createdAt: string;
  items: Array<{
    productId: number;
    productName: string;
    quantity: number;
    subtotal: number;
  }>;
}

export interface PaymentConfirmedNotification {
  orderId: number;
  orderNumber: string;
  canteenId: number;
  amount: number;
  paidAt: string;
}

export interface OrderStatusUpdatedNotification {
  orderId: number;
  orderNumber: string;
  newStatus: string;
  updatedAt: string;
  canteenId?: number;
}

export interface TvVoiceAnnouncementNotification {
  canteenId: number;
  orderId: number;
  orderNumber: string;
  tokenNumber: string;
  message: string;
  announcedAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private readonly authService = inject(AuthService);
  private hubConnection: signalR.HubConnection | null = null;

  private readonly newOrderSubject = new Subject<NewOrderNotification>();
  private readonly paymentConfirmedSubject = new Subject<PaymentConfirmedNotification>();
  private readonly orderStatusUpdatedSubject = new Subject<OrderStatusUpdatedNotification>();
  private readonly tvVoiceAnnouncementSubject = new Subject<TvVoiceAnnouncementNotification>();
  private readonly tvDeviceRevokedSubject = new Subject<{ tvDeviceId: number; canteenId: number }>();

  readonly newOrder$ = this.newOrderSubject.asObservable();
  readonly paymentConfirmed$ = this.paymentConfirmedSubject.asObservable();
  readonly orderStatusUpdated$ = this.orderStatusUpdatedSubject.asObservable();
  readonly tvVoiceAnnouncement$ = this.tvVoiceAnnouncementSubject.asObservable();
  readonly tvDeviceRevoked$ = this.tvDeviceRevokedSubject.asObservable();

  startConnection(): Promise<void> {
    const token = this.authService.getToken();
    if (!token) {
      return Promise.resolve();
    }

    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return Promise.resolve();
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/orders`, {
        accessTokenFactory: () => this.authService.getToken() || '',
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers();

    return this.hubConnection
      .start()
      .then(() => {
        return this.hubConnection?.invoke('JoinKitchenGroup').catch(() => {});
      })
      .catch((err) => {
        console.warn('SignalR Hub connection warning:', err?.message || err);
      });
  }

  startTvConnection(canteenId: number): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return this.hubConnection.invoke('JoinTvDisplayGroup', canteenId).catch((err) => {
        console.warn('Could not join TV display group:', err);
      });
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/orders`, {
        accessTokenFactory: () => this.authService.getToken() || '',
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers();

    return this.hubConnection
      .start()
      .then(() => {
        return this.hubConnection?.invoke('JoinTvDisplayGroup', canteenId);
      })
      .catch((err) => {
        console.warn('TV display SignalR connection error:', err?.message || err);
      });
  }

  startTrackingConnection(orderId: number, trackingToken: string): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return this.hubConnection.invoke('JoinOrderTrackingGroup', orderId, trackingToken).catch((err) => {
        console.warn('Could not join order tracking group:', err);
      });
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/orders`, {
        accessTokenFactory: () => this.authService.getToken() || '',
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers();

    return this.hubConnection
      .start()
      .then(() => {
        return this.hubConnection?.invoke('JoinOrderTrackingGroup', orderId, trackingToken);
      })
      .catch((err) => {
        console.warn('Customer tracking connection error:', err?.message || err);
      });
  }

  stopConnection(): Promise<void> {
    if (this.hubConnection) {
      const conn = this.hubConnection;
      this.hubConnection = null;
      return conn.stop().catch(() => {});
    }
    return Promise.resolve();
  }

  private registerHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('NewOrderPlaced', (data: NewOrderNotification) => {
      this.newOrderSubject.next(data);
    });

    this.hubConnection.on('PaymentConfirmed', (data: PaymentConfirmedNotification) => {
      this.paymentConfirmedSubject.next(data);
    });

    this.hubConnection.on('OrderStatusUpdated', (data: OrderStatusUpdatedNotification) => {
      this.orderStatusUpdatedSubject.next(data);
    });

    this.hubConnection.on('TvVoiceAnnouncement', (data: TvVoiceAnnouncementNotification) => {
      this.tvVoiceAnnouncementSubject.next(data);
    });

    this.hubConnection.on('TvDeviceRevoked', (data: { tvDeviceId: number; canteenId: number }) => {
      this.tvDeviceRevokedSubject.next(data);
    });
  }
}
