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
  taxAmount?: number;
  platformFeeAmount?: number;
  platformFeePolicy?: string;
  totalAmount: number;
  canteenEarningsAmount?: number;
  trackingToken: string;
  createdAt: string;
  items: OrderItemDto[];
}
