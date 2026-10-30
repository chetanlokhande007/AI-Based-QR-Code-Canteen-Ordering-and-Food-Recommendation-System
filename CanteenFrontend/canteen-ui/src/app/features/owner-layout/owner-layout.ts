import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { ThemeService } from '../../core/services/theme.service';
import { CanteenSummaryDto, CanteenDto } from '../../core/models/canteen.models';

@Component({
  selector: 'app-owner-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './owner-layout.html',
  styleUrl: './owner-layout.css'
})
export class OwnerLayout implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly subscriptions = new Subscription();

  readonly currentUser = this.authService.currentUser;
  readonly activeTheme = this.themeService.activeTheme;

  readonly canteens = signal<CanteenSummaryDto[]>([]);
  readonly activeCanteen = signal<CanteenDto | null>(null);
  readonly isCanteenSelectorOpen = signal<boolean>(false);
  readonly isSwitching = signal<boolean>(false);

  readonly currentCanteenName = computed(() => {
    return this.activeCanteen()?.name || 
      (this.currentUser()?.canteenId ? ('Canteen #' + this.currentUser()?.canteenId) : 'Select Canteen');
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
    this.loadOwnerCanteens();
    this.loadActiveCanteenDetails();

    // Listen to queryParams for canteen switching or updates
    this.subscriptions.add(
      this.route.queryParams.subscribe(params => {
        const qId = params['canteenId'] ? Number(params['canteenId']) : null;
        if (qId && qId !== this.currentUser()?.canteenId) {
          this.switchCanteenById(qId);
        } else {
          this.loadActiveCanteenDetails();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadOwnerCanteens(): void {
    this.canteenService.getMyCanteens().subscribe({
      next: (list) => {
        this.canteens.set(list || []);
        this.cdr.markForCheck();
      },
      error: () => {}
    });
  }

  loadActiveCanteenDetails(): void {
    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) {
      this.activeCanteen.set(null);
      return;
    }

    this.canteenService.getById(canteenId).subscribe({
      next: (canteen) => {
        this.activeCanteen.set(canteen);
        this.cdr.markForCheck();
      },
      error: () => {}
    });
  }

  toggleCanteenSelector(event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.isCanteenSelectorOpen.update(v => !v);
  }

  closeCanteenSelector(): void {
    this.isCanteenSelectorOpen.set(false);
  }

  switchCanteen(canteen: CanteenSummaryDto): void {
    this.closeCanteenSelector();
    if (canteen.status === 'Setup Pending') {
      this.router.navigate(['/canteen-setup'], { queryParams: { step: '1', new: 'true' } });
      return;
    }
    this.switchCanteenById(canteen.canteenId);
  }

  private switchCanteenById(canteenId: number): void {
    if (canteenId === this.currentUser()?.canteenId && this.activeCanteen()) {
      return;
    }

    this.isSwitching.set(true);
    this.authService.switchCanteen(canteenId).subscribe({
      next: () => {
        this.isSwitching.set(false);
        this.loadOwnerCanteens();
        this.loadActiveCanteenDetails();
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { canteenId: canteenId },
          queryParamsHandling: 'merge'
        });
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSwitching.set(false);
        this.cdr.markForCheck();
      }
    });
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  logout(): void {
    this.authService.logout();
  }
}

