import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RecommendationService } from './recommendation.service';
import { environment } from '../../../environments/environment';
import {
  FoodRecommendation,
  CartAddonRecommendation,
  CartAddonRequest,
  RecordRecommendationEventRequest,
  RecommendationInsights
} from '../models/recommendation.models';

describe('RecommendationService', () => {
  let service: RecommendationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        RecommendationService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(RecommendationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created and generate an anonymous session id', () => {
    expect(service).toBeTruthy();
    const sessionId = service.getAnonymousSessionId();
    expect(sessionId).toBeTruthy();
    expect(sessionId.startsWith('anon_')).toBe(true);

    // Subsequent calls in the same session return the identical ID
    expect(service.getAnonymousSessionId()).toBe(sessionId);
  });

  it('should call getMenuRecommendations with appropriate query params', () => {
    const mockRecommendations: FoodRecommendation[] = [
      {
        productId: 101,
        name: 'Masala Dosa',
        description: 'Crispy rice crepe',
        price: 90,
        foodType: 'Veg',
        categoryName: 'Breakfast',
        imageUrl: null,
        recommendationScore: 88.5,
        badgeText: 'Trending Breakfast',
        recommendationCategory: 'Breakfast',
        availablePortions: 12
      }
    ];

    service.getMenuRecommendations(1, 'Veg', 4).subscribe((recs) => {
      expect(recs.length).toBe(1);
      expect(recs[0].name).toBe('Masala Dosa');
      expect(recs[0].badgeText).toBe('Trending Breakfast');
    });

    const req = httpMock.expectOne((r) =>
      r.url === `${environment.apiUrl}/canteens/1/recommendations/menu` &&
      r.params.get('foodType') === 'Veg' &&
      r.params.get('limit') === '4'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockRecommendations);
  });

  it('should call getCartAddons with POST payload', () => {
    const mockAddons: CartAddonRecommendation[] = [
      {
        productId: 202,
        name: 'Filter Coffee',
        price: 25,
        foodType: 'Veg',
        categoryName: 'Beverages',
        imageUrl: null,
        pairedWithProductName: 'Masala Dosa',
        pairConfidencePercentage: 84,
        availablePortions: 25
      }
    ];

    const request: CartAddonRequest = {
      cartProductIds: [101],
      limit: 3
    };

    service.getCartAddons(1, request).subscribe((addons) => {
      expect(addons.length).toBe(1);
      expect(addons[0].pairedWithProductName).toBe('Masala Dosa');
      expect(addons[0].pairConfidencePercentage).toBe(84);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/canteens/1/recommendations/cart-addons`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(mockAddons);
  });

  it('should record an interaction event with anonymous session ID', () => {
    const eventReq: RecordRecommendationEventRequest = {
      productId: 101,
      eventType: 'Click',
      recommendationCategory: 'Breakfast'
    };

    service.recordEvent(1, eventReq).subscribe((res) => {
      expect(res.success).toBe(true);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/canteens/1/recommendations/events`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.productId).toBe(101);
    expect(req.request.body.eventType).toBe('Click');
    expect(req.request.body.anonymousSessionId).toBeTruthy();
    req.flush({ success: true });
  });

  it('should retrieve recommendation insights for canteen owner', () => {
    const mockInsights: RecommendationInsights = {
      totalImpressions: 1200,
      totalClicks: 240,
      totalAdditionsToCart: 160,
      totalOrders: 90,
      clickThroughRate: 20.0,
      conversionRate: 37.5,
      topConvertingDishes: [
        {
          productId: 101,
          productName: 'Masala Dosa',
          impressions: 400,
          clicks: 120,
          orders: 50,
          conversionRate: 41.67
        }
      ]
    };

    service.getInsights(1, 30).subscribe((insights) => {
      expect(insights.totalImpressions).toBe(1200);
      expect(insights.clickThroughRate).toBe(20.0);
      expect(insights.topConvertingDishes.length).toBe(1);
    });

    const req = httpMock.expectOne(
      `${environment.apiUrl}/canteens/1/recommendations/insights?daysBack=30`
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockInsights);
  });
});

