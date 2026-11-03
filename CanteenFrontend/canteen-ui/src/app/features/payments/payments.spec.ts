import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PaymentsComponent } from './payments';
import { PaymentService } from '../../core/services/payment.service';
import { AuthService } from '../../core/services/auth.service';
import {
  PaymentDashboardSummary,
  PaymentTransaction,
  PaymentPagedResult,
  CanteenSettlementSummary,
  PaymentMethodConfig,
  CanteenPaymentSettings,
  RefundRecord,
  PaymentReconciliationResult
} from '../../core/models/payment.models';

describe('PaymentsComponent', () => {
  let component: PaymentsComponent;
  let fixture: ComponentFixture<PaymentsComponent>;

  const mockSummary: PaymentDashboardSummary = {
    totalAmount: 24860,
    totalCount: 428,
    successfulAmount: 24260,
    successfulCount: 415,
    successfulPercentage: 97.6,
    failedAmount: 480,
    failedCount: 9,
    failedPercentage: 1.9,
    refundedAmount: 120,
    refundedCount: 4,
    refundedPercentage: 0.5,
    comparisonPercentage: 12.4,
    comparisonLabel: 'vs previous 7 days',
    period: 'Last 7 Days'
  };

  const mockTransactions: PaymentTransaction[] = [
    {
      paymentId: 101,
      orderId: 1058,
      orderNumber: '#ORD-1058',
      transactionId: 'pay_Qk7a2s9182',
      customerName: 'Amit Patil',
      customerInitials: 'AP',
      amount: 180,
      currency: 'INR',
      paymentMethod: 'Razorpay',
      status: 'Success',
      createdAt: '2026-11-03T12:41:00Z'
    },
    {
      paymentId: 102,
      orderId: 1055,
      orderNumber: '#ORD-1055',
      transactionId: 'pay_Qk6z9m1234',
      customerName: 'Pooja Raut',
      customerInitials: 'PR',
      amount: 90,
      currency: 'INR',
      paymentMethod: 'Razorpay',
      status: 'Failed',
      failureReason: 'Customer dropped off before completing OTP',
      createdAt: '2026-11-03T11:56:00Z'
    }
  ];

  const mockPagedResult: PaymentPagedResult<PaymentTransaction> = {
    items: mockTransactions,
    totalCount: 2,
    page: 1,
    pageSize: 10,
    totalPages: 1
  };

  const mockSettlement: CanteenSettlementSummary = {
    availableForSettlement: 22840,
    grossSales: 24260,
    platformFeesDeducted: 1300,
    totalRefundsDeducted: 120,
    netSettlementAmount: 22840,
    settlementStatus: 'Ready for Payout',
    bankName: 'HDFC Bank',
    accountNumberMasked: '••••1234',
    ifscCode: 'HDFC0001234',
    accountHolder: 'CT Catten Foods',
    accountType: 'Current',
    upiId: 'ctcatten@hdfcbank',
    isBankConfigured: true,
    nextSettlementDate: '2026-11-04T00:00:00Z',
    payoutSchedule: 'Daily (Auto T+1)'
  };

  const mockMethods: PaymentMethodConfig[] = [
    { id: 'razorpay', name: 'Razorpay', subtitle: 'Online payments', provider: 'Razorpay', isActive: true, badgeText: 'Active' },
    { id: 'upi', name: 'UPI', subtitle: 'GPay, PhonePe, Paytm etc.', provider: 'NPCI', isActive: true, badgeText: 'Active' },
    { id: 'cards', name: 'Cards', subtitle: 'Credit & Debit Cards', provider: 'Card Networks', isActive: true, badgeText: 'Active' }
  ];

  const mockSettings: CanteenPaymentSettings = {
    canteenId: 1,
    bankName: 'HDFC Bank',
    accountNumber: '123456789012',
    ifscCode: 'HDFC0001234',
    bankAccountHolder: 'CT Catten Foods',
    accountType: 'Current',
    upiId: 'ctcatten@hdfcbank',
    feeAbsorptionPolicy: 'CustomerPays',
    taxPercentage: 5.0
  };

  const mockRefunds: RefundRecord[] = [
    {
      refundId: 1,
      orderId: 1050,
      orderNumber: '#ORD-1050',
      paymentId: 98,
      canteenId: 1,
      amount: 120,
      currency: 'INR',
      refundMethod: 'Gateway',
      status: 'Succeeded',
      reason: 'Dish out of stock',
      initiatedByRole: 'CanteenOwner',
      createdAt: '2026-11-02T10:00:00Z'
    }
  ];

  const mockReconciliation: PaymentReconciliationResult = {
    totalChecked: 2,
    reconciled: 1,
    discrepanciesFound: 1,
    message: 'Reconciliation complete. 1 pending payment resolved.',
    reconciledAt: '2026-11-03T12:00:00Z'
  };

  const mockPaymentService = {
    getDashboardSummary: vi.fn(() => of(mockSummary)),
    getTransactions: vi.fn(() => of(mockPagedResult)),
    getSettlementSummary: vi.fn(() => of(mockSettlement)),
    getPaymentMethods: vi.fn(() => of(mockMethods)),
    getCanteenRefunds: vi.fn(() => of(mockRefunds)),
    getPaymentSettings: vi.fn(() => of(mockSettings)),
    updatePaymentSettings: vi.fn(() => of(mockSettings)),
    exportStatement: vi.fn(() => of(new Blob(['Payment ID,Order'], { type: 'text/csv' }))),
    reconcilePayments: vi.fn(() => of(mockReconciliation)),
    confirmCounterPayment: vi.fn(() => of({} as any))
  };

  const mockAuthService = {
    currentUser: () => ({
      ownerId: 5,
      fullName: 'Tanisha Rajendra Mahajan',
      email: 'owner@canteen.com',
      role: 'CanteenOwner',
      canteenId: 1
    })
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: PaymentService, useValue: mockPaymentService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PaymentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component and load dashboard summary and transactions', () => {
    expect(component).toBeTruthy();
    expect(mockPaymentService.getDashboardSummary).toHaveBeenCalled();
    expect(mockPaymentService.getTransactions).toHaveBeenCalled();
    expect(component.summary()?.totalAmount).toBe(24860);
    expect(component.transactions().length).toBe(2);
  });

  it('should switch active tabs correctly', () => {
    component.onTabChange('settlements');
    expect(component.activeTab()).toBe('settlements');

    component.onTabChange('refunds');
    expect(component.activeTab()).toBe('refunds');

    component.onTabChange('settings');
    expect(component.activeTab()).toBe('settings');
  });

  it('should update date range and reload metrics', () => {
    component.onDateRangeChange('Today');
    expect(component.selectedDateRange()).toBe('Today');
    expect(mockPaymentService.getDashboardSummary).toHaveBeenCalledWith(1, 'Today');
  });

  it('should trigger search filter and reset page to 1', () => {
    component.currentPage.set(3);
    const event = { target: { value: 'ORD-1058' } } as unknown as Event;
    component.onSearch(event);
    expect(component.searchTerm()).toBe('ORD-1058');
    expect(component.currentPage()).toBe(1);
  });

  it('should open and close transaction details modal', () => {
    component.openDetailsModal(mockTransactions[0]);
    expect(component.showDetailsModal()).toBe(true);
    expect(component.activeTransactionDetails()?.orderNumber).toBe('#ORD-1058');

    component.closeDetailsModal();
    expect(component.showDetailsModal()).toBe(false);
    expect(component.activeTransactionDetails()).toBeNull();
  });

  it('should open and close refund modal', () => {
    component.openRefundModal(mockTransactions[0]);
    expect(component.showRefundModal()).toBe(true);
    expect(component.selectedTransactionForRefund()?.orderNumber).toBe('#ORD-1058');

    component.closeRefundModal();
    expect(component.showRefundModal()).toBe(false);
  });

  it('should trigger reconcile payments action and set result message', () => {
    component.reconcilePayments();
    expect(mockPaymentService.reconcilePayments).toHaveBeenCalledWith(1);
    expect(component.reconciliationResult()?.reconciled).toBe(1);
  });

  it('should trigger exportReport and invoke paymentService.exportStatement', () => {
    component.exportReport();
    expect(mockPaymentService.exportStatement).toHaveBeenCalledWith(1, 'Last 7 Days');
  });

  it('should format Indian currency accurately', () => {
    expect(component.formatCurrency(24860)).toBe('₹24,860');
    expect(component.formatCurrency(0)).toBe('₹0');
  });
});

