import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminAuditLog, AdminCanteenItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-audit-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-audit-logs.html',
  styleUrl: './admin-audit-logs.css'
})
export class AdminAuditLogsComponent implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');

  readonly logs = signal<AdminAuditLog[]>([]);
  readonly canteens = signal<AdminCanteenItem[]>([]);

  selectedCanteen = 'all';
  selectedPeriod = '30d';
  searchQuery = '';

  readonly filteredLogs = computed(() => {
    let list = this.logs();
    const query = this.searchQuery.trim().toLowerCase();
    const canteen = this.selectedCanteen;

    if (canteen !== 'all') {
      list = list.filter(l => l.details.toLowerCase().includes(canteen.toLowerCase()));
    }

    if (query) {
      list = list.filter(l =>
        l.action.toLowerCase().includes(query) ||
        l.user.toLowerCase().includes(query) ||
        l.details.toLowerCase().includes(query) ||
        l.ipAddress.includes(query)
      );
    }

    return list;
  });

  ngOnInit(): void {
    this.loadLogs();
    this.loadCanteens();
  }

  loadLogs(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getAuditLogs().subscribe({
      next: (data) => {
        this.logs.set(data || []);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to load audit logs.');
      }
    });
  }

  loadCanteens(): void {
    this.adminService.getCanteens().subscribe({
      next: (list) => {
        this.canteens.set(list || []);
      }
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

