import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ReportFilter,
  OverviewReport,
  SalesReport,
  OrderReport,
  ProductReport,
  PaymentReport,
  InventoryReport,
  CouponReport,
  StaffReport,
  StaffAttendanceReport,
  KitchenReport,
  PeakHourReport,
  ExportReportRequest
} from '../models/reports.models';

@Injectable({
  providedIn: 'root'
})
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  private buildParams(filter: ReportFilter): HttpParams {
    let params = new HttpParams();
    if (filter.canteenId) params = params.set('canteenId', filter.canteenId.toString());
    if (filter.period) params = params.set('period', filter.period);
    if (filter.startDate) params = params.set('startDate', filter.startDate);
    if (filter.endDate) params = params.set('endDate', filter.endDate);
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());
    if (filter.search) params = params.set('search', filter.search);
    if (filter.sortBy) params = params.set('sortBy', filter.sortBy);
    if (filter.sortDirection) params = params.set('sortDirection', filter.sortDirection);
    return params;
  }

  getOverview(filter: ReportFilter): Observable<OverviewReport> {
    return this.http.get<OverviewReport>(`${this.baseUrl}/reports/overview`, {
      params: this.buildParams(filter)
    });
  }

  getSales(filter: ReportFilter): Observable<SalesReport> {
    return this.http.get<SalesReport>(`${this.baseUrl}/reports/sales`, {
      params: this.buildParams(filter)
    });
  }

  getOrders(filter: ReportFilter): Observable<OrderReport> {
    return this.http.get<OrderReport>(`${this.baseUrl}/reports/orders`, {
      params: this.buildParams(filter)
    });
  }

  getProducts(filter: ReportFilter): Observable<ProductReport> {
    return this.http.get<ProductReport>(`${this.baseUrl}/reports/products`, {
      params: this.buildParams(filter)
    });
  }

  getPayments(filter: ReportFilter): Observable<PaymentReport> {
    return this.http.get<PaymentReport>(`${this.baseUrl}/reports/payments`, {
      params: this.buildParams(filter)
    });
  }

  getInventory(filter: ReportFilter): Observable<InventoryReport> {
    return this.http.get<InventoryReport>(`${this.baseUrl}/reports/inventory`, {
      params: this.buildParams(filter)
    });
  }

  getCoupons(filter: ReportFilter): Observable<CouponReport> {
    return this.http.get<CouponReport>(`${this.baseUrl}/reports/coupons`, {
      params: this.buildParams(filter)
    });
  }

  getStaff(filter: ReportFilter): Observable<StaffReport> {
    return this.http.get<StaffReport>(`${this.baseUrl}/reports/staff`, {
      params: this.buildParams(filter)
    });
  }

  getAttendance(filter: ReportFilter): Observable<StaffAttendanceReport> {
    return this.http.get<StaffAttendanceReport>(`${this.baseUrl}/reports/attendance`, {
      params: this.buildParams(filter)
    });
  }

  getKitchen(filter: ReportFilter): Observable<KitchenReport> {
    return this.http.get<KitchenReport>(`${this.baseUrl}/reports/kitchen`, {
      params: this.buildParams(filter)
    });
  }

  getPeakHours(filter: ReportFilter): Observable<PeakHourReport> {
    return this.http.get<PeakHourReport>(`${this.baseUrl}/reports/peak-hours`, {
      params: this.buildParams(filter)
    });
  }

  exportReport(request: ExportReportRequest): Observable<Blob> {
    let params = this.buildParams(request);
    params = params.set('reportType', request.reportType);
    params = params.set('format', request.format);

    return this.http.get(`${this.baseUrl}/reports/export`, {
      params,
      responseType: 'blob'
    });
  }

  triggerBlobDownload(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  }
}

