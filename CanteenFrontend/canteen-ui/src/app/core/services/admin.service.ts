import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminAuditLog,
  AdminCanteenDetails,
  AdminCanteenItem,
  AdminCanteenPaymentSetup,
  AdminOwnerDetails,
  AdminOwnerItem,
  AdminPaymentOverview,
  PlatformAdminMetrics,
  AdminCanteenSettlement,
  EarningsOverview,
  PlatformFeeSettings,
  UpdateCanteenStatusRequest
} from '../models/admin.models';
import { CanteenTableDto } from '../models/table.models';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin`;

  getMetrics(): Observable<PlatformAdminMetrics> {
    return this.http.get<PlatformAdminMetrics>(`${this.baseUrl}/metrics`);
  }

  getCanteens(): Observable<AdminCanteenItem[]> {
    return this.http.get<AdminCanteenItem[]>(`${this.baseUrl}/canteens`);
  }

  getCanteenDetails(canteenId: number): Observable<AdminCanteenDetails> {
    return this.http.get<AdminCanteenDetails>(`${this.baseUrl}/canteens/${canteenId}/details`);
  }

  updateCanteenStatus(canteenId: number, isActive: boolean): Observable<{ canteenId: number; isActive: boolean; message: string }> {
    const payload: UpdateCanteenStatusRequest = { isActive };
    return this.http.patch<{ canteenId: number; isActive: boolean; message: string }>(
      `${this.baseUrl}/canteens/${canteenId}/status`,
      payload
    );
  }

  getCanteenTables(canteenId: number): Observable<CanteenTableDto[]> {
    return this.http.get<CanteenTableDto[]>(`${this.baseUrl}/canteens/${canteenId}/tables`);
  }

  getAdminQrImageUrl(canteenId: number, tableId: number): string {
    return `${this.baseUrl}/canteens/${canteenId}/tables/${tableId}/qr-image`;
  }

  getPaymentOverview(): Observable<AdminPaymentOverview> {
    return this.http.get<AdminPaymentOverview>(`${this.baseUrl}/payments/overview`);
  }

  getCanteensPaymentSetup(): Observable<AdminCanteenPaymentSetup[]> {
    return this.http.get<AdminCanteenPaymentSetup[]>(`${this.baseUrl}/payments/canteens-setup`);
  }

  getAuditLogs(): Observable<AdminAuditLog[]> {
    return this.http.get<AdminAuditLog[]>(`${this.baseUrl}/audit-logs`);
  }

  getOwners(): Observable<AdminOwnerItem[]> {
    return this.http.get<AdminOwnerItem[]>(`${this.baseUrl}/owners`);
  }

  getOwnerDetails(ownerId: number): Observable<AdminOwnerDetails> {
    return this.http.get<AdminOwnerDetails>(`${this.baseUrl}/owners/${ownerId}`);
  }

  getEarningsOverview(): Observable<EarningsOverview> {
    return this.http.get<EarningsOverview>(`${this.baseUrl}/earnings/overview`);
  }

  getSettlements(): Observable<AdminCanteenSettlement[]> {
    return this.http.get<AdminCanteenSettlement[]>(`${this.baseUrl}/settlements`);
  }

  getFeeSettings(): Observable<PlatformFeeSettings> {
    return this.http.get<PlatformFeeSettings>(`${this.baseUrl}/fee-settings`);
  }

  updateFeeSettings(dto: { defaultFixedFee?: number; defaultFeePolicy?: string }): Observable<PlatformFeeSettings> {
    return this.http.put<PlatformFeeSettings>(`${this.baseUrl}/fee-settings`, dto);
  }

  recordPayout(canteenId: number, dto: { amount: number; referenceNumber: string; notes?: string }): Observable<{ success: boolean; message: string; settledAt: string }> {
    return this.http.post<{ success: boolean; message: string; settledAt: string }>(`${this.baseUrl}/settlements/${canteenId}/payout`, dto);
  }
}
