import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformAdminMetrics, AdminCanteenSettlement } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.css'
})
export class AdminDashboard implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly metrics = signal<PlatformAdminMetrics | null>(null);
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');
  readonly successMessage = signal<string>('');

  // Settlement Payout Modal
  readonly isPayoutModalOpen = signal<boolean>(false);
  readonly selectedSettlement = signal<AdminCanteenSettlement | null>(null);
  readonly payoutAmount = signal<number>(0);
  readonly payoutRefNumber = signal<string>('');
  readonly payoutNotes = signal<string>('');
  readonly isProcessingPayout = signal<boolean>(false);

  ngOnInit(): void {
    this.loadMetrics();
  }

  loadMetrics(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getMetrics().subscribe({
      next: (data) => {
        this.metrics.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 403) {
          this.errorMessage.set('Access Denied. You do not have Platform Administrator authorization.');
        } else if (err.status === 401) {
          this.errorMessage.set('Your session has expired or is unauthorized. Please sign in again.');
        } else {
          this.errorMessage.set(err.error?.message || 'Failed to load platform metrics. Please verify backend connection.');
        }
      }
    });
  }

  getInitials(name?: string | null): string {
    if (!name) return 'PA';
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
  }

  getDonutGradient(): string {
    const dist = this.metrics()?.statusDistribution;
    if (!dist || dist.total === 0) {
      return 'conic-gradient(#E2E8F0 0% 100%)';
    }
    const total = dist.total;
    const activePct = (dist.active / total) * 100;
    const suspendedPct = (dist.suspended / total) * 100;

    const activeDeg = Math.round((activePct / 100) * 360);
    const suspendedDeg = Math.round((suspendedPct / 100) * 360);

    if (dist.suspended === 0 && dist.setupInProgress === 0) {
      return 'conic-gradient(#059669 0deg 360deg)';
    }

    return `conic-gradient(#059669 0deg ${activeDeg}deg, #E11D48 ${activeDeg}deg ${activeDeg + suspendedDeg}deg, #D97706 ${activeDeg + suspendedDeg}deg 360deg)`;
  }

  getMaxRevenue(): number {
    const trend = this.metrics()?.monthlyRevenueTrend;
    if (!trend || trend.length === 0) return 50;
    const max = Math.max(...trend.map(t => t.platformRevenue));
    return max > 0 ? max : 50;
  }

  getRevenueBarHeight(val: number): number {
    if (val <= 0) return 6;
    const max = this.getMaxRevenue();
    const pct = Math.round((val / max) * 80) + 16;
    return Math.min(pct, 100);
  }

  openPayoutModal(item: AdminCanteenSettlement): void {
    this.selectedSettlement.set(item);
    this.payoutAmount.set(item.pendingPayableBalance);
    this.payoutRefNumber.set(`UTR${Date.now().toString().slice(-8)}`);
    this.payoutNotes.set(`Direct bank transfer to ${item.bankName || 'configured account'}`);
    this.isPayoutModalOpen.set(true);
  }

  closePayoutModal(): void {
    this.isPayoutModalOpen.set(false);
    this.selectedSettlement.set(null);
  }

  submitPayout(): void {
    const item = this.selectedSettlement();
    if (!item) return;

    this.isProcessingPayout.set(true);
    this.adminService.recordPayout(item.canteenId, {
      amount: this.payoutAmount(),
      referenceNumber: this.payoutRefNumber(),
      notes: this.payoutNotes()
    }).subscribe({
      next: (res) => {
        this.isProcessingPayout.set(false);
        this.closePayoutModal();
        this.successMessage.set(res.message || `Payout of ₹${this.payoutAmount()} recorded successfully.`);
        setTimeout(() => this.successMessage.set(''), 5000);
        this.loadMetrics();
      },
      error: (err) => {
        this.isProcessingPayout.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to record settlement payout.');
      }
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
