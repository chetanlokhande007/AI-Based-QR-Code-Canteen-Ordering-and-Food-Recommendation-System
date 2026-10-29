import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminCanteenDetails } from '../../../core/models/admin.models';
import { CanteenTableDto } from '../../../core/models/table.models';

@Component({
  selector: 'app-admin-canteen-details',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-canteen-details.html',
  styleUrl: './admin-canteen-details.css'
})
export class AdminCanteenDetailsComponent implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly canteenId = signal<number>(0);
  readonly details = signal<AdminCanteenDetails | null>(null);
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');
  readonly activeTab = signal<'overview' | 'owner' | 'tables' | 'food-items' | 'orders' | 'payments' | 'activity'>('overview');

  // QR Modal inspection
  readonly showQrModal = signal<boolean>(false);
  readonly selectedQrTable = signal<CanteenTableDto | null>(null);

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      const id = Number(idParam);
      this.canteenId.set(id);
      this.loadDetails(id);
    } else {
      this.errorMessage.set('Invalid canteen ID specified.');
      this.loading.set(false);
    }
  }

  loadDetails(id: number): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getCanteenDetails(id).subscribe({
      next: (data) => {
        this.details.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to load canteen details.');
      }
    });
  }

  setTab(tab: 'overview' | 'owner' | 'tables' | 'food-items' | 'orders' | 'payments' | 'activity'): void {
    this.activeTab.set(tab);
  }

  openQrModal(table: CanteenTableDto): void {
    this.selectedQrTable.set(table);
    this.showQrModal.set(true);
  }

  closeQrModal(): void {
    this.showQrModal.set(false);
    this.selectedQrTable.set(null);
  }

  getQrImageUrl(tableId: number): string {
    return this.adminService.getAdminQrImageUrl(this.canteenId(), tableId);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

