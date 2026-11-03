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

export interface PaymentDashboardSummary {
  totalAmount: number;
  totalCount: number;
  successfulAmount: number;
  successfulCount: number;
  successfulPercentage: number;
  failedAmount: number;
  failedCount: number;
  failedPercentage: number;
  refundedAmount: number;
  refundedCount: number;
  refundedPercentage: number;
  comparisonPercentage?: number | null;
  comparisonLabel: string;
  period: string;
}

export interface PaymentTransaction {
  paymentId: number;
  orderId: number;
  orderNumber: string;
  transactionId: string;
  customerName: string;
  customerInitials: string;
  customerPhone?: string | null;
  tableNumber?: string | null;
  amount: number;
  currency: string;
  paymentMethod: string;
  status: string;
  failureReason?: string | null;
  createdAt: string;
  paidAt?: string | null;
}

export interface PaymentPagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CanteenSettlementSummary {
  availableForSettlement: number;
  grossSales: number;
  platformFeesDeducted: number;
  totalRefundsDeducted: number;
  netSettlementAmount: number;
  settlementStatus: string;
  bankName?: string | null;
  accountNumberMasked?: string | null;
  ifscCode?: string | null;
  accountHolder?: string | null;
  accountType?: string | null;
  upiId?: string | null;
  isBankConfigured: boolean;
  nextSettlementDate?: string | null;
  payoutSchedule: string;
}

export interface PaymentMethodConfig {
  id: string;
  name: string;
  subtitle: string;
  provider: string;
  isActive: boolean;
  badgeText: string;
}

export interface CanteenPaymentSettings {
  canteenId: number;
  bankName?: string | null;
  accountNumber?: string | null;
  ifscCode?: string | null;
  bankAccountHolder?: string | null;
  accountType?: string | null;
  upiId?: string | null;
  feeAbsorptionPolicy: string;
  taxPercentage: number;
}

export interface UpdatePaymentSettings {
  bankName?: string | null;
  accountNumber?: string | null;
  ifscCode?: string | null;
  bankAccountHolder?: string | null;
  accountType?: string | null;
  upiId?: string | null;
  feeAbsorptionPolicy?: string | null;
  taxPercentage?: number | null;
}

export interface PaymentReconciliationResult {
  totalChecked: number;
  reconciled: number;
  discrepanciesFound: number;
  message: string;
  reconciledAt: string;
}

export interface RefundRecord {
  refundId: number;
  orderId: number;
  orderNumber: string;
  paymentId: number;
  canteenId: number;
  canteenName?: string;
  amount: number;
  currency: string;
  refundMethod: string;
  status: string;
  gatewayRefundId?: string | null;
  reason: string;
  initiatedByRole: string;
  failureMessage?: string | null;
  createdAt: string;
  processedAt?: string | null;
}
