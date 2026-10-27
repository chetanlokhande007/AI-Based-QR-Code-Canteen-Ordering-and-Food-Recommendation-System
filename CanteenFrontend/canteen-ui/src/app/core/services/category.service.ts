import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CategoryDto, CreateCategoryRequest, UpdateCategoryRequest } from '../models/category.models';

@Injectable({
  providedIn: 'root'
})
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/canteens`;

  getAllByCanteen(canteenId: number): Observable<CategoryDto[]> {
    return this.http.get<CategoryDto[]>(`${this.baseUrl}/${canteenId}/categories`);
  }

  getById(canteenId: number, categoryId: number): Observable<CategoryDto> {
    return this.http.get<CategoryDto>(`${this.baseUrl}/${canteenId}/categories/${categoryId}`);
  }

  create(canteenId: number, payload: CreateCategoryRequest): Observable<CategoryDto> {
    return this.http.post<CategoryDto>(`${this.baseUrl}/${canteenId}/categories`, payload);
  }

  update(canteenId: number, categoryId: number, payload: UpdateCategoryRequest): Observable<CategoryDto> {
    return this.http.put<CategoryDto>(`${this.baseUrl}/${canteenId}/categories/${categoryId}`, payload);
  }

  delete(canteenId: number, categoryId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${canteenId}/categories/${categoryId}`);
  }
}
