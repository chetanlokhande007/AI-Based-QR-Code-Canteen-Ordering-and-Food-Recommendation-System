import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminCanteenItem, PlatformAdminMetrics, UpdateCanteenStatusRequest } from '../models/admin.models';

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

  updateCanteenStatus(canteenId: number, isActive: boolean): Observable<{ canteenId: number; isActive: boolean; message: string }> {
    const payload: UpdateCanteenStatusRequest = { isActive };
    return this.http.patch<{ canteenId: number; isActive: boolean; message: string }>(
      `${this.baseUrl}/canteens/${canteenId}/status`,
      payload
    );
  }
}

