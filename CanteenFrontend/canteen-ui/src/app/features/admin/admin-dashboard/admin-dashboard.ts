import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformAdminMetrics } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
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
        } else {
          this.errorMessage.set('Failed to load platform metrics. Please verify backend connection.');
        }
      }
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

