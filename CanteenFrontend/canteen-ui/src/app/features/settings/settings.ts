import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { AudioAlertService } from '../../core/services/audio-alert.service';
import { ThemeService, ThemeMode } from '../../core/services/theme.service';
import { PaymentService } from '../../core/services/payment.service';
import { 
  CanteenDto, 
  DaySchedule, 
  UpdateCanteenRequest,
  CanteenCancellationPolicy,
  UpdateCanteenCancellationPolicy 
} from '../../core/models/canteen.models';

export type SettingsTab = 'canteen' | 'address' | 'hours' | 'operations' | 'payments' | 'sound' | 'security';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.css'
})
export class SettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly paymentService = inject(PaymentService);
  readonly audioService = inject(AudioAlertService);
  readonly themeService = inject(ThemeService);

  readonly currentUser = this.authService.currentUser;
  readonly activeTab = signal<SettingsTab>('canteen');
  readonly isLoading = signal<boolean>(true);
  readonly isSaving = signal<boolean>(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  canteen = signal<CanteenDto | null>(null);
  cancellationPolicy = signal<CanteenCancellationPolicy | null>(null);

  // New Reason input tag for Cancellation Reasons
  newReasonInput = signal<string>('');
  predefinedReasons = signal<string[]>([
    'Item out of stock',
    'Kitchen overload',
    'Equipment breakdown',
    'Closing early'
  ]);

  // Weekly Hours State
  readonly weekDays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  weeklySchedule = signal<DaySchedule[]>([
    { day: 'Monday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
    { day: 'Tuesday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
    { day: 'Wednesday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
    { day: 'Thursday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
    { day: 'Friday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
    { day: 'Saturday', isOpen: true, openTime: '09:00', closeTime: '18:00' },
    { day: 'Sunday', isOpen: false, openTime: '09:00', closeTime: '18:00' }
  ]);

  // Forms
  readonly profileForm = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(150)]],
    slug: [''],
    location: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(250)]],
    canteenType: ['College / University'],
    description: [''],
    contactPhone: [''],
    contactEmail: ['', [Validators.email]],
    imageUrl: [''],
    isActive: [true]
  });

  readonly addressForm = this.fb.group({
    addressLine1: ['', [Validators.maxLength(200)]],
    addressLine2: ['', [Validators.maxLength(200)]],
    city: ['', [Validators.maxLength(100)]],
    state: ['', [Validators.maxLength(100)]],
    postalCode: ['', [Validators.maxLength(20)]],
    country: ['India', [Validators.maxLength(100)]],
    landmark: ['', [Validators.maxLength(150)]]
  });

  readonly operationsForm = this.fb.group({
    enableCustomerCancellation: [true],
    customCutoffMinutes: [5, [Validators.required, Validators.min(1), Validators.max(15)]],
    customPolicyNotice: ['Cancellations are accepted before cooking begins.'],
    autoAcceptOrders: [true],
    defaultPrepMinutes: [15, [Validators.min(1), Validators.max(120)]]
  });

  readonly paymentsForm = this.fb.group({
    bankName: [''],
    bankAccountHolder: [''],
    accountNumber: ['', [Validators.pattern(/^[0-9]{9,18}$/)]],
    ifscCode: ['', [Validators.pattern(/^[A-Z]{4}0[A-Z0-9]{6}$/i)]],
    accountType: ['Current'],
    upiId: ['', [Validators.pattern(/^[a-zA-Z0-9.\-_]{2,256}@[a-zA-Z]{2,64}$/)]],
    feeAbsorptionPolicy: ['CustomerPays'],
    taxPercentage: [0, [Validators.min(0), Validators.max(28)]]
  });

  readonly passwordForm = this.fb.group({
    currentPassword: ['', [Validators.required, Validators.minLength(6)]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(6)]]
  });

  readonly publicMenuUrl = computed(() => {
    const c = this.canteen();
    if (!c?.slug) return '';
    if (typeof window !== 'undefined') {
      return `${window.location.origin}/c/${c.slug}`;
    }
    return `/c/${c.slug}`;
  });

  ngOnInit(): void {
    // Check tab query parameter
    this.route.queryParamMap.subscribe(params => {
      const tab = params.get('tab') as SettingsTab | null;
      if (tab && ['canteen', 'address', 'hours', 'operations', 'payments', 'sound', 'security'].includes(tab)) {
        this.activeTab.set(tab);
      }
    });

    this.loadSettings();
  }

  loadSettings(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const user = this.currentUser();
    const canteenId = user?.canteenId;

    if (!canteenId) {
      // If user has no active canteenId in token, load via getMyCanteens
      this.canteenService.getMyCanteens().subscribe({
        next: (canteens) => {
          if (canteens && canteens.length > 0) {
            this.fetchCanteenData(canteens[0].canteenId);
          } else {
            this.isLoading.set(false);
          }
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.message || 'Failed to load canteen settings.');
          this.isLoading.set(false);
        }
      });
      return;
    }

    this.fetchCanteenData(canteenId);
  }

  private fetchCanteenData(canteenId: number): void {
    this.canteenService.getById(canteenId).subscribe({
      next: (data) => {
        this.canteen.set(data);
        this.populateForms(data);
        this.loadCancellationPolicy(canteenId);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || 'Unable to retrieve canteen settings.');
        this.isLoading.set(false);
      }
    });
  }

  private populateForms(c: CanteenDto): void {
    this.profileForm.patchValue({
      name: c.name || '',
      slug: c.slug || '',
      location: c.location || '',
      canteenType: c.canteenType || 'College / University',
      description: c.description || '',
      contactPhone: c.contactPhone || '',
      contactEmail: c.contactEmail || '',
      imageUrl: c.imageUrl || '',
      isActive: c.isActive ?? true
    });

    this.addressForm.patchValue({
      addressLine1: c.addressLine1 || '',
      addressLine2: c.addressLine2 || '',
      city: c.city || '',
      state: c.state || '',
      postalCode: c.postalCode || '',
      country: c.country || 'India',
      landmark: c.landmark || ''
    });

    this.paymentsForm.patchValue({
      bankName: c.bankName || '',
      bankAccountHolder: c.bankAccountHolder || '',
      accountNumber: c.accountNumber || '',
      ifscCode: c.ifscCode || '',
      accountType: c.accountType || 'Current',
      upiId: c.upiId || '',
      feeAbsorptionPolicy: c.feeAbsorptionPolicy || 'CustomerPays',
      taxPercentage: c.taxPercentage ?? 0
    });

    // Populate Operating Hours if JSON present
    if (c.operatingHoursJson) {
      try {
        const parsed = JSON.parse(c.operatingHoursJson);
        if (Array.isArray(parsed) && parsed.length > 0) {
          this.weeklySchedule.set(parsed);
        }
      } catch {
        // use default schedule
      }
    }
  }

  private loadCancellationPolicy(canteenId: number): void {
    this.canteenService.getCancellationPolicy(canteenId).subscribe({
      next: (policy) => {
        this.cancellationPolicy.set(policy);
        this.operationsForm.patchValue({
          enableCustomerCancellation: policy.enableCustomerCancellation,
          customCutoffMinutes: policy.customCutoffMinutes,
          customPolicyNotice: policy.customPolicyNotice
        });
        if (policy.predefinedCancellationReasons && policy.predefinedCancellationReasons.length > 0) {
          this.predefinedReasons.set(policy.predefinedCancellationReasons);
        }
      },
      error: () => {
        // Keep default operations values
      }
    });
  }

  setTab(tab: SettingsTab): void {
    this.activeTab.set(tab);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
  }

  // --- SAVE ACTIONS ---

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }
    const c = this.canteen();
    if (!c) return;

    this.isSaving.set(true);
    this.clearMessages();

    const val = this.profileForm.value;
    const updateReq: UpdateCanteenRequest = {
      name: val.name!,
      slug: val.slug || undefined,
      location: val.location!,
      canteenType: val.canteenType || undefined,
      description: val.description || undefined,
      contactPhone: val.contactPhone || undefined,
      contactEmail: val.contactEmail || undefined,
      imageUrl: val.imageUrl || undefined,
      isActive: val.isActive ?? true
    };

    this.canteenService.update(c.canteenId, updateReq).subscribe({
      next: (updated) => {
        this.canteen.set(updated);
        this.populateForms(updated);
        this.isSaving.set(false);
        this.successMessage.set('General canteen profile updated successfully.');
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to update profile settings.');
      }
    });
  }

  saveAddress(): void {
    if (this.addressForm.invalid) {
      this.addressForm.markAllAsTouched();
      return;
    }
    const c = this.canteen();
    if (!c) return;

    this.isSaving.set(true);
    this.clearMessages();

    const val = this.addressForm.value;
    const currentProfile = this.profileForm.value;

    const updateReq: UpdateCanteenRequest = {
      name: currentProfile.name || c.name,
      location: currentProfile.location || c.location,
      isActive: c.isActive ?? true,
      addressLine1: val.addressLine1 || undefined,
      addressLine2: val.addressLine2 || undefined,
      city: val.city || undefined,
      state: val.state || undefined,
      postalCode: val.postalCode || undefined,
      country: val.country || undefined,
      landmark: val.landmark || undefined
    };

    this.canteenService.update(c.canteenId, updateReq).subscribe({
      next: (updated) => {
        this.canteen.set(updated);
        this.populateForms(updated);
        this.isSaving.set(false);
        this.successMessage.set('Canteen address details saved successfully.');
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to save address details.');
      }
    });
  }

  saveOperatingHours(): void {
    const c = this.canteen();
    if (!c) return;

    this.isSaving.set(true);
    this.clearMessages();

    const hoursJson = JSON.stringify(this.weeklySchedule());
    const currentProfile = this.profileForm.value;

    const updateReq: UpdateCanteenRequest = {
      name: currentProfile.name || c.name,
      location: currentProfile.location || c.location,
      isActive: c.isActive ?? true,
      operatingHoursJson: hoursJson
    };

    this.canteenService.update(c.canteenId, updateReq).subscribe({
      next: (updated) => {
        this.canteen.set(updated);
        this.isSaving.set(false);
        this.successMessage.set('Weekly operating schedule saved successfully.');
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to update operating hours.');
      }
    });
  }

  copyMondayHoursToAll(): void {
    const schedule = [...this.weeklySchedule()];
    const monday = schedule.find(s => s.day === 'Monday');
    if (!monday) return;

    const updated = schedule.map(day => {
      if (day.day !== 'Saturday' && day.day !== 'Sunday') {
        return { ...day, isOpen: monday.isOpen, openTime: monday.openTime, closeTime: monday.closeTime };
      }
      return day;
    });

    this.weeklySchedule.set(updated);
  }

  toggleDayOpen(dayName: string): void {
    const updated = this.weeklySchedule().map(item => {
      if (item.day === dayName) {
        return { ...item, isOpen: !item.isOpen };
      }
      return item;
    });
    this.weeklySchedule.set(updated);
  }

  saveOperations(): void {
    if (this.operationsForm.invalid) {
      this.operationsForm.markAllAsTouched();
      return;
    }
    const c = this.canteen();
    if (!c) return;

    this.isSaving.set(true);
    this.clearMessages();

    const val = this.operationsForm.value;
    const policyDto: UpdateCanteenCancellationPolicy = {
      enableCustomerCancellation: val.enableCustomerCancellation ?? true,
      customCutoffMinutes: val.customCutoffMinutes ?? 5,
      predefinedCancellationReasons: this.predefinedReasons(),
      customPolicyNotice: val.customPolicyNotice || ''
    };

    this.canteenService.updateCancellationPolicy(c.canteenId, policyDto).subscribe({
      next: (savedPolicy) => {
        this.cancellationPolicy.set(savedPolicy);
        this.isSaving.set(false);
        this.successMessage.set('Dining and cancellation policy settings updated successfully.');
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to update cancellation policy.');
      }
    });
  }

  addReasonTag(): void {
    const text = this.newReasonInput().trim();
    if (!text) return;
    if (!this.predefinedReasons().includes(text)) {
      this.predefinedReasons.set([...this.predefinedReasons(), text]);
    }
    this.newReasonInput.set('');
  }

  removeReasonTag(reason: string): void {
    this.predefinedReasons.set(this.predefinedReasons().filter(r => r !== reason));
  }

  savePayments(): void {
    if (this.paymentsForm.invalid) {
      this.paymentsForm.markAllAsTouched();
      return;
    }
    const c = this.canteen();
    if (!c) return;

    this.isSaving.set(true);
    this.clearMessages();

    const val = this.paymentsForm.value;
    this.paymentService.updatePaymentSettings(c.canteenId, {
      bankName: val.bankName || undefined,
      bankAccountHolder: val.bankAccountHolder || undefined,
      accountNumber: val.accountNumber || undefined,
      ifscCode: val.ifscCode ? val.ifscCode.toUpperCase() : undefined,
      accountType: val.accountType || undefined,
      upiId: val.upiId || undefined,
      feeAbsorptionPolicy: val.feeAbsorptionPolicy || undefined,
      taxPercentage: val.taxPercentage ?? 0
    }).subscribe({
      next: (res) => {
        this.isSaving.set(false);
        this.successMessage.set('Bank settlement, UPI and tax preferences saved successfully.');
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to save payment settings.');
      }
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.value;
    if (newPassword !== confirmPassword) {
      this.errorMessage.set('New password and confirm password do not match.');
      return;
    }

    this.isSaving.set(true);
    this.clearMessages();

    this.authService.changePassword({
      currentPassword: currentPassword!,
      newPassword: newPassword!
    }).subscribe({
      next: (res) => {
        this.isSaving.set(false);
        this.successMessage.set(res.message || 'Password changed successfully!');
        this.passwordForm.reset();
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to change password. Please check your current password.');
      }
    });
  }

  // --- AUDIO ALERT ACTIONS ---
  toggleAudioAlerts(): void {
    this.audioService.toggleAudio();
  }

  toggleVoiceAlerts(): void {
    this.audioService.toggleVoice();
  }

  testAudioChime(): void {
    this.audioService.playNewOrderChime();
  }

  testVoiceAnnouncement(): void {
    this.audioService.speakToken('104', 'Counter 1');
  }

  setTheme(mode: ThemeMode): void {
    this.themeService.setMode(mode);
  }

  copyPublicMenuUrl(): void {
    const url = this.publicMenuUrl();
    if (!url) return;
    if (navigator?.clipboard) {
      navigator.clipboard.writeText(url);
      this.successMessage.set('Public Menu link copied to clipboard!');
      setTimeout(() => {
        if (this.successMessage() === 'Public Menu link copied to clipboard!') {
          this.successMessage.set(null);
        }
      }, 3000);
    }
  }

  private clearMessages(): void {
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }
}
