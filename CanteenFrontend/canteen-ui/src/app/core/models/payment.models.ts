export interface CreatePaymentOrderRequest {
  orderId: number;
}

export interface PaymentOrderResponse {
  orderId: number;
  orderNumber: string;
  razorpayOrderId: string;
  amount: number;
  amountInPaise: number;
  currency: string;
  keyId: string;
}

export interface VerifyPaymentRequest {
  orderId: number;
  razorpayPaymentId: string;
  razorpayOrderId: string;
  razorpaySignature: string;
}

export interface PaymentDto {
  paymentId: number;
  orderId: number;
  canteenId: number;
  razorpayOrderId?: string | null;
  razorpayPaymentId?: string | null;
  amount: number;
  currency: string;
  paymentMethod: string;
  status: string;
  createdAt: string;
  paidAt?: string | null;
  failureReason?: string | null;
}

