export interface FoodRecommendation {
  productId: number;
  name: string;
  description: string;
  price: number;
  foodType: string;
  categoryName: string;
  imageUrl?: string | null;
  recommendationScore: number;
  badgeText: string;
  recommendationCategory: string;
  availablePortions: number;
}

export interface CartAddonRecommendation {
  productId: number;
  name: string;
  price: number;
  foodType: string;
  categoryName: string;
  imageUrl?: string | null;
  pairedWithProductName: string;
  pairConfidencePercentage: number;
  availablePortions: number;
}

export interface CartAddonRequest {
  cartProductIds: number[];
  foodTypeFilter?: string;
  limit?: number;
}

export interface RecordRecommendationEventRequest {
  productId: number;
  eventType: 'Impression' | 'Click' | 'AddToCart' | 'Ordered';
  recommendationCategory: string;
  anonymousSessionId?: string;
}

export interface TopConvertingDish {
  productId: number;
  productName: string;
  impressions: number;
  clicks: number;
  orders: number;
  conversionRate: number;
}

export interface RecommendationInsights {
  totalImpressions: number;
  totalClicks: number;
  totalAdditionsToCart: number;
  totalOrders: number;
  clickThroughRate: number;
  conversionRate: number;
  topConvertingDishes: TopConvertingDish[];
}

