export interface OrderItemDto {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
}

export interface OrderDto {
  orderId: number;
  canteenId: number;
  tableId: number;
  orderNumber: string;
  status: string;
  paymentStatus: string;
  subtotalAmount?: number;
  discountAmount?: number;
  couponCode?: string;
  couponId?: number;
  taxAmount?: number;
  platformFeeAmount?: number;
  platformFeePolicy?: string;
  totalAmount: number;
  canteenEarningsAmount?: number;
  trackingToken: string;
  refundedAmount?: number;
  cancellationNote?: string;
  createdAt: string;
  items: OrderItemDto[];
}

export interface TvTokenDto {
  orderId: number;
  orderNumber: string;
  tokenNumber: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  itemCount: number;
}

export interface CanteenTvDisplayDto {
  canteenId: number;
  canteenName: string;
  stationScope?: string;
  deviceName?: string;
  preparingOrders: TvTokenDto[];
  readyOrders: TvTokenDto[];
}

export interface TvVoiceAnnouncementDto {
  canteenId: number;
  orderId: number;
  orderNumber: string;
  tokenNumber: string;
  message: string;
  announcedAt: string;
}

export interface KitchenTicket extends OrderDto {
  tokenNumber: string;
  elapsedMinutes: number;
  urgency: 'normal' | 'warning' | 'critical';
  checkedItems: { [productId: number]: boolean };
}
