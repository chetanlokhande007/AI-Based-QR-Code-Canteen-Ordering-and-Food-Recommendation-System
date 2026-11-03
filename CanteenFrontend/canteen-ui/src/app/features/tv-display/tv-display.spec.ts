import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, Subject } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { TvDisplayComponent } from './tv-display';
import { OrderService } from '../../core/services/order.service';
import { SignalRService, OrderStatusUpdatedNotification, TvVoiceAnnouncementNotification, NewOrderNotification } from '../../core/services/signalr.service';
import { AudioAlertService } from '../../core/services/audio-alert.service';
import { AuthService } from '../../core/services/auth.service';
import { TvDeviceService } from '../../core/services/tv-device.service';
import { CanteenTvDisplayDto } from '../../core/models/order.models';
import { TvClaimResultDto } from '../../core/models/tv-device.models';

describe('TvDisplayComponent', () => {
  let component: TvDisplayComponent;
  let fixture: ComponentFixture<TvDisplayComponent>;
  let mockOrderService: any;
  let mockSignalRService: any;
  let mockAudioAlert: any;
  let mockAuthService: any;
  let mockTvDeviceService: any;

  const orderStatusUpdatedSubject = new Subject<OrderStatusUpdatedNotification>();
  const tvVoiceAnnouncementSubject = new Subject<TvVoiceAnnouncementNotification>();
  const newOrderSubject = new Subject<NewOrderNotification>();
  const tvDeviceRevokedSubject = new Subject<{ tvDeviceId: number; canteenId: number }>();

  const sampleTvData: CanteenTvDisplayDto = {
    canteenId: 1,
    canteenName: 'CT Catten',
    stationScope: 'All',
    deviceName: 'Counter 1 TV',
    preparingOrders: [
      { orderId: 201, orderNumber: 'ORD-20261103-9101', tokenNumber: '9101', status: 'InPreparation', createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), itemCount: 2 },
      { orderId: 202, orderNumber: 'ORD-20261103-9102', tokenNumber: '9102', status: 'InPreparation', createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), itemCount: 1 }
    ],
    readyOrders: [
      { orderId: 203, orderNumber: 'ORD-20261103-9099', tokenNumber: '9099', status: 'Ready', createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), itemCount: 3 }
    ]
  };

  const sampleClaimResult: TvClaimResultDto = {
    success: true,
    message: 'Paired successfully',
    deviceToken: 'test-device-token-64chars',
    canteenId: 1,
    canteenName: 'CT Catten',
    tvDeviceId: 7,
    deviceName: 'Main Dining TV',
    stationScope: 'Beverage'
  };

  beforeEach(async () => {
    mockOrderService = {
      getTvDisplayData: vi.fn().mockReturnValue(of(sampleTvData))
    };

    mockAudioAlert = {
      playOrderReadyChime: vi.fn(),
      speakToken: vi.fn(),
      announceCustom: vi.fn(),
      toggleAudio: vi.fn(),
      audioEnabled: signal(true),
      voiceEnabled: signal(true)
    };

    mockAuthService = {
      currentUser: signal({ canteenId: 1 })
    };

    mockTvDeviceService = {
      getStoredDeviceToken: vi.fn().mockReturnValue(null),
      setStoredDeviceToken: vi.fn(),
      clearStoredDeviceToken: vi.fn(),
      getOrCreateDeviceIdentifier: vi.fn().mockReturnValue('TV_DEVICE_UUID_123'),
      getTvDisplayByToken: vi.fn().mockReturnValue(of(sampleTvData)),
      claimPairingCode: vi.fn().mockReturnValue(of(sampleClaimResult))
    };

    mockSignalRService = {
      startTvConnection: vi.fn().mockReturnValue(Promise.resolve()),
      orderStatusUpdated$: orderStatusUpdatedSubject.asObservable(),
      tvVoiceAnnouncement$: tvVoiceAnnouncementSubject.asObservable(),
      newOrder$: newOrderSubject.asObservable(),
      tvDeviceRevoked$: tvDeviceRevokedSubject.asObservable()
    };

    await TestBed.configureTestingModule({
      imports: [TvDisplayComponent],
      providers: [
        { provide: OrderService, useValue: mockOrderService },
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: AudioAlertService, useValue: mockAudioAlert },
        { provide: AuthService, useValue: mockAuthService },
        { provide: TvDeviceService, useValue: mockTvDeviceService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: (key: string) => key === 'canteenId' ? '1' : null },
              queryParamMap: { get: () => null }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TvDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the TV display component', () => {
    expect(component).toBeTruthy();
  });

  it('should load initial preparing and ready token boards', () => {
    expect(component.canteenName()).toBe('CT Catten');
    expect(component.preparingOrders().length).toBe(2);
    expect(component.readyOrders().length).toBe(1);
    expect(component.readyOrders()[0].tokenNumber).toBe('9099');
  });

  it('should move token from preparing to ready and trigger voice announcement on Ready status event', () => {
    orderStatusUpdatedSubject.next({
      orderId: 201,
      orderNumber: 'ORD-20261103-9101',
      newStatus: 'Ready',
      updatedAt: new Date().toISOString(),
      canteenId: 1
    });

    expect(component.preparingOrders().some(o => o.orderId === 201)).toBe(false);
    expect(component.readyOrders().some(o => o.orderId === 201)).toBe(true);
    expect(mockAudioAlert.speakToken).toHaveBeenCalledWith('9101');
    expect(component.newlyReadyToken()).toBe('9101');
  });

  it('should remove token from ready board when order is completed', () => {
    orderStatusUpdatedSubject.next({
      orderId: 203,
      orderNumber: 'ORD-20261103-9099',
      newStatus: 'Completed',
      updatedAt: new Date().toISOString(),
      canteenId: 1
    });

    expect(component.readyOrders().some(o => o.orderId === 203)).toBe(false);
  });

  it('should handle TV voice announcement and speak token', () => {
    tvVoiceAnnouncementSubject.next({
      canteenId: 1,
      orderId: 203,
      orderNumber: 'ORD-20261103-9099',
      tokenNumber: '9099',
      message: 'Token number 9099, please collect your food!',
      announcedAt: new Date().toISOString()
    });

    expect(mockAudioAlert.speakToken).toHaveBeenCalledWith('9099');
    expect(component.activeAnnouncement()).toBe('Token number 9099, please collect your food!');
  });

  it('should append digits via numpad and claim pairing code', () => {
    component.isPaired.set(false);
    component.clearCode();

    component.onNumpadClick(8);
    component.onNumpadClick(3);
    component.onNumpadClick(9);
    component.onNumpadClick(2);
    component.onNumpadClick(0);
    expect(component.pairingCode()).toBe('83920');

    // Enter 6th digit -> triggers auto-submit
    component.onNumpadClick(1);
    expect(component.pairingCode()).toBe('839201');
    expect(mockTvDeviceService.claimPairingCode).toHaveBeenCalledWith({
      pairingCode: '839201',
      deviceIdentifier: 'TV_DEVICE_UUID_123',
      deviceName: 'Smart TV Screen'
    });
    expect(mockTvDeviceService.setStoredDeviceToken).toHaveBeenCalledWith('test-device-token-64chars');
    expect(component.isPaired()).toBe(true);
  });

  it('should reset to pairing screen when TV device is revoked by kitchen manager', () => {
    component.isPaired.set(true);
    component.tvDeviceId.set(7);

    tvDeviceRevokedSubject.next({
      tvDeviceId: 7,
      canteenId: 1
    });

    expect(mockTvDeviceService.clearStoredDeviceToken).toHaveBeenCalled();
    expect(component.isPaired()).toBe(false);
    expect(component.pairingError()).toContain('disconnected by the kitchen manager');
  });
});
