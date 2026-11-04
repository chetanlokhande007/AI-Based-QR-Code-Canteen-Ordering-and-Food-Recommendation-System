export interface ReportFilter {
  canteenId?: number;
  period?: 'today' | 'yesterday' | 'this_week' | 'last_week' | 'this_month' | 'last_month' | 'custom' | string;
  startDate?: string;
  endDate?: string;
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDirection?: string;
}

export interface ReportPagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ReportTimeSeriesPoint {
  dateLabel: string;
  date: string;
  grossSales: number;
  netSales: number;
  orderCount: number;
}

export interface ReportDistribution {
  label: string;
  count: number;
  percentage: number;
  totalValue: number;
}

export interface TopProductSummary {
  productId: number;
  name: string;
  categoryName: string;
  quantitySold: number;
  totalSales: number;
}

export interface OverviewReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  grossRevenue: number;
  netRevenue: number;
  totalRefunds: number;
  totalOrders: number;
  completedOrders: number;
  cancelledOrders: number;
  averageOrderValue: number;
  activeProductsCount: number;
  grossRevenueGrowthPercent: number;
  netRevenueGrowthPercent: number;
  ordersGrowthPercent: number;
  averageOrderValueGrowthPercent: number;
  revenueTrend: ReportTimeSeriesPoint[];
  orderStatusBreakdown: ReportDistribution[];
  paymentMethodBreakdown: ReportDistribution[];
  topSellingProducts: TopProductSummary[];
}

export interface SalesDailyTimelinePoint {
  date: string;
  dateLabel: string;
  grossSales: number;
  netSales: number;
  orderCount: number;
  taxCollected: number;
  discountsGiven: number;
  refundsIssued: number;
}

export interface SalesPaymentMethodBreakdown {
  paymentMethod: string;
  transactionsCount: number;
  grossAmount: number;
  refundAmount: number;
  netAmount: number;
  sharePercentage: number;
}

export interface SalesCategoryBreakdown {
  categoryName: string;
  quantitySold: number;
  totalSales: number;
  sharePercentage: number;
}

export interface SaleTransactionDetail {
  orderId: number;
  orderNumber: string;
  tableNumber: string;
  createdAt: string;
  subtotal: number;
  discount: number;
  tax: number;
  totalAmount: number;
  refundedAmount: number;
  netAmount: number;
  paymentMethod: string;
  orderStatus: string;
  paymentStatus: string;
}

export interface SalesReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  grossSales: number;
  netSales: number;
  subtotalAmount: number;
  totalDiscounts: number;
  totalTaxCollected: number;
  totalPlatformFees: number;
  totalCanteenEarnings: number;
  totalRefunds: number;
  successfulOrdersCount: number;
  averageOrderValue: number;
  grossSalesGrowthPercent: number;
  netSalesGrowthPercent: number;
  ordersGrowthPercent: number;
  dailyTimeline: SalesDailyTimelinePoint[];
  paymentMethodSummary: SalesPaymentMethodBreakdown[];
  categorySales: SalesCategoryBreakdown[];
  transactions: ReportPagedResult<SaleTransactionDetail>;
}

export interface CancellationReasonStat {
  reason: string;
  count: number;
  percentage: number;
  totalRefundedAmount: number;
}

export interface OrderTimelinePoint {
  dateLabel: string;
  date: string;
  totalOrders: number;
  completedOrders: number;
  cancelledOrders: number;
}

export interface OrderDetailReportItem {
  orderId: number;
  orderNumber: string;
  tableNumber: string;
  totalItems: number;
  subtotalAmount: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  status: string;
  paymentStatus: string;
  cancellationReason?: string;
  createdAt: string;
}

export interface OrderReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalOrders: number;
  completedOrders: number;
  cancelledOrders: number;
  inProgressOrders: number;
  fulfillmentRate: number;
  cancellationRate: number;
  totalOrderValue: number;
  cancelledValue: number;
  statusBreakdown: ReportDistribution[];
  cancellationReasons: CancellationReasonStat[];
  timeline: OrderTimelinePoint[];
  orders: ReportPagedResult<OrderDetailReportItem>;
}

export interface ProductPerformanceDetail {
  productId: number;
  name: string;
  categoryName: string;
  foodType: string;
  unitPrice: number;
  quantitySold: number;
  totalRevenue: number;
  currentStock: number;
  isAvailable: boolean;
  revenueSharePercentage: number;
}

export interface CategoryPerformanceStat {
  categoryId: number;
  categoryName: string;
  totalProducts: number;
  unitsSold: number;
  totalSales: number;
  revenueSharePercentage: number;
}

export interface ProductReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalProductsInCatalog: number;
  activeProductsCount: number;
  outOfStockCount: number;
  totalUnitsSold: number;
  totalRevenueGenerated: number;
  topSellingProductName: string;
  topSellingProducts: ProductPerformanceDetail[];
  slowMovingProducts: ProductPerformanceDetail[];
  categoryPerformance: CategoryPerformanceStat[];
  products: ReportPagedResult<ProductPerformanceDetail>;
}

export interface PaymentMethodLedgerSummary {
  method: string;
  totalTransactions: number;
  successCount: number;
  failedCount: number;
  totalCollected: number;
  totalRefunded: number;
  netCollected: number;
  sharePercentage: number;
}

export interface RefundBreakdown {
  initiatedByRole: string;
  refundCount: number;
  totalAmount: number;
  percentage: number;
}

export interface PaymentLedgerItem {
  paymentId: number;
  orderId: number;
  orderNumber: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  status: string;
  razorpayPaymentId?: string;
  failureReason?: string;
  createdAt: string;
  paidAt?: string;
  refundedAmount: number;
  refundStatus: string;
}

export interface PaymentReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalGrossCollections: number;
  onlineCollections: number;
  counterCashCollections: number;
  totalRefundedAmount: number;
  netSettlementAmount: number;
  totalTransactions: number;
  successfulPaymentsCount: number;
  failedPaymentsCount: number;
  refundsCount: number;
  paymentSuccessRate: number;
  methodsSummary: PaymentMethodLedgerSummary[];
  refundsByRole: RefundBreakdown[];
  payments: ReportPagedResult<PaymentLedgerItem>;
}

export interface LowStockAlertItem {
  ingredientId: number;
  name: string;
  category: string;
  baseUnit: string;
  currentStock: number;
  minimumStockLevel: number;
  reorderLevel: number;
  costPerUnit: number;
  deficitQuantity: number;
}

export interface WastageReasonSummary {
  reason: string;
  incidentCount: number;
  totalQuantityWasted: number;
  totalCost: number;
  percentageOfCost: number;
}

export interface StockMovementSummary {
  transactionType: string;
  transactionCount: number;
  totalQuantityMoved: number;
}

export interface IngredientInventoryItem {
  ingredientId: number;
  name: string;
  category: string;
  baseUnit: string;
  currentStock: number;
  reservedStock: number;
  availableStock: number;
  minimumStockLevel: number;
  costPerBaseUnit: number;
  valuation: number;
  isLowStock: boolean;
  isActive: boolean;
}

export interface InventoryReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalIngredientsTracked: number;
  totalInventoryValuation: number;
  lowStockAlertsCount: number;
  totalWastageCost: number;
  totalWastageQuantity: number;
  totalStockTransactionsCount: number;
  lowStockItems: LowStockAlertItem[];
  wastageByReason: WastageReasonSummary[];
  stockMovementsByType: StockMovementSummary[];
  ingredients: ReportPagedResult<IngredientInventoryItem>;
}

export interface CouponCampaignPerformance {
  couponId: number;
  code: string;
  title: string;
  discountType: string;
  discountValue: number;
  redemptionsCount: number;
  totalDiscountGiven: number;
  totalSalesGenerated: number;
  isActive: boolean;
  startDate: string;
  endDate: string;
}

export interface CouponRedemptionReportItem {
  redemptionId: number;
  couponCode: string;
  orderNumber: string;
  customerIdentifier: string;
  discountAmountApplied: number;
  orderTotal: number;
  redeemedAt: string;
}

export interface CouponReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalActiveCoupons: number;
  totalRedemptionsCount: number;
  totalDiscountGiven: number;
  totalOrderSalesWithDiscount: number;
  averageDiscountPerRedemption: number;
  promotionalRoiRatio: number;
  campaigns: CouponCampaignPerformance[];
  redemptions: ReportPagedResult<CouponRedemptionReportItem>;
}

export interface DailyAttendanceTrend {
  date: string;
  dateLabel: string;
  presentCount: number;
  absentCount: number;
  halfDayCount: number;
  onLeaveCount: number;
}

export interface StaffAttendanceSummary {
  totalAttendanceLogs: number;
  presentCount: number;
  absentCount: number;
  halfDayCount: number;
  onLeaveCount: number;
  attendancePercentage: number;
  dailyTrend: DailyAttendanceTrend[];
}

export interface StaffMemberReportItem {
  staffId: number;
  employeeId: string;
  fullName: string;
  role: string;
  shift: string;
  status: string;
  joinedOn: string;
  salary?: number;
  presentDays: number;
  absentDays: number;
  attendanceRate: number;
}

export interface StaffReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalStaffCount: number;
  activeStaffCount: number;
  onLeaveStaffCount: number;
  inactiveStaffCount: number;
  totalMonthlyPayroll: number;
  overallAttendanceRate: number;
  roleBreakdown: ReportDistribution[];
  shiftBreakdown: ReportDistribution[];
  attendanceSummary: StaffAttendanceSummary;
  staffMembers: ReportPagedResult<StaffMemberReportItem>;
}

export interface AttendanceRecordDetail {
  attendanceId: number;
  staffId: number;
  employeeId: string;
  fullName: string;
  role: string;
  date: string;
  checkInTime?: string;
  checkOutTime?: string;
  status: string;
  notes?: string;
}

export interface StaffAttendanceReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalWorkDays: number;
  totalPresent: number;
  totalAbsent: number;
  totalHalfDay: number;
  totalOnLeave: number;
  overallAttendanceRate: number;
  dailyRecords: DailyAttendanceTrend[];
  logEntries: ReportPagedResult<AttendanceRecordDetail>;
}

export interface HourlyKitchenVelocity {
  hour: number;
  hourLabel: string;
  ordersCount: number;
  avgPrepMinutes: number;
}

export interface KitchenDishThroughput {
  productId: number;
  name: string;
  categoryName: string;
  totalUnitsCooked: number;
  percentageOfTotal: number;
}

export interface KitchenOrderQueueItem {
  orderId: number;
  orderNumber: string;
  tableNumber: string;
  totalItemsCount: number;
  status: string;
  createdAt: string;
  durationMinutes: number;
  isDelayed: boolean;
}

export interface KitchenReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  totalOrdersProcessed: number;
  totalDishesPrepared: number;
  averagePreparationMinutes: number;
  ordersPreparedUnder15Minutes: number;
  delayedOrdersCount: number;
  kitchenEfficiencyScore: number;
  velocityByHour: HourlyKitchenVelocity[];
  topPreparedDishes: KitchenDishThroughput[];
  orderQueue: ReportPagedResult<KitchenOrderQueueItem>;
}

export interface HourlyPeakTrafficStat {
  hour: number;
  hourLabel: string;
  ordersCount: number;
  grossSales: number;
  averageOrderValue: number;
  trafficPercentage: number;
  isPeakHour: boolean;
}

export interface DayOfWeekPeakStat {
  dayOfWeekIndex: number;
  dayName: string;
  ordersCount: number;
  grossSales: number;
  percentageOfTotal: number;
}

export interface TableUtilizationPeak {
  tableId: number;
  tableNumber: string;
  totalOrdersServed: number;
  totalRevenueGenerated: number;
  utilizationPercentage: number;
}

export interface PeakHourReport {
  canteenId: number;
  canteenName: string;
  period: string;
  fromDate: string;
  toDate: string;
  busiestHourLabel: string;
  busiestHourOrdersCount: number;
  busiestHourSales: number;
  busiestDayOfWeek: string;
  averageOrdersPerHour: number;
  peakHourRevenueContribution: number;
  hourlyTraffic: HourlyPeakTrafficStat[];
  dayOfWeekTraffic: DayOfWeekPeakStat[];
  tableUtilization: TableUtilizationPeak[];
}

export interface ExportReportRequest extends ReportFilter {
  reportType: 'overview' | 'sales' | 'orders' | 'products' | 'payments' | 'inventory' | 'coupons' | 'staff' | 'attendance' | 'kitchen' | 'peak-hours' | string;
  format: 'csv' | 'excel';
}

