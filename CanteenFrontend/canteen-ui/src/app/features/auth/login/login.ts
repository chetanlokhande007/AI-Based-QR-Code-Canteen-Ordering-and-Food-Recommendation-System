import { Component, ChangeDetectorRef, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';
import { CanteenService } from '../../../core/services/canteen.service';
import { ThemeService } from '../../../core/services/theme.service';

export type AuthViewMode = 'login' | 'register' | 'forgot' | 'reset';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly activeTheme = this.themeService.activeTheme;

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  ngOnInit(): void {
    const sessionExpired = this.route.snapshot.queryParamMap.get('sessionExpired');
    if (sessionExpired === 'true') {
      this.errorMessage.set('Your session has expired. Please sign in again.');
    }

    if (this.router.url.includes('/canteen/register') || this.route.snapshot.queryParamMap.get('tab') === 'register') {
      this.activeTab.set('register');
    }

    if (this.authService.isLoggedIn()) {
      const user = this.authService.currentUser();
      if (user?.role === 'PlatformAdmin') {
        this.router.navigate(['/admin/dashboard']);
        return;
      }
      if (user?.canteenId) {
        this.router.navigate(['/dashboard']);
      } else {
        this.router.navigate(['/canteen-setup']);
      }
    }
  }

  // Active view: 'login' | 'register' | 'forgot' | 'reset'
  readonly activeTab = signal<AuthViewMode>('login');

  // Login & Registration inputs
  email = '';
  password = '';
  fullName = '';
  confirmPassword = '';
  rememberMe = true;

  // Password Recovery inputs
  resetEmail = '';
  resetToken = '';
  newPassword = '';
  confirmNewPassword = '';
  readonly devResetToken = signal<string | null>(null);

  // UI state signals
  readonly showPassword = signal(false);
  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  switchTab(tab: AuthViewMode): void {
    this.activeTab.set(tab);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.cdr.markForCheck();
  }

  openForgotPassword(): void {
    this.activeTab.set('forgot');
    this.resetEmail = this.email.trim();
    this.errorMessage.set('');
    this.successMessage.set('');
    this.cdr.markForCheck();
  }

  openResetPassword(): void {
    this.activeTab.set('reset');
    if (!this.resetEmail) {
      this.resetEmail = this.email.trim();
    }
    this.errorMessage.set('');
    this.successMessage.set('');
    this.cdr.markForCheck();
  }

  fillDemoCredentials(): void {
    this.activeTab.set('login');
    this.email = 'owner@canteen.com';
    this.password = 'Password123!';
    this.errorMessage.set('');
    this.successMessage.set('Demo owner credentials applied! Click "Sign In" to continue.');
    this.cdr.markForCheck();
  }

  fillAdminCredentials(): void {
    this.activeTab.set('login');
    this.email = 'admin@canteen.com';
    this.password = 'Admin123!';
    this.errorMessage.set('');
    this.successMessage.set('Demo Admin credentials applied! Click "Sign In" to open Platform Admin Dashboard.');
    this.cdr.markForCheck();
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((visible) => !visible);
  }

  onSubmit(): void {
    const mode = this.activeTab();
    if (mode === 'login') {
      this.handleLogin();
    } else if (mode === 'register') {
      this.handleRegister();
    } else if (mode === 'forgot') {
      this.handleForgotPassword();
    } else if (mode === 'reset') {
      this.handleResetPassword();
    }
  }

  private handleLogin(): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const trimmedEmail = this.email.trim();
    if (!trimmedEmail || !this.password) {
      this.errorMessage.set('Please provide both your email and password.');
      this.cdr.markForCheck();
      return;
    }

    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailPattern.test(trimmedEmail)) {
      this.errorMessage.set('Please enter a valid email address.');
      this.cdr.markForCheck();
      return;
    }

    this.loading.set(true);
    this.cdr.markForCheck();

    this.authService.login({
      email: trimmedEmail,
      password: this.password
    }).subscribe({
      next: (response) => {
        this.loading.set(false);

        // ─── 1. Platform Admin Redirection ───────────────────────────────────
        if (response.role === 'PlatformAdmin') {
          this.successMessage.set(`Welcome Platform Administrator, ${response.fullName}! Opening platform dashboard...`);
          this.cdr.markForCheck();
          setTimeout(() => {
            this.router.navigate(['/admin/dashboard']);
          }, 400);
          return;
        }

        // ─── 2. Canteen Owner Redirection (Resolves authorized canteens) ─────
        this.canteenService.getMyCanteens().subscribe({
          next: (canteens) => {
            if (!canteens || canteens.length === 0) {
              // No canteens yet -> Onboarding / Canteen Setup
              this.successMessage.set(`Welcome back, ${response.fullName}! Redirecting to canteen setup...`);
              this.cdr.markForCheck();
              setTimeout(() => {
                this.router.navigate(['/canteen-setup']);
              }, 400);
            } else {
              // Owner has their ONE Canteen -> check if setup is complete
              const canteen = canteens[0];
              const isSetupIncomplete = !canteen.isSetupComplete &&
                (canteen.status === 'Setup Pending' ||
                 canteen.setupStatus === 'Draft' ||
                 (canteen.setupStatus as any) === 0 ||
                 (canteen.setupStatus as any) === 1);

              if (isSetupIncomplete) {
                this.successMessage.set(`Welcome back, ${response.fullName}! Please complete setting up your canteen...`);
                this.cdr.markForCheck();
                setTimeout(() => {
                  this.router.navigate(['/canteen-setup']);
                }, 400);
                return;
              }

              this.successMessage.set(`Welcome back, ${response.fullName}! Opening ${canteen.name}...`);
              this.cdr.markForCheck();
              if (response.canteenId !== canteen.canteenId) {
                this.authService.switchCanteen(canteen.canteenId).subscribe({
                  next: () => { this.router.navigate(['/dashboard']); },
                  error: () => { this.router.navigate(['/dashboard']); }
                });
              } else {
                setTimeout(() => {
                  this.router.navigate(['/dashboard']);
                }, 400);
              }
            }
          },
          error: () => {
            // Fallback in case of network issue
            const targetRoute = response.canteenId ? '/dashboard' : '/canteen-setup';
            this.router.navigate([targetRoute]);
          }
        });
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        if (error.status === 401) {
          this.errorMessage.set(error.error?.message || 'Invalid email or password. Please try again.');
        } else if (error.status === 400) {
          this.errorMessage.set(error.error?.message || 'Please check the provided login credentials.');
        } else if (error.status === 0) {
          this.errorMessage.set('Cannot reach backend API (http://localhost:5151). Please verify the server is running.');
        } else {
          this.errorMessage.set(error.error?.message || 'An unexpected error occurred. Please try again.');
        }
        this.cdr.markForCheck();
      }
    });
  }

  private handleRegister(): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const trimmedName = this.fullName.trim();
    const trimmedEmail = this.email.trim();

    if (!trimmedName || !trimmedEmail || !this.password) {
      this.errorMessage.set('All fields are required.');
      this.cdr.markForCheck();
      return;
    }

    if (trimmedName.length < 2) {
      this.errorMessage.set('Full name must be at least 2 characters long.');
      this.cdr.markForCheck();
      return;
    }

    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailPattern.test(trimmedEmail)) {
      this.errorMessage.set('Please enter a valid email address.');
      this.cdr.markForCheck();
      return;
    }

    if (this.password.length < 6) {
      this.errorMessage.set('Password must be at least 6 characters long.');
      this.cdr.markForCheck();
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.errorMessage.set('Passwords do not match. Please re-enter.');
      this.cdr.markForCheck();
      return;
    }

    this.loading.set(true);
    this.cdr.markForCheck();

    this.authService.register({
      fullName: trimmedName,
      email: trimmedEmail,
      password: this.password
    }).subscribe({
      next: (response) => {
        this.loading.set(false);
        const hasCanteen = !!response.canteenId;
        const targetRoute = hasCanteen ? '/dashboard' : '/canteen-setup';
        const msg = `Welcome, ${response.fullName}! Your owner profile has been created successfully. Redirecting to canteen setup...`;
        this.successMessage.set(msg);
        this.cdr.markForCheck();
        setTimeout(() => {
          this.router.navigate([targetRoute]);
        }, 600);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        if (error.status === 409) {
          this.errorMessage.set(error.error?.message || 'An account with this email address already exists.');
        } else if (error.status === 400) {
          this.errorMessage.set(error.error?.message || 'Please verify the registration details and try again.');
        } else if (error.status === 0) {
          this.errorMessage.set('Cannot reach backend API (http://localhost:5151). Please verify the server is running.');
        } else {
          this.errorMessage.set(error.error?.message || 'Registration failed. Please try again.');
        }
        this.cdr.markForCheck();
      }
    });
  }

  private handleForgotPassword(): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const trimmedEmail = this.resetEmail.trim();
    if (!trimmedEmail) {
      this.errorMessage.set('Please enter your email address.');
      this.cdr.markForCheck();
      return;
    }

    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailPattern.test(trimmedEmail)) {
      this.errorMessage.set('Please enter a valid email address.');
      this.cdr.markForCheck();
      return;
    }

    this.loading.set(true);
    this.cdr.markForCheck();

    this.authService.forgotPassword({ email: trimmedEmail }).subscribe({
      next: (response) => {
        this.loading.set(false);
        this.successMessage.set(response.message);

        // In development, auto-populate the token and move to 'reset' step!
        if (response.resetToken) {
          this.devResetToken.set(response.resetToken);
          this.resetToken = response.resetToken;
          this.successMessage.set(
            'Reset token generated! Enter your new password below to complete recovery.'
          );
        }

        // Advance to the token entry step
        this.activeTab.set('reset');
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(
          error.error?.message || 'Failed to process password reset request. Please try again.'
        );
        this.cdr.markForCheck();
      }
    });
  }

  private handleResetPassword(): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const trimmedEmail = this.resetEmail.trim();
    const trimmedToken = this.resetToken.trim();

    if (!trimmedEmail || !trimmedToken || !this.newPassword) {
      this.errorMessage.set('Email, reset token, and new password are all required.');
      this.cdr.markForCheck();
      return;
    }

    if (this.newPassword.length < 6) {
      this.errorMessage.set('New password must be at least 6 characters long.');
      this.cdr.markForCheck();
      return;
    }

    if (this.newPassword !== this.confirmNewPassword) {
      this.errorMessage.set('Passwords do not match. Please verify both passwords.');
      this.cdr.markForCheck();
      return;
    }

    this.loading.set(true);
    this.cdr.markForCheck();

    this.authService.resetPassword({
      email: trimmedEmail,
      token: trimmedToken,
      newPassword: this.newPassword
    }).subscribe({
      next: (response) => {
        this.loading.set(false);
        this.email = trimmedEmail;
        this.password = '';
        this.resetToken = '';
        this.newPassword = '';
        this.confirmNewPassword = '';
        this.devResetToken.set(null);
        this.activeTab.set('login');
        this.successMessage.set(response.message || 'Password has been reset successfully! Please sign in with your new password.');
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(
          error.error?.message || 'Invalid or expired reset token. Please check and try again.'
        );
        this.cdr.markForCheck();
      }
    });
  }
}
