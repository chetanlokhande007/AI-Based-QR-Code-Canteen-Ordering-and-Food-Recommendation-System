import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CouponDto,
  CreateCouponDto,
  UpdateCouponDto,
  ValidateCouponRequest,
  ValidateCouponResponse,
  PublicCouponDto,
  CouponRedemptionReport
} from '../models/coupon.models';

@Injectable({
  providedIn: 'root'
})
export class CouponService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl || 'http://localhost:5000/api';

  getActiveCoupons(canteenId: number): Observable<PublicCouponDto[]> {
    return this.http.get<PublicCouponDto[]>(`${this.baseUrl}/canteens/${canteenId}/coupons/active`);
  }

  validateCoupon(canteenId: number, request: ValidateCouponRequest): Observable<ValidateCouponResponse> {
    return this.http.post<ValidateCouponResponse>(`${this.baseUrl}/canteens/${canteenId}/coupons/validate`, request);
  }

  getAllCoupons(canteenId: number): Observable<CouponDto[]> {
    return this.http.get<CouponDto[]>(`${this.baseUrl}/canteens/${canteenId}/coupons`);
  }

  getCouponById(canteenId: number, couponId: number): Observable<CouponDto> {
    return this.http.get<CouponDto>(`${this.baseUrl}/canteens/${canteenId}/coupons/${couponId}`);
  }

  createCoupon(canteenId: number, dto: CreateCouponDto): Observable<CouponDto> {
    return this.http.post<CouponDto>(`${this.baseUrl}/canteens/${canteenId}/coupons`, dto);
  }

  updateCoupon(canteenId: number, couponId: number, dto: UpdateCouponDto): Observable<CouponDto> {
    return this.http.put<CouponDto>(`${this.baseUrl}/canteens/${canteenId}/coupons/${couponId}`, dto);
  }

  toggleCouponStatus(canteenId: number, couponId: number): Observable<{ couponId: number; isActive: boolean; message: string }> {
    return this.http.patch<{ couponId: number; isActive: boolean; message: string }>(
      `${this.baseUrl}/canteens/${canteenId}/coupons/${couponId}/toggle`,
      {}
    );
  }

  deleteCoupon(canteenId: number, couponId: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/canteens/${canteenId}/coupons/${couponId}`);
  }

  getRedemptionReport(canteenId: number): Observable<CouponRedemptionReport> {
    return this.http.get<CouponRedemptionReport>(`${this.baseUrl}/canteens/${canteenId}/coupons/reports/redemptions`);
  }
}

