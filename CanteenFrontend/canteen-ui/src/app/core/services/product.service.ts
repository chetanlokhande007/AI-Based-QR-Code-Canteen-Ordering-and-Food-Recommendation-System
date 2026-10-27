import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateProductRequest, ProductDto, UpdateProductRequest } from '../models/product.models';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/products`;

  getByCanteen(canteenId: number, availableOnly = false): Observable<ProductDto[]> {
    return this.http.get<ProductDto[]>(`${this.baseUrl}/canteen/${canteenId}`, {
      params: { availableOnly: availableOnly.toString() }
    });
  }

  getByCategory(canteenId: number, categoryId: number, availableOnly = false): Observable<ProductDto[]> {
    return this.http.get<ProductDto[]>(`${this.baseUrl}/canteen/${canteenId}/category/${categoryId}`, {
      params: { availableOnly: availableOnly.toString() }
    });
  }

  getById(id: number): Observable<ProductDto> {
    return this.http.get<ProductDto>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateProductRequest): Observable<ProductDto> {
    return this.http.post<ProductDto>(this.baseUrl, payload);
  }

  update(id: number, payload: UpdateProductRequest): Observable<ProductDto> {
    return this.http.put<ProductDto>(`${this.baseUrl}/${id}`, payload);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
