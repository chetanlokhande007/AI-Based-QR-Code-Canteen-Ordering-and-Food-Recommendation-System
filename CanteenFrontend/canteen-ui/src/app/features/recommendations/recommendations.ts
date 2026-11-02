import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RecommendationService } from '../../core/services/recommendation.service';
import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import {
  RecommendationInsights,
  FoodRecommendation,
  TopConvertingDish
} from '../../core/models/recommendation.models';
import { CanteenSummaryDto } from '../../core/models/canteen.models';

@Component({
  selector: 'app-recommendations',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './recommendations.html',
  styleUrl: './recommendations.css'
})
export class RecommendationsComponent implements OnInit {
  private readonly recommendationService = inject(RecommendationService);
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);

  // State Signals
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly activeCanteen = signal<CanteenSummaryDto | null>(null);

  // Analytics Filter & Data
  readonly selectedDays = signal<number>(30);
  readonly insights = signal<RecommendationInsights | null>(null);

  // Live Recommendation Simulator
  readonly activePreviewTab = signal<'current' | 'veg' | 'nonveg'>('current');
  readonly previewRecommendations = signal<FoodRecommendation[]>([]);
  readonly loadingPreview = signal<boolean>(false);

  ngOnInit(): void {
    this.resolveCurrentCanteenAndLoad();
  }

  resolveCurrentCanteenAndLoad(): void {
    const user = this.authService.currentUser();
    const canteenId = user?.canteenId;

    if (canteenId) {
      this.canteenService.getMyCanteens().subscribe({
        next: (canteens) => {
          const match = canteens.find((c) => c.canteenId === canteenId) || canteens[0];
          if (match) {
            this.activeCanteen.set(match);
            this.loadInsights(match.canteenId);
            this.loadLivePreview(match.canteenId);
          } else {
            this.errorMessage.set('No active canteen found for your account.');
            this.loading.set(false);
          }
        },
        error: (err) => {
          console.error('Failed to load canteens:', err);
          this.errorMessage.set('Could not fetch canteen profile.');
          this.loading.set(false);
        }
      });
    } else {
      this.errorMessage.set('Please select or configure a canteen first.');
      this.loading.set(false);
    }
  }

  loadInsights(canteenId: number): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.recommendationService.getInsights(canteenId, this.selectedDays()).subscribe({
      next: (data) => {
        this.insights.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load recommendation insights:', err);
        this.errorMessage.set(
          err?.error?.message || 'Could not load recommendation performance insights.'
        );
        this.loading.set(false);
      }
    });
  }

  loadLivePreview(canteenId: number): void {
    this.loadingPreview.set(true);
    let filter: string | undefined = undefined;
    if (this.activePreviewTab() === 'veg') filter = 'Veg';
    if (this.activePreviewTab() === 'nonveg') filter = 'NonVeg';

    this.recommendationService.getMenuRecommendations(canteenId, filter, 8).subscribe({
      next: (recs) => {
        this.previewRecommendations.set(recs || []);
        this.loadingPreview.set(false);
      },
      error: (err) => {
        console.warn('Failed to load live preview:', err);
        this.loadingPreview.set(false);
      }
    });
  }

  onDaysFilterChange(days: number): void {
    this.selectedDays.set(days);
    const canteen = this.activeCanteen();
    if (canteen) {
      this.loadInsights(canteen.canteenId);
    }
  }

  onPreviewTabChange(tab: 'current' | 'veg' | 'nonveg'): void {
    this.activePreviewTab.set(tab);
    const canteen = this.activeCanteen();
    if (canteen) {
      this.loadLivePreview(canteen.canteenId);
    }
  }

  refreshAll(): void {
    const canteen = this.activeCanteen();
    if (canteen) {
      this.loadInsights(canteen.canteenId);
      this.loadLivePreview(canteen.canteenId);
    }
  }

  getPerformanceBadgeClass(rate: number): string {
    if (rate >= 20) return 'badge-perf-high';
    if (rate >= 10) return 'badge-perf-medium';
    return 'badge-perf-low';
  }

  getPerformanceLabel(rate: number): string {
    if (rate >= 20) return 'High Velocity';
    if (rate >= 10) return 'Healthy Conversion';
    return 'Needs Promotion';
  }
}

