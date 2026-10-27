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
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  ngOnInit(): void {
    const isNew = this.route.snapshot.queryParamMap.get('new') === 'true';
    const stepParam = this.route.snapshot.queryParamMap.get('step');

    if (isNew || stepParam === '1') {
      this.currentStep.set(1);
      return;
    }

    // If owner already has an assigned canteen and didn't request a new setup, redirect to dashboard
    const user = this.currentUser();
    if (user?.canteenId) {
      this.router.navigate(['/dashboard']);
      return;
    }

    this.authService.getProfile().subscribe({
      next: (profile) => {
        if (profile.canteenId) {
          this.router.navigate(['/dashboard']);
        }
      },
      error: () => {}
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
    this.router.navigate(['/my-canteens']);
  }

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
      this.currentStep.update(s => Math.min(s + 1, 4));
    }
  }

  prevStep(): void {
    this.errorMessage.set(null);
    this.currentStep.update(s => Math.max(s - 1, 1));
  }

  cancel(): void {
    this.router.navigate(['/my-canteens']);
  }

  validateStep(step: number): boolean {
    if (step === 1) {
      if (!this.canteenName.trim() || this.canteenName.trim().length < 2) {
        this.errorMessage.set('Please provide a valid Canteen Name (at least 2 characters).');
        return false;
      }
      if (!this.canteenType) {
        this.errorMessage.set('Please select a Canteen Type.');
        return false;
      }
      if (!this.phone.trim() || this.phone.trim().length < 6) {
        this.errorMessage.set('Please provide a valid Contact Phone number.');
        return false;
      }
      return true;
    }

    if (step === 2) {
      if (!this.addressLine1.trim() || this.addressLine1.trim().length < 2) {
        this.errorMessage.set('Address Line 1 is required (at least 2 characters).');
        return false;
      }
      if (!this.city.trim()) {
        this.errorMessage.set('City is required.');
        return false;
      }
      if (!this.postalCode.trim()) {
        this.errorMessage.set('Postal / PIN Code is required.');
        return false;
      }
      return true;
    }

    if (step === 3) {
      const openCount = this.operatingHours.filter(h => h.isOpen).length;
      if (openCount === 0) {
        this.errorMessage.set('Please enable at least one operating day for your store.');
        return false;
      }
      return true;
    }

    return true;
  }

  private validateCurrentStep(): boolean {
    return this.validateStep(this.currentStep());
  }

  submitSetup(): void {
    for (let s = 1; s <= 3; s++) {
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

    this.canteenService.create(payload).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.successMessage.set(`"${created.name}" has been registered successfully!`);

        // Update authenticated user session with fresh token & canteenId
        this.authService.updateCanteen(created.canteenId, created.token);

        // Move to Step 5 (Celebration animation with action buttons)
        this.currentStep.set(5);
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
