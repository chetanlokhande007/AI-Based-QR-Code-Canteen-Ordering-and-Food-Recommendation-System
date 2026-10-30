import { CanteenTableDto } from './table.models';

export interface PlatformAdminMetrics {
  totalCanteens: number;
  activeCanteens: number;
  suspendedCanteens: number;
  totalOwners: number;
  // Platform Financial & Earnings Metrics
  platformEarnings: number;
  totalOrders: number;
  pendingSettlements: number;
  grossMerchandiseValue: number;
  recentCanteens: AdminCanteenItem[];
  registrationTrend?: MonthlyRegistrationTrend[];
  monthlyRevenueTrend?: MonthlyRevenueTrend[];
  statusDistribution?: StatusDistribution;
  recentActivities?: PlatformActivity[];
  canteenSettlements?: AdminCanteenSettlement[];
}

export interface MonthlyRevenueTrend {
  month: string;
  platformRevenue: number;
  grossVolume: number;
  ordersCount: number;
}

export interface AdminCanteenSettlement {
  canteenId: number;
  canteenName: string;
  ownerName: string;
  ownerEmail: string;
  totalOrders: number;
  grossSales: number;
  platformFeesDeducted: number;
  canteenEarnings: number;
  pendingPayableBalance: number;
  bankName?: string;
  accountNumberMasked?: string;
  ifscCode?: string;
  upiId?: string;
  isBankConfigured: boolean;
  settlementStatus: string;
}

export interface PlatformFeeSettings {
  defaultFixedFee: number;
  defaultFeePercentage: number;
  defaultFeePolicy: string;
  platformFeeRefundRatePercentage: number;
}

export interface EarningsOverview {
  totalPlatformFeesCollected: number;
  platformFeeRefunds: number;
  netPlatformRevenue: number;
  pendingSettlements: number;
  grossTransactionVolume: number;
  foodSubtotal: number;
  taxCollected: number;
  totalPaidOrders: number;
  monthlyRevenueTrend: MonthlyRevenueTrend[];
}

export interface MonthlyRegistrationTrend {
  month: string;
  count: number;
}

export interface StatusDistribution {
  total: number;
  active: number;
  suspended: number;
  setupInProgress: number;
}

export interface PlatformActivity {
  id: number;
  title: string;
  subtitle: string;
  timestamp: string;
  type: string;
}

export interface AdminCanteenItem {
  canteenId: number;
  name: string;
  location: string;
  canteenType?: string;
  ownerId?: number;
  ownerName: string;
  ownerEmail: string;
  isActive: boolean;
  tenantStatus?: string;
  setupStatus?: string;
  menuItemsCount: number;
  tablesCount: number;
  createdAt: string;
}

export interface UpdateCanteenStatusRequest {
  isActive?: boolean;
  tenantStatus?: string;
}

export interface AdminCanteenDetails {
  canteenId: number;
  name: string;
  slug: string;
  location: string;
  canteenType?: string;
  description?: string;
  contactPhone?: string;
  contactEmail?: string;
  imageUrl?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  landmark?: string;
  operatingHoursJson?: string;
  isActive: boolean;
  tenantStatus?: string;
  setupStatus?: string;
  createdAt: string;
  owner: AdminOwnerSummary;
  statistics: AdminCanteenStats;
  tables: CanteenTableDto[];
  foodItems: AdminFoodItem[];
  orders: AdminOrderSummary[];
  bankDetails: AdminBankDetails;
}

export interface AdminOwnerSummary {
  ownerId: number;
  fullName: string;
  email: string;
  role: string;
  createdAt: string;
}

export interface AdminCanteenStats {
  tablesCount: number;
  foodItemsCount: number;
  totalOrdersCount: number;
  revenue: number;
}

export interface AdminFoodItem {
  productId: number;
  name: string;
  categoryName?: string;
  price: number;
  foodType: string;
  isAvailable: boolean;
  imageUrl?: string;
  stockQuantity: number;
}

export interface AdminOrderSummary {
  orderId: number;
  orderNumber: string;
  tableNumber: number;
  totalAmount: number;
  status: string;
  paymentStatus: string;
  createdAt: string;
}

export interface AdminBankDetails {
  bankAccountHolder?: string;
  bankName?: string;
  accountNumberMasked?: string;
  ifscCode?: string;
  accountType?: string;
  upiId?: string;
  isConfigured: boolean;
}

export interface AdminPaymentOverview {
  totalTransactions: number;
  successfulCount: number;
  failedCount: number;
  totalRevenue: number;
  recentTransactions: AdminPaymentTransaction[];
}

export interface AdminPaymentTransaction {
  paymentId: number;
  transactionId?: string;
  orderId: number;
  orderNumber: string;
  canteenId: number;
  canteenName: string;
  amount: number;
  currency: string;
  status: string;
  paymentMethod: string;
  createdAt: string;
}

export interface AdminCanteenPaymentSetup {
  canteenId: number;
  canteenName: string;
  ownerName: string;
  ownerEmail: string;
  bankAccountHolder?: string;
  bankName?: string;
  accountNumberMasked?: string;
  ifscCode?: string;
  accountType?: string;
  upiId?: string;
  isConfigured: boolean;
  settlementStatus: string;
}

export interface AdminAuditLog {
  id: number;
  timestamp: string;
  user: string;
  action: string;
  details: string;
  ipAddress: string;
}

export interface AdminOwnerItem {
  ownerId: number;
  fullName: string;
  email: string;
  role: string;
  canteensCount: number;
  status: string;
  createdAt: string;
}

export interface AdminOwnerDetails {
  ownerId: number;
  fullName: string;
  email: string;
  role: string;
  status: string;
  createdAt: string;
  canteens: AdminCanteenItem[];
}
