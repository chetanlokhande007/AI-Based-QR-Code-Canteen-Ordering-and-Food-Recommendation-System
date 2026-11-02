import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  RecipeDto,
  CreateRecipeDto,
  UpdateRecipeDto,
  RecipeCostingSummaryDto,
  RecipeAvailabilityDto
} from '../models/recipe.models';

@Injectable({
  providedIn: 'root'
})
export class RecipeService {
  private readonly http = inject(HttpClient);

  private getUrl(canteenId: number, endpoint = ''): string {
    const base = `${environment.apiUrl}/canteens/${canteenId}/recipes`;
    return endpoint ? `${base}/${endpoint}` : base;
  }

  getAllRecipes(canteenId: number, activeOnly = false): Observable<RecipeDto[]> {
    return this.http.get<RecipeDto[]>(this.getUrl(canteenId), {
      params: { activeOnly: activeOnly.toString() }
    });
  }

  getRecipeById(canteenId: number, recipeId: number): Observable<RecipeDto> {
    return this.http.get<RecipeDto>(this.getUrl(canteenId, recipeId.toString()));
  }

  getRecipeByProductId(canteenId: number, productId: number): Observable<RecipeDto> {
    return this.http.get<RecipeDto>(this.getUrl(canteenId, `product/${productId}`));
  }

  getCostingSummaries(canteenId: number): Observable<RecipeCostingSummaryDto[]> {
    return this.http.get<RecipeCostingSummaryDto[]>(this.getUrl(canteenId, 'costing-summary'));
  }

  getPortionAvailability(canteenId: number, productId: number): Observable<RecipeAvailabilityDto> {
    return this.http.get<RecipeAvailabilityDto>(this.getUrl(canteenId, `product/${productId}/availability`));
  }

  createRecipe(canteenId: number, dto: CreateRecipeDto): Observable<RecipeDto> {
    return this.http.post<RecipeDto>(this.getUrl(canteenId), dto);
  }

  updateRecipe(canteenId: number, recipeId: number, dto: UpdateRecipeDto): Observable<RecipeDto> {
    return this.http.put<RecipeDto>(this.getUrl(canteenId, recipeId.toString()), dto);
  }

  deleteRecipe(canteenId: number, recipeId: number): Observable<void> {
    return this.http.delete<void>(this.getUrl(canteenId, recipeId.toString()));
  }
}

