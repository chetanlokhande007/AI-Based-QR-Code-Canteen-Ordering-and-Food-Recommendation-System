import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CanteenDto, CanteenSummaryDto, CreateCanteenRequest, PublicCanteenDto, UpdateCanteenRequest } from '../models/canteen.models';

@Injectable({
  providedIn: 'root'
})
export class CanteenService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/canteens`;

  getPublicInfo(id: number): Observable<PublicCanteenDto> {
    return this.http.get<PublicCanteenDto>(`${this.baseUrl}/${id}/public-info`);
  }

  getById(id: number): Observable<CanteenDto> {
    return this.http.get<CanteenDto>(`${this.baseUrl}/${id}`);
  }

  getMyCanteens(): Observable<CanteenSummaryDto[]> {
    return this.http.get<CanteenSummaryDto[]>(`${this.baseUrl}/my`);
  }

  create(payload: CreateCanteenRequest): Observable<CanteenDto> {
    return this.http.post<CanteenDto>(this.baseUrl, payload);
  }

  update(id: number, payload: UpdateCanteenRequest): Observable<CanteenDto> {
    return this.http.put<CanteenDto>(`${this.baseUrl}/${id}`, payload);
  }
}

