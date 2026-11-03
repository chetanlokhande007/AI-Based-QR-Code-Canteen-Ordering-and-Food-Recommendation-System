import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { CouponsComponent } from './coupons';
import { CouponService } from '../../core/services/coupon.service';
import { AuthService } from '../../core/services/auth.service';
import { CouponDto, CouponRedemptionReport } from '../../core/models/coupon.models';

describe('CouponsComponent', () => {
  let component: CouponsComponent;
  let fixture: ComponentFixture<CouponsComponent>;
  let mockCouponService: any;
  let mockAuthService: any;

  const mockCoupons: CouponDto[] = [
    {
      couponId: 1,
      canteenId: 1,
      code: 'WELCOME20',
      title: 'Welcome 20% Off',
      description: 'First order discount',
      discountType: 'Percentage',
      discountValue: 20,
      minOrderAmount: 100,
      maxDiscountAmount: 50,
      usageLimitPerCustomer: 1,
      totalUsageLimit: 200,
      timesUsed: 15,
      startDate: '2026-01-01T00:00:00Z',
      endDate: '2026-12-31T23:59:59Z',
      isActive: true,
      createdAt: '2026-01-01T00:00:00Z'
    },
    {
      couponId: 2,
      canteenId: 1,
      code: 'FLAT30',
      title: 'Flat ₹30 Off',
      discountType: 'FixedAmount',
      discountValue: 30,
      minOrderAmount: 150,
      usageLimitPerCustomer: 2,
      timesUsed: 40,
      startDate: '2026-01-01T00:00:00Z',
      endDate: '2026-12-31T23:59:59Z',
      isActive: false,
      createdAt: '2026-01-01T00:00:00Z'
    }
  ];

  const mockReport: CouponRedemptionReport = {
    totalCoupons: 2,
    activeCoupons: 1,
    totalRedemptions: 55,
    totalDiscountGiven: 1500,
    totalSalesWithDiscount: 8500
  };

  beforeEach(async () => {
    mockCouponService = {
      getAllCoupons: vi.fn().mockReturnValue(of(mockCoupons)),
      getRedemptionReport: vi.fn().mockReturnValue(of(mockReport)),
      createCoupon: vi.fn().mockReturnValue(of(mockCoupons[0])),
      updateCoupon: vi.fn().mockReturnValue(of(mockCoupons[0])),
      toggleCouponStatus: vi.fn().mockReturnValue(of({ couponId: 1, isActive: false, message: 'Status updated' })),
      deleteCoupon: vi.fn().mockReturnValue(of({ message: 'Deleted' }))
    };

    mockAuthService = {
      currentUser: signal({ canteenId: 1 })
    };

    await TestBed.configureTestingModule({
      imports: [CouponsComponent],
      providers: [
        { provide: CouponService, useValue: mockCouponService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CouponsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component and load coupons on init', () => {
    expect(component).toBeTruthy();
    expect(component.coupons().length).toBe(2);
    expect(component.report()?.totalCoupons).toBe(2);
  });

  it('should filter coupons by search query', () => {
    component.searchQuery.set('WELCOME');
    expect(component.filteredCoupons().length).toBe(1);
    expect(component.filteredCoupons()[0].code).toBe('WELCOME20');

    component.searchQuery.set('NonExistent');
    expect(component.filteredCoupons().length).toBe(0);
  });

  it('should open create modal with default values', () => {
    component.openCreateModal();
    expect(component.isModalOpen()).toBe(true);
    expect(component.editingCouponId()).toBeNull();
    expect(component.formDiscountType).toBe('Percentage');
  });

  it('should toggle coupon active status', () => {
    component.toggleStatus(mockCoupons[0]);
    expect(mockCouponService.toggleCouponStatus).toHaveBeenCalledWith(1, 1);
  });
});
