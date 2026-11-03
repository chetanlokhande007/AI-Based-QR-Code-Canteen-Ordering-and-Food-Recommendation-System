export interface CouponDto {
  couponId: number;
  canteenId?: number;
  code: string;
  title: string;
  description?: string;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  minOrderAmount: number;
  maxDiscountAmount?: number;
  usageLimitPerCustomer: number;
  totalUsageLimit?: number;
  timesUsed: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
  createdAt: string;
  isExpired?: boolean;
  isAvailable?: boolean;
}

export interface CreateCouponDto {
  code: string;
  title: string;
  description?: string;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  minOrderAmount: number;
  maxDiscountAmount?: number;
  usageLimitPerCustomer: number;
  totalUsageLimit?: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

export interface UpdateCouponDto {
  title: string;
  description?: string;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  minOrderAmount: number;
  maxDiscountAmount?: number;
  usageLimitPerCustomer: number;
  totalUsageLimit?: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

export interface ValidateCouponRequest {
  code: string;
  foodSubtotal: number;
  customerIdentifier?: string;
}

export interface ValidateCouponResponse {
  isValid: boolean;
  message: string;
  code?: string;
  title?: string;
  discountType?: 'Percentage' | 'FixedAmount';
  discountValue?: number;
  discountAmount: number;
  netFoodSubtotal: number;
  minOrderAmount?: number;
  maxDiscountAmount?: number;
}

export interface PublicCouponDto {
  couponId: number;
  code: string;
  title: string;
  description?: string;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  minOrderAmount: number;
  maxDiscountAmount?: number;
  endDate: string;
}

export interface CouponRedemptionReport {
  totalCoupons: number;
  activeCoupons: number;
  totalRedemptions: number;
  totalDiscountGiven: number;
  totalSalesWithDiscount: number;
}

