import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatePaymentOrderRequest,
  PaymentOrderResponse,
  VerifyPaymentRequest,
  PaymentDto
} from '../models/payment.models';

declare global {
  interface Window {
    Razorpay?: any;
  }
}

@Injectable({
  providedIn: 'root'
})
export class PaymentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;
  private scriptLoaded = false;

  createPaymentOrder(orderId: number): Observable<PaymentOrderResponse> {
    const payload: CreatePaymentOrderRequest = { orderId };
    return this.http.post<PaymentOrderResponse>(`${this.baseUrl}/payments/create-order`, payload);
  }

  verifyPayment(dto: VerifyPaymentRequest): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(`${this.baseUrl}/payments/verify`, dto);
  }

  payAtCounter(orderId: number): Observable<PaymentDto> {
    const payload: CreatePaymentOrderRequest = { orderId };
    return this.http.post<PaymentDto>(`${this.baseUrl}/payments/pay-at-counter`, payload);
  }

  confirmCounterPayment(canteenId: number, orderId: number): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(
      `${this.baseUrl}/canteens/${canteenId}/orders/${orderId}/confirm-counter-payment`,
      {}
    );
  }

  loadRazorpayScript(): Promise<boolean> {
    if (this.scriptLoaded && window.Razorpay) {
      return Promise.resolve(true);
    }

    return new Promise((resolve) => {
      const script = document.createElement('script');
      script.src = 'https://checkout.razorpay.com/v1/checkout.js';
      script.async = true;
      script.onload = () => {
        this.scriptLoaded = true;
        resolve(true);
      };
      script.onerror = () => {
        console.error('Failed to load Razorpay checkout script.');
        resolve(false);
      };
      document.body.appendChild(script);
    });
  }
}

