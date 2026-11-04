import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ReportsComponent } from './reports';
import { ReportsService } from '../../core/services/reports.service';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService } from '../../core/services/signalr.service';
import {
  OverviewReport,
  SalesReport,
  OrderReport,
  ProductReport
} from '../../core/models/reports.models';

describe('ReportsComponent', () => {
  let component: ReportsComponent;
  let fixture: ComponentFixture<ReportsComponent>;

  const mockOverview: OverviewReport = {
    canteenId: 1,
    canteenName: 'Campus Central Canteen',
    period: 'this_month',
    fromDate: '2026-11-01T00:00:00Z',
    toDate: '2026-11-30T23:59:59Z',
    grossRevenue: 52400,
    netRevenue: 50800,
    totalRefunds: 1600,
    totalOrders: 320,
    completedOrders: 300,
    cancelledOrders: 20,
    averageOrderValue: 163.75,
    activeProductsCount: 45,
    grossRevenueGrowthPercent: 12.5,
    netRevenueGrowthPercent: 11.8,
    ordersGrowthPercent: 8.2,
    averageOrderValueGrowthPercent: 3.9,
    revenueTrend: [
      { dateLabel: 'Nov 01', date: '2026-11-01T00:00:00Z', grossSales: 1800, netSales: 1800, orderCount: 12 },
      { dateLabel: 'Nov 02', date: '2026-11-02T00:00:00Z', grossSales: 2200, netSales: 2100, orderCount: 15 }
    ],
    orderStatusBreakdown: [
      { label: 'Completed', count: 300, percentage: 93.8, totalValue: 49000 },
      { label: 'Cancelled', count: 20, percentage: 6.2, totalValue: 3400 }
    ],
    paymentMethodBreakdown: [
      { label: 'Online', count: 240, percentage: 75.0, totalValue: 39300 },
      { label: 'Counter', count: 80, percentage: 25.0, totalValue: 13100 }
    ],
    topSellingProducts: [
      { productId: 1, name: 'Masala Chai', categoryName: 'Beverages', quantitySold: 120, totalSales: 2400 },
      { productId: 2, name: 'Thali Deluxe', categoryName: 'Meals', quantitySold: 95, totalSales: 14250 }
    ]
  };

  const mockSales: SalesReport = {
    canteenId: 1,
    canteenName: 'Campus Central Canteen',
    period: 'this_month',
    fromDate: '2026-11-01T00:00:00Z',
    toDate: '2026-11-30T23:59:59Z',
    grossSales: 52400,
    netSales: 50800,
    subtotalAmount: 48000,
    totalDiscounts: 1200,
    totalTaxCollected: 2400,
    totalPlatformFees: 1600,
    totalCanteenEarnings: 47600,
    totalRefunds: 1600,
    successfulOrdersCount: 300,
    averageOrderValue: 163.75,
    grossSalesGrowthPercent: 12.5,
    netSalesGrowthPercent: 11.8,
    ordersGrowthPercent: 8.2,
    dailyTimeline: [],
    paymentMethodSummary: [],
    categorySales: [],
    transactions: {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false
    }
  };

  const mockReportsService = {
    getOverview: vi.fn(() => of(mockOverview)),
    getSales: vi.fn(() => of(mockSales)),
    getOrders: vi.fn(() => of({} as OrderReport)),
    getProducts: vi.fn(() => of({} as ProductReport)),
    getPayments: vi.fn(() => of({} as any)),
    getInventory: vi.fn(() => of({} as any)),
    getCoupons: vi.fn(() => of({} as any)),
    getStaff: vi.fn(() => of({} as any)),
    getAttendance: vi.fn(() => of({} as any)),
    getKitchen: vi.fn(() => of({} as any)),
    getPeakHours: vi.fn(() => of({} as any)),
    exportReport: vi.fn(() => of(new Blob(['test'], { type: 'text/csv' }))),
    triggerBlobDownload: vi.fn()
  };

  const mockAuthService = {
    currentUser: () => ({
      ownerId: 1,
      fullName: 'Owner User',
      email: 'owner@canteen.com',
      role: 'CanteenOwner',
      canteenId: 1
    })
  };

  const mockSignalRService = {
    startConnection: vi.fn(),
    newOrder$: of(),
    paymentConfirmed$: of(),
    orderStatusUpdated$: of()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ReportsService, useValue: mockReportsService },
        { provide: AuthService, useValue: mockAuthService },
        { provide: SignalRService, useValue: mockSignalRService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ReportsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create ReportsComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with overview tab and this_month period', () => {
    expect(component.activeTab()).toBe('overview');
    expect(component.selectedPeriod()).toBe('this_month');
    expect(mockReportsService.getOverview).toHaveBeenCalled();
  });

  it('should switch tab and load tab data', () => {
    component.selectTab('sales');
    expect(component.activeTab()).toBe('sales');
    expect(mockReportsService.getSales).toHaveBeenCalled();
  });

  it('should change period preset and reload report', () => {
    component.setPeriod('today');
    expect(component.selectedPeriod()).toBe('today');
    expect(mockReportsService.getOverview).toHaveBeenCalled();
  });

  it('should trigger report export for csv', () => {
    component.exportCurrentReport('csv');
    expect(mockReportsService.exportReport).toHaveBeenCalled();
  });
});
