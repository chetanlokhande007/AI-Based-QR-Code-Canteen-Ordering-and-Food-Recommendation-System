import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { ThemeService } from '../../core/services/theme.service';
import { DaySchedule, CreateCanteenRequest } from '../../core/models/canteen.models';

@Component({
  selector: 'app-canteen-setup',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './canteen-setup.html',
  styleUrl: './canteen-setup.css'
})
export class CanteenSetup implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly currentUser = this.authService.currentUser;
  readonly activeTheme = this.themeService.activeTheme;

  // Wizard Step Control (0: Status Check, 1: Details, 2: Address, 3: Hours, 4: Review, 5: Success)
  readonly currentStep = signal<number>(0);
  existingCanteenId: number | null = null;
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  ngOnInit(): void {
    const isNew = this.route.snapshot.queryParamMap.get('new') === 'true';
    const stepParam = this.route.snapshot.queryParamMap.get('step');

    const user = this.currentUser();
    if (!user) {
      this.router.navigate(['/login']);
      return;
    }

    // Check if owner already has a canteen
    this.canteenService.getMyCanteens().subscribe({
      next: (canteens) => {
        if (!canteens || canteens.length === 0) {
          // No canteen yet -> start setup for their one canteen
          this.currentStep.set(1);
          return;
        }

        // Owner already has their one canteen
        const current = canteens[0];
        this.existingCanteenId = current.canteenId;

        // If it's already completely set up and not explicitly editing, go to dashboard
        if (current.isSetupComplete && !isNew && !stepParam) {
          this.router.navigate(['/dashboard']);
          return;
        }

        // Fetch full details of their one canteen to pre-fill
        this.canteenService.getById(current.canteenId).subscribe({
          next: (full) => {
            if (full.name && !this.canteenName) this.canteenName = full.name;
            if (full.contactPhone && !this.phone) this.phone = full.contactPhone;
            if (full.contactEmail && !this.email) this.email = full.contactEmail;
            if (full.canteenType && !this.canteenType) this.canteenType = full.canteenType;
            if (full.description && !this.description) this.description = full.description;
            if (full.addressLine1 && !this.addressLine1) this.addressLine1 = full.addressLine1;
            if (full.addressLine2 && !this.addressLine2) this.addressLine2 = full.addressLine2;
            if (full.city && !this.city) this.city = full.city;
            if (full.state && !this.state) this.state = full.state;
            if (full.postalCode && !this.postalCode) this.postalCode = full.postalCode;
            if (full.country && !this.country) this.country = full.country;
            if (full.landmark && !this.landmark) this.landmark = full.landmark;
            if (full.bankAccountHolder && !this.bankAccountHolder) this.bankAccountHolder = full.bankAccountHolder;
            if (full.bankName && !this.bankName) this.bankName = full.bankName;
            if (full.accountNumber && !this.accountNumber) this.accountNumber = full.accountNumber;
            if (full.ifscCode && !this.ifscCode) this.ifscCode = full.ifscCode;
            if (full.accountType && !this.accountType) this.accountType = full.accountType;
            if (full.upiId && !this.upiId) this.upiId = full.upiId;
            if (full.imageUrl && !this.canteenImage()) this.canteenImage.set(full.imageUrl);
            this.currentStep.set(1);
          },
          error: () => {
            this.currentStep.set(1);
          }
        });
      },
      error: () => {
        this.currentStep.set(1);
      }
    });
  }

  startWizard(): void {
    this.errorMessage.set(null);
    this.currentStep.set(1);
  }

  skipToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  goToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  goToMyCanteens(): void {
    this.router.navigate(['/dashboard']);
  }

  // Step 4: Bank & Settlement Details (Owner Account)
  bankAccountHolder = '';
  bankName = '';
  accountNumber = '';
  confirmAccountNumber = '';
  ifscCode = '';
  accountType = 'Current Account';
  upiId = '';

  readonly popularBanks = [
    'State Bank of India (SBI)',
    'HDFC Bank',
    'ICICI Bank',
    'Axis Bank',
    'Punjab National Bank (PNB)',
    'Bank of Baroda',
    'Kotak Mahindra Bank',
    'Canara Bank',
    'Union Bank of India',
    'Other Bank'
  ];

  readonly accountTypes = [
    'Current Account',
    'Savings Account'
  ];

  readonly maskedAccountNumber = computed(() => {
    const acc = this.accountNumber.trim();
    if (!acc) return 'Not provided';
    if (acc.length <= 4) return acc;
    return '•••• •••• ' + acc.slice(-4);
  });

  // Step 1: Canteen Details (Mockup 1)
  canteenName = '';
  canteenType = 'College Canteen';
  description = '';
  phone = '';
  email = '';

  // Canteen Hero Photo / Banner (Uploaded & Stored in Database)
  readonly canteenImage = signal<string>('');
  readonly imageFileName = signal<string>('');
  readonly isDraggingImage = signal<boolean>(false);
  readonly imageUploadError = signal<string | null>(null);

  readonly displayImage = computed(() => {
    return this.canteenImage() || '/canteen-preview.jpg';
  });

  onImageFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.processImageFile(input.files[0]);
    }
  }

  onImageDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingImage.set(true);
  }

  onImageDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingImage.set(false);
  }

  onImageDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingImage.set(false);
    if (event.dataTransfer && event.dataTransfer.files.length > 0) {
      this.processImageFile(event.dataTransfer.files[0]);
    }
  }

  processImageFile(file: File): void {
    this.imageUploadError.set(null);

    if (!file.type.startsWith('image/')) {
      this.imageUploadError.set('Please select a valid image file (JPG, PNG, WebP).');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.imageUploadError.set('Image file exceeds the 5MB size limit. Please choose a smaller photo.');
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      const rawDataUrl = e.target?.result as string;
      if (!rawDataUrl) return;

      const img = new Image();
      img.onload = () => {
        const maxWidth = 1200;
        const maxHeight = 800;
        let width = img.width;
        let height = img.height;

        if (width > maxWidth || height > maxHeight) {
          if (width / height > maxWidth / maxHeight) {
            height = Math.round((height * maxWidth) / width);
            width = maxWidth;
          } else {
            width = Math.round((width * maxHeight) / height);
            height = maxHeight;
          }
        }

        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.drawImage(img, 0, 0, width, height);
          const compressedDataUrl = canvas.toDataURL('image/jpeg', 0.88);
          this.canteenImage.set(compressedDataUrl);
          this.imageFileName.set(file.name);
        } else {
          this.canteenImage.set(rawDataUrl);
          this.imageFileName.set(file.name);
        }
      };
      img.onerror = () => {
        this.canteenImage.set(rawDataUrl);
        this.imageFileName.set(file.name);
      };
      img.src = rawDataUrl;
    };
    reader.onerror = () => {
      this.imageUploadError.set('Failed to read image file. Please try again.');
    };
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    this.canteenImage.set('');
    this.imageFileName.set('');
    this.imageUploadError.set(null);
  }

  readonly canteenTypes = [
    'College Canteen',
    'Corporate Cafeteria',
    'Hospital Food Service',
    'School Canteen',
    'Food Court Stall',
    'Commercial Cafeteria',
    'Other'
  ];

  // Step 2: Address Details (Mockup 1)
  addressLine1 = '';
  addressLine2 = '';
  city = 'Pune';
  state = 'Maharashtra';
  postalCode = '';
  country = 'India';
  landmark = '';

  readonly indianStates = [
    'Maharashtra',
    'Karnataka',
    'Delhi',
    'Gujarat',
    'Tamil Nadu',
    'Telangana',
    'Uttar Pradesh',
    'Rajasthan',
    'West Bengal',
    'Punjab',
    'Madhya Pradesh',
    'Kerala'
  ];

  // Step 3: Store Hours (Mockup 2)
  operatingHours: DaySchedule[] = [
    { day: 'Monday', isOpen: true, openTime: '08:00 AM', closeTime: '08:00 PM' },
    { day: 'Tuesday', isOpen: true, openTime: '08:00 AM', closeTime: '08:00 PM' },
    { day: 'Wednesday', isOpen: true, openTime: '08:00 AM', closeTime: '08:00 PM' },
    { day: 'Thursday', isOpen: true, openTime: '08:00 AM', closeTime: '08:00 PM' },
    { day: 'Friday', isOpen: true, openTime: '08:00 AM', closeTime: '09:00 PM' },
    { day: 'Saturday', isOpen: true, openTime: '08:00 AM', closeTime: '09:00 PM' },
    { day: 'Sunday', isOpen: false, openTime: '09:00 AM', closeTime: '06:00 PM' }
  ];

  // Copy Monday's hours to all open days (Mockup 2 "Copy to All")
  copyToAllHours(): void {
    const monday = this.operatingHours[0];
    for (let i = 1; i < this.operatingHours.length; i++) {
      if (this.operatingHours[i].isOpen) {
        this.operatingHours[i].openTime = monday.openTime;
        this.operatingHours[i].closeTime = monday.closeTime;
      }
    }
  }

  // Active "Watch Set Time" Picker State
  readonly activePicker = signal<{
    schedule: DaySchedule;
    field: 'open' | 'close';
  } | null>(null);

  readonly selectedHour = signal<number>(8);
  readonly selectedMinute = signal<string>('00');
  readonly selectedPeriod = signal<'AM' | 'PM'>('AM');

  readonly availableHours = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  readonly availableMinutes = ['00', '05', '10', '15', '20', '25', '30', '35', '40', '45', '50', '55'];

  readonly openPresets = ['07:00 AM', '07:30 AM', '08:00 AM', '08:30 AM', '09:00 AM', '09:30 AM', '10:00 AM'];
  readonly closePresets = ['06:00 PM', '06:30 PM', '07:00 PM', '07:30 PM', '08:00 PM', '08:30 PM', '09:00 PM', '10:00 PM'];

  get watchHourRotation(): number {
    const h = this.selectedHour();
    const m = parseInt(this.selectedMinute(), 10) || 0;
    return ((h % 12) + m / 60) * 30;
  }

  get watchMinuteRotation(): number {
    const m = parseInt(this.selectedMinute(), 10) || 0;
    return m * 6;
  }

  get formattedPickerTime(): string {
    const h = String(this.selectedHour()).padStart(2, '0');
    return `${h}:${this.selectedMinute()} ${this.selectedPeriod()}`;
  }

  openTimePicker(schedule: DaySchedule, field: 'open' | 'close', event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }
    if (!schedule.isOpen) return;

    const currentVal = field === 'open' ? schedule.openTime : schedule.closeTime;
    this.parseTimeToPicker(currentVal);

    this.activePicker.set({ schedule, field });
  }

  closeTimePicker(): void {
    this.activePicker.set(null);
  }

  applyTimePicker(): void {
    const picker = this.activePicker();
    if (!picker) return;

    const timeStr = this.formattedPickerTime;
    if (picker.field === 'open') {
      picker.schedule.openTime = timeStr;
    } else {
      picker.schedule.closeTime = timeStr;
    }
    this.closeTimePicker();
  }

  applyPreset(preset: string): void {
    this.parseTimeToPicker(preset);
  }

  setHour(h: number): void {
    this.selectedHour.set(h);
  }

  setMinute(m: string): void {
    this.selectedMinute.set(m);
  }

  setPeriod(p: 'AM' | 'PM'): void {
    this.selectedPeriod.set(p);
  }

  private parseTimeToPicker(timeStr: string): void {
    if (!timeStr) return;
    const match = timeStr.trim().match(/^(\d{1,2}):(\d{2})\s*(AM|PM)?$/i);
    if (match) {
      const h = parseInt(match[1], 10);
      const m = match[2];
      const p = (match[3] || 'AM').toUpperCase() as 'AM' | 'PM';

      this.selectedHour.set(h >= 1 && h <= 12 ? h : 8);
      this.selectedMinute.set(this.availableMinutes.includes(m) ? m : '00');
      this.selectedPeriod.set(p);
    }
  }

  // Owner Initials for Header Avatar
  get ownerInitials(): string {
    const name = this.currentUser()?.fullName || 'Canteen Owner';
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  // Formatted Single-Line Address
  get formattedLocation(): string {
    const parts = [
      this.addressLine1.trim(),
      this.addressLine2.trim(),
      this.city.trim(),
      this.state.trim() + (this.postalCode ? ' - ' + this.postalCode.trim() : ''),
      this.country.trim()
    ].filter(p => p.length > 0);

    return parts.length > 0 ? parts.join(', ') : 'Location will be shown here';
  }

  // Summary String for Operating Hours (Mockup 3)
  get operatingHoursSummary(): string {
    const openDays = this.operatingHours.filter(h => h.isOpen);
    const sunday = this.operatingHours.find(h => h.day === 'Sunday');
    const sat = this.operatingHours.find(h => h.day === 'Saturday');

    if (openDays.length === 0) return 'Temporarily Closed';

    let summary = 'Mon - Sat: 08:00 AM - 08:00 PM';
    if (sat && sat.isOpen && sat.closeTime !== '08:00 PM') {
      summary += ` (Sat till ${sat.closeTime})`;
    }
    if (sunday && !sunday.isOpen) {
      summary += ', Sunday: Closed';
    } else if (sunday && sunday.isOpen) {
      summary += `, Sunday: ${sunday.openTime} - ${sunday.closeTime}`;
    }
    return summary;
  }

  toggleDay(schedule: DaySchedule): void {
    schedule.isOpen = !schedule.isOpen;
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  goToStep(step: number): void {
    if (step < this.currentStep()) {
      this.errorMessage.set(null);
      this.currentStep.set(step);
      return;
    }

    if (step > this.currentStep()) {
      for (let s = 1; s < step; s++) {
        if (!this.validateStep(s)) {
          this.currentStep.set(s);
          return;
        }
      }
      this.errorMessage.set(null);
      this.currentStep.set(step);
    }
  }

  nextStep(): void {
    if (this.validateStep(this.currentStep())) {
      this.errorMessage.set(null);
      this.currentStep.update(s => Math.min(s + 1, 5));
    }
  }

  prevStep(): void {
    this.errorMessage.set(null);
    this.currentStep.update(s => Math.max(s - 1, 1));
  }

  cancel(): void {
    this.router.navigate(['/dashboard']);
  }

  // Touched state tracking for live inline error display
  readonly touched = signal<{ [key: string]: boolean }>({});

  touch(field: string): void {
    if (!this.touched()[field]) {
      this.touched.update(t => ({ ...t, [field]: true }));
    }
  }

  isTouched(field: string): boolean {
    return !!this.touched()[field];
  }

  // --- Step 1 Validation Getters ---
  get canteenNameError(): string | null {
    const val = this.canteenName.trim();
    if (!val) return 'Canteen Name is required.';
    if (val.length < 2) return 'Canteen Name must be at least 2 characters.';
    if (val.length > 100) return 'Canteen Name cannot exceed 100 characters.';
    return null;
  }

  get canteenTypeError(): string | null {
    if (!this.canteenType) return 'Please select a Canteen Type.';
    return null;
  }

  get phoneError(): string | null {
    const val = this.phone.trim();
    if (!val) return 'Contact Phone number is required.';
    const phoneRegex = /^(\+91[\-\s]?)?[6-9]\d{9}$/;
    const generalPhoneRegex = /^\+?[0-9]{10,14}$/;
    if (!phoneRegex.test(val) && !generalPhoneRegex.test(val)) {
      return 'Please enter a valid 10-digit phone number (e.g. 9876543210).';
    }
    return null;
  }

  get emailError(): string | null {
    const val = this.email.trim();
    if (!val) return null;
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(val)) {
      return 'Please enter a valid email address (e.g. canteen@example.com).';
    }
    return null;
  }

  // Sanitizers for live inputs
  onPhoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    input.value = input.value.replace(/[^\d+]/g, '').slice(0, 14);
    this.phone = input.value;
    this.touch('phone');
  }

  onPostalCodeInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    input.value = input.value.replace(/\D/g, '').slice(0, 6);
    this.postalCode = input.value;
    this.touch('postalCode');
  }

  onAccountNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    input.value = input.value.replace(/\D/g, '').slice(0, 18);
    this.accountNumber = input.value;
    this.touch('accountNumber');
  }

  onConfirmAccountNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    input.value = input.value.replace(/\D/g, '').slice(0, 18);
    this.confirmAccountNumber = input.value;
    this.touch('confirmAccountNumber');
  }

  onIfscInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    input.value = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 11);
    this.ifscCode = input.value;
    this.touch('ifscCode');
  }

  // --- Step 2 Validation Getters ---
  get addressLine1Error(): string | null {
    const val = this.addressLine1.trim();
    if (!val) return 'Address Line 1 is required.';
    if (val.length < 3) return 'Address must be at least 3 characters.';
    return null;
  }

  get cityError(): string | null {
    const val = this.city.trim();
    if (!val) return 'City is required.';
    if (val.length < 2) return 'Please enter a valid city name.';
    return null;
  }

  get stateError(): string | null {
    if (!this.state.trim()) return 'State is required.';
    return null;
  }

  get postalCodeError(): string | null {
    const val = this.postalCode.trim();
    if (!val) return 'Postal / PIN Code is required.';
    if (!/^\d{6}$/.test(val)) return 'Please enter a valid 6-digit PIN code (e.g. 411001).';
    return null;
  }

  // --- Step 3 Validation Getters ---
  parseTimeToMinutes(timeStr: string): number | null {
    if (!timeStr) return null;
    const match = timeStr.trim().match(/^(\d{1,2}):(\d{2})\s*(AM|PM)?$/i);
    if (!match) return null;
    let h = parseInt(match[1], 10);
    const m = parseInt(match[2], 10);
    const p = (match[3] || 'AM').toUpperCase();
    if (p === 'PM' && h < 12) h += 12;
    if (p === 'AM' && h === 12) h = 0;
    return h * 60 + m;
  }

  getDayHoursError(sched: DaySchedule): string | null {
    if (!sched.isOpen) return null;
    const openM = this.parseTimeToMinutes(sched.openTime);
    const closeM = this.parseTimeToMinutes(sched.closeTime);
    if (openM === null) return 'Invalid opening time.';
    if (closeM === null) return 'Invalid closing time.';
    if (closeM <= openM) return 'Closing time must be after opening time.';
    return null;
  }

  get step3HoursError(): string | null {
    const openDays = this.operatingHours.filter(h => h.isOpen);
    if (openDays.length === 0) {
      return 'Please enable at least one operating day for your store.';
    }
    for (const d of openDays) {
      const err = this.getDayHoursError(d);
      if (err) return `Invalid hours for ${d.day}: ${err}`;
    }
    return null;
  }

  // --- Step 4 Validation Getters ---
  get isBankConfigured(): boolean {
    return !!(
      this.bankAccountHolder.trim() ||
      this.bankName.trim() ||
      this.accountNumber.trim() ||
      this.confirmAccountNumber.trim() ||
      this.ifscCode.trim() ||
      this.upiId.trim()
    );
  }

  get bankAccountHolderError(): string | null {
    const val = this.bankAccountHolder.trim();
    if (this.isBankConfigured && !val) {
      return 'Account holder name is required for bank settlements.';
    }
    if (val && val.length < 2) {
      return 'Account holder name must be at least 2 characters.';
    }
    return null;
  }

  get bankNameError(): string | null {
    const val = this.bankName.trim();
    if (this.isBankConfigured && !val) {
      return 'Bank name is required.';
    }
    return null;
  }

  get accountNumberError(): string | null {
    const val = this.accountNumber.trim();
    if (this.isBankConfigured && !val) {
      return 'Account number is required.';
    }
    if (val && !/^\d{9,18}$/.test(val)) {
      return 'Account number must be between 9 and 18 digits (numbers only).';
    }
    return null;
  }

  get confirmAccountNumberError(): string | null {
    const val = this.confirmAccountNumber.trim();
    const acc = this.accountNumber.trim();
    if (acc && !val) {
      return 'Please re-enter your account number to confirm.';
    }
    if (acc && val && acc !== val) {
      return 'Account numbers do not match.';
    }
    return null;
  }

  get ifscCodeError(): string | null {
    const val = this.ifscCode.trim();
    if (this.isBankConfigured && !val) {
      return 'IFSC code is required for bank settlements.';
    }
    if (val) {
      const ifscRegex = /^[A-Z]{4}0[A-Z0-9]{6}$/;
      if (!ifscRegex.test(val)) {
        return 'Invalid IFSC code format (e.g. SBIN0001234, HDFC0000123).';
      }
    }
    return null;
  }

  get upiIdError(): string | null {
    const val = this.upiId.trim();
    if (!val) return null;
    const upiRegex = /^[\w.\-_]{2,256}@[a-zA-Z]{2,64}$/;
    if (!upiRegex.test(val)) {
      return 'Please enter a valid UPI ID (e.g. name@okaxis).';
    }
    return null;
  }

  validateStep(step: number): boolean {
    if (step === 1) {
      this.touch('canteenName');
      this.touch('canteenType');
      this.touch('phone');
      if (this.email.trim()) this.touch('email');

      if (this.canteenNameError) {
        this.errorMessage.set(this.canteenNameError);
        return false;
      }
      if (this.canteenTypeError) {
        this.errorMessage.set(this.canteenTypeError);
        return false;
      }
      if (this.phoneError) {
        this.errorMessage.set(this.phoneError);
        return false;
      }
      if (this.emailError) {
        this.errorMessage.set(this.emailError);
        return false;
      }
      return true;
    }

    if (step === 2) {
      this.touch('addressLine1');
      this.touch('city');
      this.touch('state');
      this.touch('postalCode');

      if (this.addressLine1Error) {
        this.errorMessage.set(this.addressLine1Error);
        return false;
      }
      if (this.cityError) {
        this.errorMessage.set(this.cityError);
        return false;
      }
      if (this.stateError) {
        this.errorMessage.set(this.stateError);
        return false;
      }
      if (this.postalCodeError) {
        this.errorMessage.set(this.postalCodeError);
        return false;
      }
      return true;
    }

    if (step === 3) {
      if (this.step3HoursError) {
        this.errorMessage.set(this.step3HoursError);
        return false;
      }
      return true;
    }

    if (step === 4) {
      if (this.isBankConfigured) {
        this.touch('bankAccountHolder');
        this.touch('bankName');
        this.touch('accountNumber');
        this.touch('confirmAccountNumber');
        this.touch('ifscCode');
        if (this.upiId.trim()) this.touch('upiId');

        if (this.bankAccountHolderError) {
          this.errorMessage.set(this.bankAccountHolderError);
          return false;
        }
        if (this.bankNameError) {
          this.errorMessage.set(this.bankNameError);
          return false;
        }
        if (this.accountNumberError) {
          this.errorMessage.set(this.accountNumberError);
          return false;
        }
        if (this.confirmAccountNumberError) {
          this.errorMessage.set(this.confirmAccountNumberError);
          return false;
        }
        if (this.ifscCodeError) {
          this.errorMessage.set(this.ifscCodeError);
          return false;
        }
        if (this.upiIdError) {
          this.errorMessage.set(this.upiIdError);
          return false;
        }
      }
      return true;
    }

    return true;
  }

  private validateCurrentStep(): boolean {
    return this.validateStep(this.currentStep());
  }

  submitSetup(): void {
    for (let s = 1; s <= 4; s++) {
      if (!this.validateStep(s)) {
        this.currentStep.set(s);
        return;
      }
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const payload: CreateCanteenRequest = {
      name: this.canteenName.trim(),
      location: this.formattedLocation,
      canteenType: this.canteenType,
      description: this.description.trim(),
      contactPhone: this.phone.trim(),
      contactEmail: this.email.trim(),
      bankAccountHolder: this.bankAccountHolder.trim() || undefined,
      bankName: this.bankName.trim() || undefined,
      accountNumber: this.accountNumber.trim() || undefined,
      ifscCode: this.ifscCode.trim().toUpperCase() || undefined,
      accountType: this.accountType || undefined,
      upiId: this.upiId.trim() || undefined,
      addressLine1: this.addressLine1.trim(),
      addressLine2: this.addressLine2.trim(),
      city: this.city.trim(),
      state: this.state.trim(),
      postalCode: this.postalCode.trim(),
      country: this.country.trim(),
      landmark: this.landmark.trim(),
      operatingHoursJson: JSON.stringify(this.operatingHours),
      imageUrl: this.canteenImage() || undefined
    };

    const save$ = this.existingCanteenId
      ? this.canteenService.update(this.existingCanteenId, payload)
      : this.canteenService.create(payload);

    save$.subscribe({
      next: (saved) => {
        this.isSubmitting.set(false);
        this.successMessage.set(`"${saved.name}" has been configured successfully!`);

        // Update authenticated user session with fresh token & canteenId
        this.authService.updateCanteen(saved.canteenId, saved.token);

        // Move to Step 6 (Celebration animation with action buttons)
        this.currentStep.set(6);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const detail = err.error?.message || err.error?.title || 'Failed to register canteen. Please try again.';
        this.errorMessage.set(detail);
      }
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
