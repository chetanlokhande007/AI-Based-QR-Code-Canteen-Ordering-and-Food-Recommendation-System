import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatePaymentOrderRequest,
  PaymentOrderResponse,
  VerifyPaymentRequest,
  PaymentDto,
  PaymentDashboardSummary,
  PaymentTransaction,
  PaymentPagedResult,
  CanteenSettlementSummary,
  PaymentMethodConfig,
  CanteenPaymentSettings,
  UpdatePaymentSettings,
  PaymentReconciliationResult,
  RefundRecord
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

  // ──────────────────────────────────────────────────────────────────────────
  // PAYMENTS DASHBOARD API METHODS (AUTHORITATIVE REAL DATA, ZERO FABRICATED DATA)
  // ──────────────────────────────────────────────────────────────────────────

  getDashboardSummary(canteenId: number, dateRange: string = 'Last 7 Days'): Observable<PaymentDashboardSummary> {
    let params = new HttpParams().set('dateRange', dateRange);
    return this.http.get<PaymentDashboardSummary>(
      `${this.baseUrl}/canteens/${canteenId}/payments/dashboard-summary`,
      { params }
    );
  }

  getTransactions(
    canteenId: number,
    filter: {
      page?: number;
      pageSize?: number;
      search?: string;
      status?: string;
      paymentMethod?: string;
      dateRange?: string;
    } = {}
  ): Observable<PaymentPagedResult<PaymentTransaction>> {
    let params = new HttpParams();
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());
    if (filter.search) params = params.set('search', filter.search.trim());
    if (filter.status && filter.status !== 'All') params = params.set('status', filter.status);
    if (filter.paymentMethod && filter.paymentMethod !== 'All') params = params.set('paymentMethod', filter.paymentMethod);
    if (filter.dateRange && filter.dateRange !== 'All') params = params.set('dateRange', filter.dateRange);

    return this.http.get<PaymentPagedResult<PaymentTransaction>>(
      `${this.baseUrl}/canteens/${canteenId}/payments/transactions`,
      { params }
    );
  }

  getSettlementSummary(canteenId: number): Observable<CanteenSettlementSummary> {
    return this.http.get<CanteenSettlementSummary>(
      `${this.baseUrl}/canteens/${canteenId}/payments/settlements`
    );
  }

  getPaymentMethods(canteenId: number): Observable<PaymentMethodConfig[]> {
    return this.http.get<PaymentMethodConfig[]>(
      `${this.baseUrl}/canteens/${canteenId}/payments/methods`
    );
  }

  getPaymentSettings(canteenId: number): Observable<CanteenPaymentSettings> {
    return this.http.get<CanteenPaymentSettings>(
      `${this.baseUrl}/canteens/${canteenId}/payments/settings`
    );
  }

  updatePaymentSettings(canteenId: number, settings: UpdatePaymentSettings): Observable<CanteenPaymentSettings> {
    return this.http.put<CanteenPaymentSettings>(
      `${this.baseUrl}/canteens/${canteenId}/payments/settings`,
      settings
    );
  }

  exportStatement(canteenId: number, dateRange: string = 'Last 7 Days'): Observable<Blob> {
    const params = new HttpParams().set('dateRange', dateRange);
    return this.http.get(
      `${this.baseUrl}/canteens/${canteenId}/payments/export`,
      { params, responseType: 'blob' }
    );
  }

  reconcilePayments(canteenId: number): Observable<PaymentReconciliationResult> {
    return this.http.post<PaymentReconciliationResult>(
      `${this.baseUrl}/canteens/${canteenId}/payments/reconcile`,
      {}
    );
  }

  getCanteenRefunds(canteenId: number): Observable<RefundRecord[]> {
    return this.http.get<RefundRecord[]>(
      `${this.baseUrl}/canteens/${canteenId}/refunds`
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
