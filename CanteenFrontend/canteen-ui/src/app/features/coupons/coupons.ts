import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CouponService } from '../../core/services/coupon.service';
import { AuthService } from '../../core/services/auth.service';
import {
  CouponDto,
  CreateCouponDto,
  UpdateCouponDto,
  CouponRedemptionReport
} from '../../core/models/coupon.models';

@Component({
  selector: 'app-coupons',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coupons.html',
  styleUrl: './coupons.css'
})
export class CouponsComponent implements OnInit {
  private readonly couponService = inject(CouponService);
  private readonly authService = inject(AuthService);

  readonly coupons = signal<CouponDto[]>([]);
  readonly report = signal<CouponRedemptionReport | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Filter & Search State
  readonly filterStatus = signal<'all' | 'active' | 'expired'>('all');
  readonly searchQuery = signal<string>('');

  // Modal State
  readonly isModalOpen = signal<boolean>(false);
  readonly editingCouponId = signal<number | null>(null);

  // Form State
  formCode = '';
  formTitle = '';
  formDescription = '';
  formDiscountType: 'Percentage' | 'FixedAmount' = 'Percentage';
  formDiscountValue = 10;
  formMinOrderAmount = 0;
  formMaxDiscountAmount: number | null = null;
  formUsageLimitPerCustomer = 1;
  formTotalUsageLimit: number | null = null;
  formStartDate = '';
  formEndDate = '';
  formIsActive = true;

  readonly currentUser = this.authService.currentUser;

  readonly effectiveCanteenId = computed(() => {
    return this.currentUser()?.canteenId ?? 1;
  });

  readonly filteredCoupons = computed(() => {
    let list = this.coupons();
    const query = this.searchQuery().trim().toLowerCase();
    const status = this.filterStatus();
    const now = new Date().toISOString();

    if (status === 'active') {
      list = list.filter((c) => c.isActive && c.endDate >= now);
    } else if (status === 'expired') {
      list = list.filter((c) => c.endDate < now);
    }

    if (query) {
      list = list.filter(
        (c) =>
          c.code.toLowerCase().includes(query) ||
          c.title.toLowerCase().includes(query) ||
          (c.description && c.description.toLowerCase().includes(query))
      );
    }

    return list;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const cId = this.effectiveCanteenId();
    if (!cId) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.couponService.getAllCoupons(cId).subscribe({
      next: (data) => {
        this.coupons.set(data || []);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load coupons:', err);
        this.errorMessage.set('Could not load coupons. Please try again.');
        this.loading.set(false);
      }
    });

    this.couponService.getRedemptionReport(cId).subscribe({
      next: (rep) => this.report.set(rep),
      error: (err) => console.warn('Failed to load redemption report:', err)
    });
  }

  openCreateModal(): void {
    this.editingCouponId.set(null);
    this.formCode = '';
    this.formTitle = '';
    this.formDescription = '';
    this.formDiscountType = 'Percentage';
    this.formDiscountValue = 15;
    this.formMinOrderAmount = 100;
    this.formMaxDiscountAmount = 50;
    this.formUsageLimitPerCustomer = 1;
    this.formTotalUsageLimit = 200;

    const today = new Date();
    const in30Days = new Date();
    in30Days.setDate(today.getDate() + 30);

    this.formStartDate = today.toISOString().split('T')[0];
    this.formEndDate = in30Days.toISOString().split('T')[0];
    this.formIsActive = true;

    this.isModalOpen.set(true);
  }

  openEditModal(coupon: CouponDto): void {
    this.editingCouponId.set(coupon.couponId);
    this.formCode = coupon.code;
    this.formTitle = coupon.title;
    this.formDescription = coupon.description || '';
    this.formDiscountType = coupon.discountType;
    this.formDiscountValue = coupon.discountValue;
    this.formMinOrderAmount = coupon.minOrderAmount;
    this.formMaxDiscountAmount = coupon.maxDiscountAmount || null;
    this.formUsageLimitPerCustomer = coupon.usageLimitPerCustomer;
    this.formTotalUsageLimit = coupon.totalUsageLimit || null;
    this.formStartDate = coupon.startDate ? coupon.startDate.split('T')[0] : '';
    this.formEndDate = coupon.endDate ? coupon.endDate.split('T')[0] : '';
    this.formIsActive = coupon.isActive;

    this.isModalOpen.set(true);
  }

  closeModal(): void {
    this.isModalOpen.set(false);
    this.editingCouponId.set(null);
  }

  saveCoupon(): void {
    const cId = this.effectiveCanteenId();
    if (!cId) return;

    if (!this.formTitle.trim()) {
      alert('Please enter an offer title.');
      return;
    }

    if (!this.editingCouponId() && !this.formCode.trim()) {
      alert('Please enter a coupon code.');
      return;
    }

    if (this.formDiscountValue <= 0) {
      alert('Discount value must be greater than 0.');
      return;
    }

    if (!this.formEndDate) {
      alert('Please specify an expiry date.');
      return;
    }

    this.saving.set(true);

    if (this.editingCouponId()) {
      const updateDto: UpdateCouponDto = {
        title: this.formTitle.trim(),
        description: this.formDescription.trim() || undefined,
        discountType: this.formDiscountType,
        discountValue: Number(this.formDiscountValue),
        minOrderAmount: Number(this.formMinOrderAmount || 0),
        maxDiscountAmount: this.formMaxDiscountAmount ? Number(this.formMaxDiscountAmount) : undefined,
        usageLimitPerCustomer: Number(this.formUsageLimitPerCustomer || 1),
        totalUsageLimit: this.formTotalUsageLimit ? Number(this.formTotalUsageLimit) : undefined,
        startDate: new Date(this.formStartDate || new Date()).toISOString(),
        endDate: new Date(this.formEndDate + 'T23:59:59Z').toISOString(),
        isActive: this.formIsActive
      };

      this.couponService.updateCoupon(cId, this.editingCouponId()!, updateDto).subscribe({
        next: () => {
          this.saving.set(false);
          this.closeModal();
          this.showSuccess('Coupon updated successfully!');
          this.loadData();
        },
        error: (err) => {
          this.saving.set(false);
          const msg = err?.error?.message || 'Could not update coupon.';
          alert(msg);
        }
      });
    } else {
      const createDto: CreateCouponDto = {
        code: this.formCode.trim().toUpperCase(),
        title: this.formTitle.trim(),
        description: this.formDescription.trim() || undefined,
        discountType: this.formDiscountType,
        discountValue: Number(this.formDiscountValue),
        minOrderAmount: Number(this.formMinOrderAmount || 0),
        maxDiscountAmount: this.formMaxDiscountAmount ? Number(this.formMaxDiscountAmount) : undefined,
        usageLimitPerCustomer: Number(this.formUsageLimitPerCustomer || 1),
        totalUsageLimit: this.formTotalUsageLimit ? Number(this.formTotalUsageLimit) : undefined,
        startDate: new Date(this.formStartDate || new Date()).toISOString(),
        endDate: new Date(this.formEndDate + 'T23:59:59Z').toISOString(),
        isActive: this.formIsActive
      };

      this.couponService.createCoupon(cId, createDto).subscribe({
        next: () => {
          this.saving.set(false);
          this.closeModal();
          this.showSuccess('Coupon created successfully!');
          this.loadData();
        },
        error: (err) => {
          this.saving.set(false);
          const msg = err?.error?.message || 'Could not create coupon.';
          alert(msg);
        }
      });
    }
  }

  toggleStatus(coupon: CouponDto): void {
    const cId = this.effectiveCanteenId();
    if (!cId) return;

    this.couponService.toggleCouponStatus(cId, coupon.couponId).subscribe({
      next: (res) => {
        this.coupons.update((list) =>
          list.map((c) => (c.couponId === coupon.couponId ? { ...c, isActive: res.isActive } : c))
        );
        this.showSuccess(res.message);
      },
      error: () => alert('Could not change coupon status.')
    });
  }

  deleteCoupon(coupon: CouponDto): void {
    const cId = this.effectiveCanteenId();
    if (!cId) return;

    if (!confirm(`Are you sure you want to delete coupon '${coupon.code}'?`)) {
      return;
    }

    this.couponService.deleteCoupon(cId, coupon.couponId).subscribe({
      next: () => {
        this.coupons.update((list) => list.filter((c) => c.couponId !== coupon.couponId));
        this.showSuccess(`Coupon '${coupon.code}' deleted.`);
      },
      error: () => alert('Could not delete coupon.')
    });
  }

  copyCode(code: string): void {
    navigator.clipboard.writeText(code).then(() => {
      this.showSuccess(`Coupon code '${code}' copied to clipboard!`);
    });
  }

  private showSuccess(msg: string): void {
    this.successMessage.set(msg);
    setTimeout(() => this.successMessage.set(null), 3500);
  }
}
