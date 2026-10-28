import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { CanteenService } from '../../../core/services/canteen.service';
import { ThemeService } from '../../../core/services/theme.service';
import { CanteenSummaryDto } from '../../../core/models/canteen.models';

@Component({
  selector: 'app-my-canteens',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './my-canteens.html',
  styleUrl: './my-canteens.css'
})
export class MyCanteens implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly activeTheme = this.themeService.activeTheme;

  readonly canteens = signal<CanteenSummaryDto[]>([]);
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');

  // Search & Filter
  searchTerm = '';
  selectedStatus = 'all'; // 'all' | 'Active' | 'Setup Pending' | 'Inactive'
  readonly isStatusDropdownOpen = signal<boolean>(false);

  // Top Nav Canteen Selector Dropdown
  readonly isCanteenSelectorOpen = signal<boolean>(false);

  // Card Context Menu
  readonly activeCardMenuId = signal<number | null>(null);

  // Switching state
  readonly isSwitching = signal<boolean>(false);

  readonly filteredCanteens = computed(() => {
    let list = this.canteens();
    const query = this.searchTerm.trim().toLowerCase();
    const status = this.selectedStatus;

    if (query) {
      list = list.filter(c =>
        c.name.toLowerCase().includes(query) ||
        c.location.toLowerCase().includes(query)
      );
    }

    if (status !== 'all') {
      list = list.filter(c => c.status.toLowerCase() === status.toLowerCase());
    }

    return list;
  });

  readonly currentCanteen = computed(() => {
    const activeId = this.currentUser()?.canteenId;
    if (!activeId) return null;
    return this.canteens().find(c => c.canteenId === activeId) || null;
  });

  readonly currentCanteenName = computed(() => {
    return this.currentCanteen()?.name || 'Select Canteen';
  });

  get ownerInitials(): string {
    const name = this.currentUser()?.fullName?.trim();
    if (!name) return 'CO';
    const parts = name.split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  ngOnInit(): void {
    this.loadCanteens();
  }

  loadCanteens(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.canteenService.getMyCanteens().subscribe({
      next: (list) => {
        this.canteens.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load your canteens. Please check your connection and try again.');
      }
    });
  }

  toggleCanteenSelector(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isCanteenSelectorOpen.update(v => !v);
    this.isStatusDropdownOpen.set(false);
    this.activeCardMenuId.set(null);
  }

  toggleStatusDropdown(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isStatusDropdownOpen.update(v => !v);
    this.isCanteenSelectorOpen.set(false);
    this.activeCardMenuId.set(null);
  }

  setStatusFilter(status: string): void {
    this.selectedStatus = status;
    this.isStatusDropdownOpen.set(false);
  }

  getStatusFilterLabel(): string {
    switch (this.selectedStatus) {
      case 'Active': return 'Active';
      case 'Setup Pending': return 'Setup Pending';
      case 'Inactive': return 'Inactive';
      default: return 'All statuses';
    }
  }

  toggleCardMenu(canteenId: number, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    if (this.activeCardMenuId() === canteenId) {
      this.activeCardMenuId.set(null);
    } else {
      this.activeCardMenuId.set(canteenId);
    }
  }

  closeAllDropdowns(): void {
    this.isCanteenSelectorOpen.set(false);
    this.isStatusDropdownOpen.set(false);
    this.activeCardMenuId.set(null);
  }

  selectCanteen(canteen: CanteenSummaryDto): void {
    this.closeAllDropdowns();
    if (canteen.status === 'Setup Pending') {
      this.completeSetup(canteen);
      return;
    }

    if (canteen.canteenId === this.currentUser()?.canteenId) {
      this.router.navigate(['/dashboard'], { queryParams: { canteenId: canteen.canteenId } });
      return;
    }

    this.isSwitching.set(true);
    this.authService.switchCanteen(canteen.canteenId).subscribe({
      next: () => {
        this.isSwitching.set(false);
        this.router.navigate(['/dashboard'], { queryParams: { canteenId: canteen.canteenId } });
      },
      error: (err) => {
        this.isSwitching.set(false);
        alert(err.error?.message || 'Failed to switch canteen.');
      }
    });
  }

  openDashboard(canteen: CanteenSummaryDto): void {
    if (canteen.status === 'Setup Pending') {
      this.completeSetup(canteen);
      return;
    }

    if (canteen.canteenId === this.currentUser()?.canteenId) {
      this.router.navigate(['/dashboard'], { queryParams: { canteenId: canteen.canteenId } });
      return;
    }

    this.isSwitching.set(true);
    this.authService.switchCanteen(canteen.canteenId).subscribe({
      next: () => {
        this.isSwitching.set(false);
        this.router.navigate(['/dashboard'], { queryParams: { canteenId: canteen.canteenId } });
      },
      error: (err) => {
        this.isSwitching.set(false);
        alert(err.error?.message || 'Failed to switch canteen.');
      }
    });
  }

  manageMenu(canteen: CanteenSummaryDto): void {
    if (canteen.status === 'Setup Pending') {
      this.completeSetup(canteen);
      return;
    }

    if (canteen.canteenId === this.currentUser()?.canteenId) {
      this.router.navigate(['/dashboard'], { queryParams: { canteenId: canteen.canteenId, openMenu: 'products' } });
      return;
    }

    this.isSwitching.set(true);
    this.authService.switchCanteen(canteen.canteenId).subscribe({
      next: () => {
        this.isSwitching.set(false);
        this.router.navigate(['/dashboard'], { queryParams: { canteenId: canteen.canteenId, openMenu: 'products' } });
      },
      error: (err) => {
        this.isSwitching.set(false);
        alert(err.error?.message || 'Failed to switch canteen.');
      }
    });
  }

  completeSetup(canteen: CanteenSummaryDto): void {
    this.router.navigate(['/canteen-setup'], { queryParams: { step: '1', new: 'true' } });
  }

  addNewCanteen(): void {
    this.router.navigate(['/canteen-setup'], { queryParams: { step: '1', new: 'true' } });
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
