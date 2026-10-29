import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminOwnerDetails, AdminOwnerItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-owners',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-owners.html',
  styleUrl: './admin-owners.css'
})
export class AdminOwnersComponent implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');

  readonly owners = signal<AdminOwnerItem[]>([]);
  readonly selectedOwner = signal<AdminOwnerDetails | null>(null);
  readonly showDetailsModal = signal<boolean>(false);

  searchQuery = '';
  statusFilter = 'all';

  readonly filteredOwners = computed(() => {
    let list = this.owners();
    const query = this.searchQuery.trim().toLowerCase();

    if (query) {
      list = list.filter(o =>
        o.fullName.toLowerCase().includes(query) ||
        o.email.toLowerCase().includes(query)
      );
    }

    if (this.statusFilter !== 'all') {
      list = list.filter(o => o.status.toLowerCase() === this.statusFilter);
    }

    return list;
  });

  ngOnInit(): void {
    this.loadOwners();
  }

  loadOwners(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getOwners().subscribe({
      next: (data) => {
        this.owners.set(data || []);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to load owner accounts.');
      }
    });
  }

  viewOwnerDetails(owner: AdminOwnerItem): void {
    this.adminService.getOwnerDetails(owner.ownerId).subscribe({
      next: (details) => {
        this.selectedOwner.set(details);
        this.showDetailsModal.set(true);
      }
    });
  }

  closeModal(): void {
    this.showDetailsModal.set(false);
    this.selectedOwner.set(null);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

