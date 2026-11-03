import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateTvPairingRequestDto,
  TvPairingSessionResponseDto,
  ClaimTvPairingDto,
  TvClaimResultDto,
  TvDeviceDto,
  UpdateTvDeviceDto
} from '../models/tv-device.models';
import { CanteenTvDisplayDto } from '../models/order.models';

const TV_TOKEN_KEY = 'canteen_tv_device_token';
const TV_IDENTIFIER_KEY = 'canteen_tv_device_id';

@Injectable({
  providedIn: 'root'
})
export class TvDeviceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  createPairingSession(canteenId: number, dto: CreateTvPairingRequestDto): Observable<TvPairingSessionResponseDto> {
    return this.http.post<TvPairingSessionResponseDto>(
      `${this.baseUrl}/canteens/${canteenId}/tv-devices/pair-request`,
      dto
    );
  }

  claimPairingCode(dto: ClaimTvPairingDto): Observable<TvClaimResultDto> {
    return this.http.post<TvClaimResultDto>(`${this.baseUrl}/tv/pair/claim`, dto);
  }

  getTvDisplayByToken(deviceToken: string): Observable<CanteenTvDisplayDto> {
    const headers = new HttpHeaders({
      'X-Device-Token': deviceToken
    });
    return this.http.get<CanteenTvDisplayDto>(`${this.baseUrl}/tv/display`, { headers });
  }

  getDevicesByCanteen(canteenId: number): Observable<TvDeviceDto[]> {
    return this.http.get<TvDeviceDto[]>(`${this.baseUrl}/canteens/${canteenId}/tv-devices`);
  }

  updateDevice(canteenId: number, deviceId: number, dto: UpdateTvDeviceDto): Observable<TvDeviceDto> {
    return this.http.put<TvDeviceDto>(`${this.baseUrl}/canteens/${canteenId}/tv-devices/${deviceId}`, dto);
  }

  revokeDevice(canteenId: number, deviceId: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/canteens/${canteenId}/tv-devices/${deviceId}`);
  }

  // Device Token persistence
  getStoredDeviceToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(TV_TOKEN_KEY);
  }

  setStoredDeviceToken(token: string): void {
    if (typeof localStorage === 'undefined') return;
    localStorage.setItem(TV_TOKEN_KEY, token);
  }

  clearStoredDeviceToken(): void {
    if (typeof localStorage === 'undefined') return;
    localStorage.removeItem(TV_TOKEN_KEY);
  }

  // Device persistent unique fingerprint
  getOrCreateDeviceIdentifier(): string {
    if (typeof localStorage === 'undefined') {
      return 'TV_' + Math.random().toString(36).substring(2, 12);
    }

    let id = localStorage.getItem(TV_IDENTIFIER_KEY);
    if (!id) {
      id = 'TV_' + (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2, 15));
      localStorage.setItem(TV_IDENTIFIER_KEY, id);
    }
    return id;
  }
}

