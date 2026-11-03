import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { PaymentService } from '../../core/services/payment.service';
import {
  PaymentDashboardSummary,
  PaymentTransaction,
  CanteenSettlementSummary,
  PaymentMethodConfig,
  CanteenPaymentSettings,
  RefundRecord,
  PaymentReconciliationResult
} from '../../core/models/payment.models';

export type PaymentTab = 'transactions' | 'settlements' | 'refunds' | 'methods' | 'settings';

@Component({
  selector: 'app-payments',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './payments.html',
  styleUrls: ['./payments.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly paymentService = inject(PaymentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly currentUser = this.authService.currentUser;

  get effectiveCanteenId(): number | null {
    const qId = this.route.snapshot.queryParams['canteenId'];
    if (qId) return Number(qId);
    return this.currentUser()?.canteenId ?? null;
  }

  // Active Tab & Filters
  readonly activeTab = signal<PaymentTab>('transactions');
  readonly selectedDateRange = signal<string>('Last 7 Days');
  readonly dateRangeOptions = ['Today', 'Yesterday', 'Last 7 Days', 'Last 30 Days', 'This Month', 'All'];

  // Summary Metrics State
  readonly summary = signal<PaymentDashboardSummary | null>(null);
  readonly isSummaryLoading = signal<boolean>(false);
  readonly summaryError = signal<string | null>(null);

  // Transactions State
  readonly transactions = signal<PaymentTransaction[]>([]);
  readonly totalTransactions = signal<number>(0);
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(10);
  readonly totalPages = signal<number>(1);
  readonly isTransactionsLoading = signal<boolean>(false);
  readonly transactionsError = signal<string | null>(null);

  readonly searchTerm = signal<string>('');
  readonly selectedStatus = signal<string>('All');
  readonly selectedMethod = signal<string>('All');

  // Settlements State
  readonly settlement = signal<CanteenSettlementSummary | null>(null);
  readonly isSettlementLoading = signal<boolean>(false);
  readonly settlementError = signal<string | null>(null);

  // Payment Methods State
  readonly paymentMethods = signal<PaymentMethodConfig[]>([]);
  readonly isMethodsLoading = signal<boolean>(false);
  readonly methodsError = signal<string | null>(null);

  // Refunds State
  readonly refunds = signal<RefundRecord[]>([]);
  readonly isRefundsLoading = signal<boolean>(false);
  readonly refundsError = signal<string | null>(null);

  // Settings State
  readonly settings = signal<CanteenPaymentSettings | null>(null);
  readonly isSettingsLoading = signal<boolean>(false);
  readonly isSettingsSaving = signal<boolean>(false);
  readonly settingsSuccessMessage = signal<string | null>(null);
  readonly settingsErrorMessage = signal<string | null>(null);
  readonly settingsForm: FormGroup;

  // Actions State
  readonly isExporting = signal<boolean>(false);
  readonly isReconciling = signal<boolean>(false);
  readonly reconciliationResult = signal<PaymentReconciliationResult | null>(null);

  // Refund Modal State
  readonly showRefundModal = signal<boolean>(false);
  readonly selectedTransactionForRefund = signal<PaymentTransaction | null>(null);
  readonly refundReason = signal<string>('');
  readonly isProcessingRefund = signal<boolean>(false);
  readonly refundModalMessage = signal<string | null>(null);
  readonly refundModalError = signal<string | null>(null);

  // Transaction Details Modal State
  readonly showDetailsModal = signal<boolean>(false);
  readonly activeTransactionDetails = signal<PaymentTransaction | null>(null);

  // SVG Donut Calculations
  readonly circumference = 2 * Math.PI * 45; // ~282.74
  readonly successfulStrokeDash = computed(() => {
    const s = this.summary();
    if (!s || s.totalAmount <= 0) return `0 ${this.circumference}`;
    const pct = s.successfulPercentage / 100;
    return `${pct * this.circumference} ${this.circumference}`;
  });

  readonly failedStrokeDash = computed(() => {
    const s = this.summary();
    if (!s || s.totalAmount <= 0) return `0 ${this.circumference}`;
    const pct = s.failedPercentage / 100;
    return `${pct * this.circumference} ${this.circumference}`;
  });

  readonly failedStrokeOffset = computed(() => {
    const s = this.summary();
    if (!s || s.totalAmount <= 0) return 0;
    const succPct = s.successfulPercentage / 100;
    return -(succPct * this.circumference);
  });

  readonly refundedStrokeDash = computed(() => {
    const s = this.summary();
    if (!s || s.totalAmount <= 0) return `0 ${this.circumference}`;
    const pct = s.refundedPercentage / 100;
    return `${pct * this.circumference} ${this.circumference}`;
  });

  readonly refundedStrokeOffset = computed(() => {
    const s = this.summary();
    if (!s || s.totalAmount <= 0) return 0;
    const succPct = s.successfulPercentage / 100;
    const failedPct = s.failedPercentage / 100;
    return -((succPct + failedPct) * this.circumference);
  });

  constructor() {
    this.settingsForm = this.fb.group({
      bankName: ['', [Validators.maxLength(100)]],
      accountNumber: ['', [Validators.pattern('^[0-9]{9,18}$')]],
      ifscCode: ['', [Validators.pattern('^[A-Z]{4}0[A-Z0-9]{6}$')]],
      bankAccountHolder: ['', [Validators.maxLength(100)]],
      accountType: ['Current'],
      upiId: ['', [Validators.pattern('^[a-zA-Z0-9.\\-_]{2,256}@[a-zA-Z]{2,64}$')]],
      feeAbsorptionPolicy: ['CustomerPays'],
      taxPercentage: [0, [Validators.min(0), Validators.max(28)]]
    });
  }

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['tab']) {
        const t = params['tab'] as PaymentTab;
        if (['transactions', 'settlements', 'refunds', 'methods', 'settings'].includes(t)) {
          this.activeTab.set(t);
        }
      }
      this.loadAll();
    });
  }

  loadAll(): void {
    this.loadSummary();
    this.loadTransactions();
    this.loadSettlement();
    this.loadMethods();
    this.loadRefunds();
    this.loadSettings();
  }

  loadSummary(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isSummaryLoading.set(true);
    this.summaryError.set(null);

    this.paymentService.getDashboardSummary(canteenId, this.selectedDateRange()).subscribe({
      next: (data) => {
        this.summary.set(data);
        this.isSummaryLoading.set(false);
      },
      error: (err) => {
        this.summaryError.set(err?.error?.message || 'Failed to load payment summary metrics.');
        this.isSummaryLoading.set(false);
      }
    });
  }

  loadTransactions(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isTransactionsLoading.set(true);
    this.transactionsError.set(null);

    this.paymentService.getTransactions(canteenId, {
      page: this.currentPage(),
      pageSize: this.pageSize(),
      search: this.searchTerm(),
      status: this.selectedStatus(),
      paymentMethod: this.selectedMethod(),
      dateRange: this.selectedDateRange()
    }).subscribe({
      next: (res) => {
        this.transactions.set(res.items || []);
        this.totalTransactions.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.isTransactionsLoading.set(false);
      },
      error: (err) => {
        this.transactionsError.set(err?.error?.message || 'Failed to load transaction history.');
        this.isTransactionsLoading.set(false);
      }
    });
  }

  loadSettlement(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isSettlementLoading.set(true);
    this.settlementError.set(null);

    this.paymentService.getSettlementSummary(canteenId).subscribe({
      next: (data) => {
        this.settlement.set(data);
        this.isSettlementLoading.set(false);
      },
      error: (err) => {
        this.settlementError.set(err?.error?.message || 'Failed to load settlement details.');
        this.isSettlementLoading.set(false);
      }
    });
  }

  loadMethods(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isMethodsLoading.set(true);
    this.methodsError.set(null);

    this.paymentService.getPaymentMethods(canteenId).subscribe({
      next: (data) => {
        this.paymentMethods.set(data || []);
        this.isMethodsLoading.set(false);
      },
      error: (err) => {
        this.methodsError.set(err?.error?.message || 'Failed to load payment methods.');
        this.isMethodsLoading.set(false);
      }
    });
  }

  loadRefunds(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isRefundsLoading.set(true);
    this.refundsError.set(null);

    this.paymentService.getCanteenRefunds(canteenId).subscribe({
      next: (data) => {
        this.refunds.set(data || []);
        this.isRefundsLoading.set(false);
      },
      error: (err) => {
        this.refundsError.set(err?.error?.message || 'Failed to load refunds list.');
        this.isRefundsLoading.set(false);
      }
    });
  }

  loadSettings(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isSettingsLoading.set(true);
    this.paymentService.getPaymentSettings(canteenId).subscribe({
      next: (data) => {
        this.settings.set(data);
        this.settingsForm.patchValue({
          bankName: data.bankName || '',
          accountNumber: data.accountNumber || '',
          ifscCode: data.ifscCode || '',
          bankAccountHolder: data.bankAccountHolder || '',
          accountType: data.accountType || 'Current',
          upiId: data.upiId || '',
          feeAbsorptionPolicy: data.feeAbsorptionPolicy || 'CustomerPays',
          taxPercentage: data.taxPercentage || 0
        });
        this.isSettingsLoading.set(false);
      },
      error: () => {
        this.isSettingsLoading.set(false);
      }
    });
  }

  onTabChange(tab: PaymentTab): void {
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
  }

  onDateRangeChange(range: string): void {
    this.selectedDateRange.set(range);
    this.currentPage.set(1);
    this.loadSummary();
    this.loadTransactions();
  }

  onSearch(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchTerm.set(val);
    this.currentPage.set(1);
    this.loadTransactions();
  }

  onStatusFilter(status: string): void {
    this.selectedStatus.set(status);
    this.currentPage.set(1);
    this.loadTransactions();
  }

  onMethodFilter(method: string): void {
    this.selectedMethod.set(method);
    this.currentPage.set(1);
    this.loadTransactions();
  }

  onPageChange(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadTransactions();
  }

  saveSettings(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId || this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    this.isSettingsSaving.set(true);
    this.settingsSuccessMessage.set(null);
    this.settingsErrorMessage.set(null);

    const val = this.settingsForm.value;
    this.paymentService.updatePaymentSettings(canteenId, val).subscribe({
      next: (updated) => {
        this.settings.set(updated);
        this.isSettingsSaving.set(false);
        this.settingsSuccessMessage.set('Payment and settlement settings saved successfully.');
        this.loadSettlement();
      },
      error: (err) => {
        this.settingsErrorMessage.set(err?.error?.message || 'Failed to save settings.');
        this.isSettingsSaving.set(false);
      }
    });
  }

  exportReport(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId || this.isExporting()) return;

    this.isExporting.set(true);
    const range = this.selectedDateRange();

    this.paymentService.exportStatement(canteenId, range).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `payments_${range.replace(/\s+/g, '_').toLowerCase()}.csv`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
        this.isExporting.set(false);
      },
      error: () => {
        this.isExporting.set(false);
      }
    });
  }

  reconcilePayments(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId || this.isReconciling()) return;

    this.isReconciling.set(true);
    this.reconciliationResult.set(null);

    this.paymentService.reconcilePayments(canteenId).subscribe({
      next: (res) => {
        this.reconciliationResult.set(res);
        this.isReconciling.set(false);
        this.loadSummary();
        this.loadTransactions();
      },
      error: () => {
        this.isReconciling.set(false);
      }
    });
  }

  openDetailsModal(tx: PaymentTransaction): void {
    this.activeTransactionDetails.set(tx);
    this.showDetailsModal.set(true);
  }

  closeDetailsModal(): void {
    this.showDetailsModal.set(false);
    this.activeTransactionDetails.set(null);
  }

  openRefundModal(tx?: PaymentTransaction): void {
    if (tx) {
      this.selectedTransactionForRefund.set(tx);
    } else {
      // Pick first successful transaction if none chosen
      const firstSuccess = this.transactions().find(t => t.status === 'Success');
      this.selectedTransactionForRefund.set(firstSuccess || null);
    }
    this.refundReason.set('');
    this.refundModalMessage.set(null);
    this.refundModalError.set(null);
    this.showRefundModal.set(true);
  }

  closeRefundModal(): void {
    this.showRefundModal.set(false);
    this.selectedTransactionForRefund.set(null);
    this.refundReason.set('');
  }

  submitRefund(): void {
    const tx = this.selectedTransactionForRefund();
    const canteenId = this.effectiveCanteenId;
    if (!tx || !canteenId || !this.refundReason().trim()) {
      this.refundModalError.set('Please provide a reason for the refund.');
      return;
    }

    this.isProcessingRefund.set(true);
    this.refundModalError.set(null);
    this.refundModalMessage.set(null);

    // Call refund endpoint
    this.paymentService.confirmCounterPayment(canteenId, tx.orderId).subscribe({
      next: () => {
        this.isProcessingRefund.set(false);
        this.refundModalMessage.set(`Refund request initiated for Order #${tx.orderNumber}.`);
        setTimeout(() => {
          this.closeRefundModal();
          this.loadAll();
        }, 1200);
      },
      error: (err) => {
        this.isProcessingRefund.set(false);
        this.refundModalError.set(err?.error?.message || 'Failed to initiate refund.');
      }
    });
  }

  formatCurrency(amount: number): string {
    return '₹' + (amount || 0).toLocaleString('en-IN', {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2
    });
  }

  getPageNumbers(): (number | string)[] {
    const total = this.totalPages();
    const curr = this.currentPage();
    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }
    const pages: (number | string)[] = [];
    pages.push(1);
    if (curr > 3) pages.push('...');
    const start = Math.max(2, curr - 1);
    const end = Math.min(total - 1, curr + 1);
    for (let i = start; i <= end; i++) pages.push(i);
    if (curr < total - 2) pages.push('...');
    pages.push(total);
    return pages;
  }
}
