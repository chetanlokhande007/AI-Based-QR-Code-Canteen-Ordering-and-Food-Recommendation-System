import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  IngredientDto,
  CreateIngredientDto,
  UpdateIngredientDto,
  AdjustStockDto,
  RecordWastageDto,
  IngredientWastageDto,
  StockTransactionDto,
  InventoryDashboardSummaryDto
} from '../models/inventory.models';

@Injectable({
  providedIn: 'root'
})
export class InventoryService {
  private readonly http = inject(HttpClient);

  private getUrl(canteenId: number, endpoint = ''): string {
    const base = `${environment.apiUrl}/canteens/${canteenId}/inventory`;
    return endpoint ? `${base}/${endpoint}` : base;
  }

  getAllIngredients(canteenId: number, activeOnly = false): Observable<IngredientDto[]> {
    return this.http.get<IngredientDto[]>(this.getUrl(canteenId), {
      params: { activeOnly: activeOnly.toString() }
    });
  }

  getIngredient(canteenId: number, ingredientId: number): Observable<IngredientDto> {
    return this.http.get<IngredientDto>(this.getUrl(canteenId, ingredientId.toString()));
  }

  createIngredient(canteenId: number, dto: CreateIngredientDto): Observable<IngredientDto> {
    return this.http.post<IngredientDto>(this.getUrl(canteenId), dto);
  }

  updateIngredient(canteenId: number, ingredientId: number, dto: UpdateIngredientDto): Observable<IngredientDto> {
    return this.http.put<IngredientDto>(this.getUrl(canteenId, ingredientId.toString()), dto);
  }

  adjustStock(canteenId: number, ingredientId: number, dto: AdjustStockDto): Observable<IngredientDto> {
    return this.http.post<IngredientDto>(this.getUrl(canteenId, `${ingredientId}/adjust`), dto);
  }

  recordWastage(canteenId: number, ingredientId: number, dto: RecordWastageDto): Observable<IngredientWastageDto> {
    return this.http.post<IngredientWastageDto>(this.getUrl(canteenId, `${ingredientId}/wastage`), dto);
  }

  getTransactions(canteenId: number, ingredientId?: number, limit = 50): Observable<StockTransactionDto[]> {
    const params: Record<string, string> = { limit: limit.toString() };
    if (ingredientId) {
      params['ingredientId'] = ingredientId.toString();
    }
    return this.http.get<StockTransactionDto[]>(this.getUrl(canteenId, 'transactions'), { params });
  }

  getWastages(canteenId: number, ingredientId?: number, limit = 50): Observable<IngredientWastageDto[]> {
    const params: Record<string, string> = { limit: limit.toString() };
    if (ingredientId) {
      params['ingredientId'] = ingredientId.toString();
    }
    return this.http.get<IngredientWastageDto[]>(this.getUrl(canteenId, 'wastages'), { params });
  }

  getDashboardSummary(canteenId: number): Observable<InventoryDashboardSummaryDto> {
    return this.http.get<InventoryDashboardSummaryDto>(this.getUrl(canteenId, 'dashboard'));
  }
}

