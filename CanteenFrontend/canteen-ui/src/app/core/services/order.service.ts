import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { OrderDto } from '../models/order.models';

@Injectable({
  providedIn: 'root'
})
export class OrderService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getOrdersByCanteen(canteenId: number): Observable<OrderDto[]> {
    return this.http.get<OrderDto[]>(`${this.baseUrl}/canteens/${canteenId}/orders`);
  }

  getOrderById(canteenId: number, orderId: number): Observable<OrderDto> {
    return this.http.get<OrderDto>(`${this.baseUrl}/canteens/${canteenId}/orders/${orderId}`);
  }

  updateOrderStatus(canteenId: number, orderId: number, status: string): Observable<OrderDto> {
    return this.http.patch<OrderDto>(`${this.baseUrl}/canteens/${canteenId}/orders/${orderId}/status`, { status });
  }
}
