import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  FoodRecommendation,
  CartAddonRecommendation,
  CartAddonRequest,
  RecordRecommendationEventRequest,
  RecommendationInsights
} from '../models/recommendation.models';

const ANON_SESSION_STORAGE_KEY = 'canteen_hub_anon_session_id';

@Injectable({
  providedIn: 'root'
})
export class RecommendationService {
  private readonly http = inject(HttpClient);

  /**
   * Retrieves or initializes a privacy-preserving anonymous session ID for telemetry.
   */
  getAnonymousSessionId(): string {
    try {
      let sessionId = sessionStorage.getItem(ANON_SESSION_STORAGE_KEY);
      if (!sessionId) {
        sessionId = 'anon_' + Math.random().toString(36).substring(2, 11) + '_' + Date.now();
        sessionStorage.setItem(ANON_SESSION_STORAGE_KEY, sessionId);
      }
      return sessionId;
    } catch {
      return 'anon_fallback_' + Date.now();
    }
  }

  private getBaseUrl(canteenId: number): string {
    return `${environment.apiUrl}/canteens/${canteenId}/recommendations`;
  }

  /**
   * Fetches AI-ranked menu recommendations for customer browsing.
   */
  getMenuRecommendations(
    canteenId: number,
    foodType?: string,
    limit: number = 6
  ): Observable<FoodRecommendation[]> {
    let params = new HttpParams().set('limit', limit.toString());
    if (foodType && foodType !== 'All') {
      params = params.set('foodType', foodType);
    }

    return this.http.get<FoodRecommendation[]>(`${this.getBaseUrl(canteenId)}/menu`, { params });
  }

  /**
   * Fetches smart cross-sell add-on recommendations based on basket association mining.
   */
  getCartAddons(
    canteenId: number,
    request: CartAddonRequest
  ): Observable<CartAddonRecommendation[]> {
    return this.http.post<CartAddonRecommendation[]>(
      `${this.getBaseUrl(canteenId)}/cart-addons`,
      request
    );
  }

  /**
   * Records a customer interaction event with recommended dishes.
   */
  recordEvent(
    canteenId: number,
    request: RecordRecommendationEventRequest
  ): Observable<{ success: boolean }> {
    const payload: RecordRecommendationEventRequest = {
      ...request,
      anonymousSessionId: request.anonymousSessionId || this.getAnonymousSessionId()
    };

    return this.http.post<{ success: boolean }>(
      `${this.getBaseUrl(canteenId)}/events`,
      payload
    );
  }

  /**
   * Retrieves AI recommendation performance insights for canteen owner.
   */
  getInsights(
    canteenId: number,
    daysBack: number = 30
  ): Observable<RecommendationInsights> {
    const params = new HttpParams().set('daysBack', daysBack.toString());
    return this.http.get<RecommendationInsights>(
      `${this.getBaseUrl(canteenId)}/insights`,
      { params }
    );
  }
}

