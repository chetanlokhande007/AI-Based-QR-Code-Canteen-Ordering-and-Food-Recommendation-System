import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { RecommendationsComponent } from './recommendations';
import { RecommendationService } from '../../core/services/recommendation.service';
import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { RecommendationInsights, FoodRecommendation } from '../../core/models/recommendation.models';
import { CanteenSummaryDto } from '../../core/models/canteen.models';

describe('RecommendationsComponent', () => {
  let component: RecommendationsComponent;
  let fixture: ComponentFixture<RecommendationsComponent>;
  let recommendationServiceSpy: any;
  let authServiceSpy: any;
  let canteenServiceSpy: any;

  const mockInsights: RecommendationInsights = {
    totalImpressions: 500,
    totalClicks: 100,
    totalAdditionsToCart: 70,
    totalOrders: 35,
    clickThroughRate: 20.0,
    conversionRate: 35.0,
    topConvertingDishes: [
      {
        productId: 1,
        productName: 'Paneer Butter Masala',
        impressions: 250,
        clicks: 60,
        orders: 25,
        conversionRate: 41.67
      }
    ]
  };

  const mockLivePreview: FoodRecommendation[] = [
    {
      productId: 1,
      name: 'Paneer Butter Masala',
      description: 'Rich curry',
      price: 180,
      foodType: 'Veg',
      categoryName: 'Main Course',
      imageUrl: null,
      recommendationScore: 92.5,
      badgeText: 'Lunch Favorite',
      recommendationCategory: 'Lunch',
      availablePortions: 14
    }
  ];

  const mockCanteens: CanteenSummaryDto[] = [
    {
      canteenId: 1,
      name: 'Main Campus Food Court',
      location: 'Block A, Ground Floor',
      status: 'Active',
      menuItemsCount: 24,
      tablesCount: 8,
      todayOrdersCount: 15,
      isActive: true,
      isSetupComplete: true,
      createdAt: '2026-01-01T00:00:00Z'
    }
  ];

  beforeEach(async () => {
    recommendationServiceSpy = {
      getInsights: vi.fn().mockReturnValue(of(mockInsights)),
      getMenuRecommendations: vi.fn().mockReturnValue(of(mockLivePreview)),
      getAnonymousSessionId: vi.fn().mockReturnValue('anon_test_123'),
      recordEvent: vi.fn().mockReturnValue(of({ success: true }))
    };

    authServiceSpy = {
      currentUser: vi.fn().mockReturnValue({
        userId: 'owner-1',
        email: 'owner@canteen.com',
        role: 'CanteenOwner',
        canteenId: 1
      })
    };

    canteenServiceSpy = {
      getMyCanteens: vi.fn().mockReturnValue(of(mockCanteens))
    };

    await TestBed.configureTestingModule({
      imports: [RecommendationsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RecommendationService, useValue: recommendationServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        { provide: CanteenService, useValue: canteenServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RecommendationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load initial insights and live preview', () => {
    expect(component).toBeTruthy();
    expect(recommendationServiceSpy.getInsights).toHaveBeenCalledWith(1, 30);
    expect(recommendationServiceSpy.getMenuRecommendations).toHaveBeenCalledWith(1, undefined, 8);
    expect(component.insights()?.totalImpressions).toBe(500);
    expect(component.previewRecommendations().length).toBe(1);
    expect(component.previewRecommendations()[0].badgeText).toBe('Lunch Favorite');
  });

  it('should reload insights when changing performance days filter', () => {
    component.onDaysFilterChange(7);
    expect(component.selectedDays()).toBe(7);
    expect(recommendationServiceSpy.getInsights).toHaveBeenCalledWith(1, 7);
  });

  it('should filter live recommendations when changing preview tab', () => {
    component.onPreviewTabChange('veg');
    expect(component.activePreviewTab()).toBe('veg');
    expect(recommendationServiceSpy.getMenuRecommendations).toHaveBeenCalledWith(1, 'Veg', 8);

    component.onPreviewTabChange('nonveg');
    expect(component.activePreviewTab()).toBe('nonveg');
    expect(recommendationServiceSpy.getMenuRecommendations).toHaveBeenCalledWith(1, 'NonVeg', 8);
  });

  it('should assign correct performance labels and badges based on conversion rate', () => {
    expect(component.getPerformanceLabel(25)).toBe('High Velocity');
    expect(component.getPerformanceBadgeClass(25)).toBe('badge-perf-high');

    expect(component.getPerformanceLabel(14)).toBe('Healthy Conversion');
    expect(component.getPerformanceBadgeClass(14)).toBe('badge-perf-medium');

    expect(component.getPerformanceLabel(5)).toBe('Needs Promotion');
    expect(component.getPerformanceBadgeClass(5)).toBe('badge-perf-low');
  });
});
