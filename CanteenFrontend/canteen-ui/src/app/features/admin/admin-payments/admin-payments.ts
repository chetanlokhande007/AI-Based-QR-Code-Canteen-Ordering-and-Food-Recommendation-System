import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminCanteenPaymentSetup, AdminPaymentOverview, AdminPaymentTransaction } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-payments',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-payments.html',
  styleUrl: './admin-payments.css'
})
export class AdminPaymentsComponent implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');

  readonly paymentOverview = signal<AdminPaymentOverview | null>(null);
  readonly canteensSetup = signal<AdminCanteenPaymentSetup[]>([]);
  readonly activeTab = signal<'transactions' | 'setup'>('transactions');

  // Filters
  selectedCanteenFilter = 'all';
  selectedDateFilter = '30d';
  searchQuery = '';

  // Filtered transactions
  readonly filteredTransactions = computed(() => {
    const list = this.paymentOverview()?.recentTransactions || [];
    const query = this.searchQuery.trim().toLowerCase();
    const canteen = this.selectedCanteenFilter;

    return list.filter(t => {
      const matchCanteen = canteen === 'all' || t.canteenId.toString() === canteen;
      const matchQuery = !query ||
        t.canteenName.toLowerCase().includes(query) ||
        t.orderNumber.toLowerCase().includes(query) ||
        (t.transactionId && t.transactionId.toLowerCase().includes(query));
      return matchCanteen && matchQuery;
    });
  });

  // Filtered setups
  readonly filteredSetups = computed(() => {
    let list = this.canteensSetup();
    const query = this.searchQuery.trim().toLowerCase();
    if (query) {
      list = list.filter(s =>
        s.canteenName.toLowerCase().includes(query) ||
        s.ownerName.toLowerCase().includes(query) ||
        (s.bankName && s.bankName.toLowerCase().includes(query))
      );
    }
    return list;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getPaymentOverview().subscribe({
      next: (data) => {
        this.paymentOverview.set(data);
        this.loadSetups();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to load payment overview.');
      }
    });
  }

  loadSetups(): void {
    this.adminService.getCanteensPaymentSetup().subscribe({
      next: (setups) => {
        this.canteensSetup.set(setups);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  setTab(tab: 'transactions' | 'setup'): void {
    this.activeTab.set(tab);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

