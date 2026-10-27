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

  readonly newOrder$ = this.newOrderSubject.asObservable();
  readonly paymentConfirmed$ = this.paymentConfirmedSubject.asObservable();
  readonly orderStatusUpdated$ = this.orderStatusUpdatedSubject.asObservable();

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
        // Explicitly invoke JoinKitchenGroup
        return this.hubConnection?.invoke('JoinKitchenGroup').catch(() => {
          // Auto-joined on connect if claims present
        });
      })
      .catch((err) => {
        console.warn('SignalR Hub connection warning:', err?.message || err);
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
  }
}
