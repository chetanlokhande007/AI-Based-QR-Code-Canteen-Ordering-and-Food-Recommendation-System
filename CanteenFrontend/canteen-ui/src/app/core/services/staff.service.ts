import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  StaffSummaryMetrics,
  StaffMember,
  CreateStaffMember,
  UpdateStaffMember,
  StaffPagedResult,
  StaffRoleConfig,
  StaffAttendance,
  MarkAttendance,
  StaffActivityLog,
  ImportStaffResult
} from '../models/staff.models';

@Injectable({
  providedIn: 'root'
})
export class StaffService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getSummaryMetrics(canteenId: number): Observable<StaffSummaryMetrics> {
    return this.http.get<StaffSummaryMetrics>(
      `${this.baseUrl}/canteens/${canteenId}/staff/metrics`
    );
  }

  getPagedStaff(
    canteenId: number,
    filter: {
      page?: number;
      pageSize?: number;
      search?: string;
      role?: string;
      status?: string;
      shift?: string;
    } = {}
  ): Observable<StaffPagedResult<StaffMember>> {
    let params = new HttpParams();
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());
    if (filter.search) params = params.set('search', filter.search.trim());
    if (filter.role && filter.role !== 'All') params = params.set('role', filter.role);
    if (filter.status && filter.status !== 'All') params = params.set('status', filter.status);
    if (filter.shift && filter.shift !== 'All') params = params.set('shift', filter.shift);

    return this.http.get<StaffPagedResult<StaffMember>>(
      `${this.baseUrl}/canteens/${canteenId}/staff`,
      { params }
    );
  }

  getStaffById(canteenId: number, staffId: number): Observable<StaffMember> {
    return this.http.get<StaffMember>(
      `${this.baseUrl}/canteens/${canteenId}/staff/${staffId}`
    );
  }

  createStaff(canteenId: number, dto: CreateStaffMember): Observable<StaffMember> {
    return this.http.post<StaffMember>(
      `${this.baseUrl}/canteens/${canteenId}/staff`,
      dto
    );
  }

  updateStaff(canteenId: number, staffId: number, dto: UpdateStaffMember): Observable<StaffMember> {
    return this.http.put<StaffMember>(
      `${this.baseUrl}/canteens/${canteenId}/staff/${staffId}`,
      dto
    );
  }

  deleteStaff(canteenId: number, staffId: number): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/canteens/${canteenId}/staff/${staffId}`
    );
  }

  getRoleConfigs(canteenId: number): Observable<StaffRoleConfig[]> {
    return this.http.get<StaffRoleConfig[]>(
      `${this.baseUrl}/canteens/${canteenId}/staff/roles`
    );
  }

  getAttendance(canteenId: number, date?: string): Observable<StaffAttendance[]> {
    let params = new HttpParams();
    if (date) params = params.set('date', date);
    return this.http.get<StaffAttendance[]>(
      `${this.baseUrl}/canteens/${canteenId}/staff/attendance`,
      { params }
    );
  }

  markAttendance(canteenId: number, dto: MarkAttendance): Observable<StaffAttendance> {
    return this.http.post<StaffAttendance>(
      `${this.baseUrl}/canteens/${canteenId}/staff/attendance`,
      dto
    );
  }

  getActivityLogs(canteenId: number, limit: number = 50): Observable<StaffActivityLog[]> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<StaffActivityLog[]>(
      `${this.baseUrl}/canteens/${canteenId}/staff/activity-logs`,
      { params }
    );
  }

  importStaff(canteenId: number, dtos: CreateStaffMember[]): Observable<ImportStaffResult> {
    return this.http.post<ImportStaffResult>(
      `${this.baseUrl}/canteens/${canteenId}/staff/import`,
      dtos
    );
  }
}

