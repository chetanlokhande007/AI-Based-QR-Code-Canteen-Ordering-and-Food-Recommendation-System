import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminCanteenItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-canteens',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-canteens.html',
  styleUrl: './admin-canteens.css'
})
export class AdminCanteens implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly canteens = signal<AdminCanteenItem[]>([]);
  readonly loading = signal<boolean>(true);
  readonly actionLoading = signal<boolean>(false);
  readonly errorMessage = signal<string>('');
  readonly successMessage = signal<string>('');

  // Search & Filter
  searchTerm = '';
  selectedStatus = 'all'; // 'all' | 'active' | 'suspended'

  // Modals & Confirmation
  readonly selectedCanteenForModal = signal<AdminCanteenItem | null>(null);
  readonly showSuspendModal = signal<boolean>(false);
  readonly canteenToSuspend = signal<AdminCanteenItem | null>(null);

  readonly filteredCanteens = computed(() => {
    let list = this.canteens();
    const query = this.searchTerm.trim().toLowerCase();
    const status = this.selectedStatus;

    if (query) {
      list = list.filter(c =>
        c.name.toLowerCase().includes(query) ||
        c.location.toLowerCase().includes(query) ||
        c.ownerName.toLowerCase().includes(query) ||
        c.ownerEmail.toLowerCase().includes(query)
      );
    }

    if (status === 'active') {
      list = list.filter(c => c.isActive);
    } else if (status === 'suspended') {
      list = list.filter(c => !c.isActive);
    }

    return list;
  });

  ngOnInit(): void {
    this.loadCanteens();
  }

  loadCanteens(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getCanteens().subscribe({
      next: (data) => {
        this.canteens.set(data || []);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 403) {
          this.errorMessage.set('Access Denied. Platform Administrator authorization is required.');
        } else {
          this.errorMessage.set('Failed to load canteens from server.');
        }
      }
    });
  }

  promptSuspend(canteen: AdminCanteenItem): void {
    this.canteenToSuspend.set(canteen);
    this.showSuspendModal.set(true);
  }

  cancelSuspend(): void {
    this.showSuspendModal.set(false);
    this.canteenToSuspend.set(null);
  }

  confirmSuspend(): void {
    const target = this.canteenToSuspend();
    if (!target) return;

    this.actionLoading.set(true);
    this.adminService.updateCanteenStatus(target.canteenId, false).subscribe({
      next: (res) => {
        this.actionLoading.set(false);
        this.showSuspendModal.set(false);
        this.canteenToSuspend.set(null);
        this.successMessage.set(res.message);
        this.loadCanteens();
        setTimeout(() => this.successMessage.set(''), 4000);
      },
      error: () => {
        this.actionLoading.set(false);
        this.errorMessage.set('Failed to suspend canteen. Please try again.');
      }
    });
  }

  activateCanteen(canteen: AdminCanteenItem): void {
    this.actionLoading.set(true);
    this.adminService.updateCanteenStatus(canteen.canteenId, true).subscribe({
      next: (res) => {
        this.actionLoading.set(false);
        this.successMessage.set(res.message);
        this.loadCanteens();
        setTimeout(() => this.successMessage.set(''), 4000);
      },
      error: () => {
        this.actionLoading.set(false);
        this.errorMessage.set('Failed to activate canteen. Please try again.');
      }
    });
  }

  openDetails(canteen: AdminCanteenItem): void {
    this.router.navigate(['/admin/canteens', canteen.canteenId]);
  }

  closeDetails(): void {
    this.selectedCanteenForModal.set(null);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

