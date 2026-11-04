import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SettingsComponent } from './settings';
import { CanteenService } from '../../core/services/canteen.service';
import { AuthService } from '../../core/services/auth.service';
import { PaymentService } from '../../core/services/payment.service';
import { AudioAlertService } from '../../core/services/audio-alert.service';
import { ThemeService } from '../../core/services/theme.service';
import { CanteenDto, CanteenCancellationPolicy } from '../../core/models/canteen.models';

describe('SettingsComponent', () => {
  let component: SettingsComponent;
  let fixture: ComponentFixture<SettingsComponent>;
  let canteenService: CanteenService;
  let authService: AuthService;
  let paymentService: PaymentService;
  let audioService: AudioAlertService;
  let themeService: ThemeService;

  const mockCanteen: CanteenDto = {
    canteenId: 10,
    name: 'CT Catten',
    slug: 'ct-catten',
    location: 'Campus Building A',
    canteenType: 'College / University',
    description: 'Fresh healthy student meals',
    contactPhone: '+91 9988776655',
    contactEmail: 'tanu@canteen.com',
    bankAccountHolder: 'Tanisha Mahajan',
    bankName: 'HDFC Bank',
    accountNumber: '123456789012',
    ifscCode: 'HDFC0001234',
    accountType: 'Current',
    upiId: 'tanu@okhdfcbank',
    addressLine1: 'Station Road',
    city: 'Pune',
    state: 'Maharashtra',
    postalCode: '411001',
    country: 'India',
    landmark: 'Opp City Center',
    feeAbsorptionPolicy: 'CustomerPays',
    taxPercentage: 5,
    isActive: true,
    operatingHoursJson: JSON.stringify([
      { day: 'Monday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
      { day: 'Tuesday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
      { day: 'Wednesday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
      { day: 'Thursday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
      { day: 'Friday', isOpen: true, openTime: '08:00', closeTime: '20:00' },
      { day: 'Saturday', isOpen: true, openTime: '09:00', closeTime: '18:00' },
      { day: 'Sunday', isOpen: false, openTime: '09:00', closeTime: '18:00' }
    ]),
    createdAt: '2026-01-01T00:00:00Z'
  };

  const mockPolicy: CanteenCancellationPolicy = {
    canteenPolicyId: 1,
    canteenId: 10,
    appliedPlatformPolicyVersion: 1,
    enableCustomerCancellation: true,
    customCutoffMinutes: 5,
    predefinedCancellationReasons: ['Item out of stock', 'Kitchen busy'],
    customPolicyNotice: 'Cancellations allowed before cooking starts.',
    updatedAt: '2026-01-01T00:00:00Z',
    platformAllowsCustomerCancellation: true,
    platformMaxCutoffMinutes: 5,
    platformProhibitsPostPreparation: true
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SettingsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    canteenService = TestBed.inject(CanteenService);
    authService = TestBed.inject(AuthService);
    paymentService = TestBed.inject(PaymentService);
    audioService = TestBed.inject(AudioAlertService);
    themeService = TestBed.inject(ThemeService);

    // Mock services
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      token: 'mock-token',
      ownerId: 10,
      fullName: 'Tanisha Mahajan',
      email: 'tanu@canteen.com',
      role: 'CanteenOwner',
      canteenId: 10,
      expiresAt: '2027-01-01T00:00:00Z'
    });

    vi.spyOn(canteenService, 'getById').mockReturnValue(of(mockCanteen));
    vi.spyOn(canteenService, 'getCancellationPolicy').mockReturnValue(of(mockPolicy));
    vi.spyOn(canteenService, 'update').mockReturnValue(of(mockCanteen));
    vi.spyOn(canteenService, 'updateCancellationPolicy').mockReturnValue(of(mockPolicy));
    vi.spyOn(paymentService, 'updatePaymentSettings').mockReturnValue(of({} as any));
    vi.spyOn(authService, 'changePassword').mockReturnValue(of({ message: 'Password updated successfully!' }));

    fixture = TestBed.createComponent(SettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load settings for active canteen', () => {
    expect(component).toBeTruthy();
    expect(component.canteen()?.name).toBe('CT Catten');
    expect(component.profileForm.controls.name.value).toBe('CT Catten');
    expect(component.profileForm.controls.location.value).toBe('Campus Building A');
    expect(component.cancellationPolicy()?.customCutoffMinutes).toBe(5);
    expect(component.isLoading()).toBe(false);
  });

  it('should switch tabs smoothly', () => {
    expect(component.activeTab()).toBe('canteen');
    component.setTab('hours');
    expect(component.activeTab()).toBe('hours');
    component.setTab('operations');
    expect(component.activeTab()).toBe('operations');
    component.setTab('payments');
    expect(component.activeTab()).toBe('payments');
    component.setTab('sound');
    expect(component.activeTab()).toBe('sound');
    component.setTab('security');
    expect(component.activeTab()).toBe('security');
  });

  it('should save canteen profile changes', () => {
    component.profileForm.patchValue({
      name: 'CT Catten Updated',
      location: 'New Campus Wing'
    });

    component.saveProfile();

    expect(canteenService.update).toHaveBeenCalledWith(10, expect.objectContaining({
      name: 'CT Catten Updated',
      location: 'New Campus Wing'
    }));
    expect(component.successMessage()).toBe('General canteen profile updated successfully.');
  });

  it('should save physical address changes', () => {
    component.addressForm.patchValue({
      addressLine1: '45 MG Road',
      city: 'Pune',
      state: 'Maharashtra',
      postalCode: '411005'
    });

    component.saveAddress();

    expect(canteenService.update).toHaveBeenCalledWith(10, expect.objectContaining({
      addressLine1: '45 MG Road',
      city: 'Pune'
    }));
    expect(component.successMessage()).toBe('Canteen address details saved successfully.');
  });

  it('should toggle day open/close and copy Monday schedule to weekdays', () => {
    component.toggleDayOpen('Monday');
    const mon = component.weeklySchedule().find(s => s.day === 'Monday');
    expect(mon?.isOpen).toBe(false);

    // Turn Monday back on with new hours
    mon!.isOpen = true;
    mon!.openTime = '07:30';
    mon!.closeTime = '21:00';

    component.copyMondayHoursToAll();
    const wed = component.weeklySchedule().find(s => s.day === 'Wednesday');
    expect(wed?.openTime).toBe('07:30');
    expect(wed?.closeTime).toBe('21:00');

    component.saveOperatingHours();
    expect(canteenService.update).toHaveBeenCalled();
    expect(component.successMessage()).toBe('Weekly operating schedule saved successfully.');
  });

  it('should add and remove predefined cancellation reason tags', () => {
    component.newReasonInput.set('Equipment failure');
    component.addReasonTag();
    expect(component.predefinedReasons()).toContain('Equipment failure');

    component.removeReasonTag('Equipment failure');
    expect(component.predefinedReasons()).not.toContain('Equipment failure');
  });

  it('should save operations and cancellation policy', () => {
    component.operationsForm.patchValue({
      enableCustomerCancellation: true,
      customCutoffMinutes: 8,
      customPolicyNotice: 'Custom Notice'
    });

    component.saveOperations();

    expect(canteenService.updateCancellationPolicy).toHaveBeenCalledWith(10, {
      enableCustomerCancellation: true,
      customCutoffMinutes: 8,
      predefinedCancellationReasons: component.predefinedReasons(),
      customPolicyNotice: 'Custom Notice'
    });
    expect(component.successMessage()).toBe('Dining and cancellation policy settings updated successfully.');
  });

  it('should save payment settlement and GST tax preferences', () => {
    component.paymentsForm.patchValue({
      bankName: 'ICICI Bank',
      bankAccountHolder: 'CT Enterprises',
      accountNumber: '987654321012',
      ifscCode: 'ICIC0001234',
      taxPercentage: 18
    });

    component.savePayments();

    expect(paymentService.updatePaymentSettings).toHaveBeenCalledWith(10, expect.objectContaining({
      bankName: 'ICICI Bank',
      taxPercentage: 18
    }));
    expect(component.successMessage()).toBe('Bank settlement, UPI and tax preferences saved successfully.');
  });

  it('should prevent password change when confirmation does not match', () => {
    component.passwordForm.patchValue({
      currentPassword: 'Password123!',
      newPassword: 'NewPassword123!',
      confirmPassword: 'DifferentPassword123!'
    });

    component.changePassword();

    expect(authService.changePassword).not.toHaveBeenCalled();
    expect(component.errorMessage()).toBe('New password and confirm password do not match.');
  });

  it('should call authService.changePassword when valid', () => {
    component.passwordForm.patchValue({
      currentPassword: 'Password123!',
      newPassword: 'NewPassword123!',
      confirmPassword: 'NewPassword123!'
    });

    component.changePassword();

    expect(authService.changePassword).toHaveBeenCalledWith({
      currentPassword: 'Password123!',
      newPassword: 'NewPassword123!'
    });
    expect(component.successMessage()).toBe('Password updated successfully!');
  });

  it('should toggle audio and voice alerts and trigger theme changes', () => {
    const audioSpy = vi.spyOn(audioService, 'toggleAudio');
    const voiceSpy = vi.spyOn(audioService, 'toggleVoice');
    const themeSpy = vi.spyOn(themeService, 'setMode');

    component.toggleAudioAlerts();
    expect(audioSpy).toHaveBeenCalled();

    component.toggleVoiceAlerts();
    expect(voiceSpy).toHaveBeenCalled();

    component.setTheme('dark');
    expect(themeSpy).toHaveBeenCalledWith('dark');
  });
});

