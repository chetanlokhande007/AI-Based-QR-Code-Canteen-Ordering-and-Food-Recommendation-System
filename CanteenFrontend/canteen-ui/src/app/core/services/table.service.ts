import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CanteenTableDto, CreateTableRequest } from '../models/table.models';

@Injectable({
  providedIn: 'root'
})
export class TableService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getTablesByCanteen(canteenId: number): Observable<CanteenTableDto[]> {
    return this.http.get<CanteenTableDto[]>(`${this.baseUrl}/canteens/${canteenId}/tables`);
  }

  getTableById(canteenId: number, tableId: number): Observable<CanteenTableDto> {
    return this.http.get<CanteenTableDto>(`${this.baseUrl}/canteens/${canteenId}/tables/${tableId}`);
  }

  createTable(canteenId: number, payload: CreateTableRequest): Observable<CanteenTableDto> {
    return this.http.post<CanteenTableDto>(`${this.baseUrl}/canteens/${canteenId}/tables`, payload);
  }

  getQrImageUrl(canteenId: number, tableId: number): string {
    return `${this.baseUrl}/canteens/${canteenId}/tables/${tableId}/qr-image`;
  }

  getQrCodeUrl(canteenId: number, tableId: number): string {
    return `${this.baseUrl}/canteens/${canteenId}/tables/${tableId}/qr-image`;
  }

  deleteTable(canteenId: number, tableId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/canteens/${canteenId}/tables/${tableId}`);
  }

  regenerateQr(canteenId: number, tableId: number): Observable<CanteenTableDto> {
    return this.http.post<CanteenTableDto>(`${this.baseUrl}/canteens/${canteenId}/tables/${tableId}/regenerate-qr`, {});
  }
}
