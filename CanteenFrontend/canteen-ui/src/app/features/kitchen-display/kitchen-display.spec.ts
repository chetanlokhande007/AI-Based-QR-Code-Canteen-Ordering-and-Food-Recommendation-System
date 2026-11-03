import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, Subject } from 'rxjs';
import { KitchenDisplayComponent } from './kitchen-display';
import { OrderService } from '../../core/services/order.service';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService, NewOrderNotification, OrderStatusUpdatedNotification, PaymentConfirmedNotification } from '../../core/services/signalr.service';
import { AudioAlertService } from '../../core/services/audio-alert.service';
import { TvDeviceService } from '../../core/services/tv-device.service';
import { OrderDto } from '../../core/models/order.models';
import { TvDeviceDto, TvPairingSessionResponseDto } from '../../core/models/tv-device.models';

describe('KitchenDisplayComponent', () => {
  let component: KitchenDisplayComponent;
  let fixture: ComponentFixture<KitchenDisplayComponent>;
  let mockOrderService: any;
  let mockAuthService: any;
  let mockSignalRService: any;
  let mockAudioAlert: any;
  let mockTvDeviceService: any;

  const newOrderSubject = new Subject<NewOrderNotification>();
  const orderStatusUpdatedSubject = new Subject<OrderStatusUpdatedNotification>();
  const paymentConfirmedSubject = new Subject<PaymentConfirmedNotification>();

  const sampleOrders: OrderDto[] = [
    {
      orderId: 101,
      canteenId: 1,
      tableId: 3,
      orderNumber: 'ORD-20261103-8491',
      status: 'Pending',
      paymentStatus: 'Pending',
      totalAmount: 120,
      trackingToken: 'tok101',
      createdAt: new Date().toISOString(),
      items: [
        { productId: 1, productName: 'Masala Dosa', quantity: 2, unitPrice: 50, subtotal: 100 },
        { productId: 2, productName: 'Chai', quantity: 1, unitPrice: 20, subtotal: 20 }
      ]
    },
    {
      orderId: 102,
      canteenId: 1,
      tableId: 0,
      orderNumber: 'ORD-20261103-8492',
      status: 'InPreparation',
      paymentStatus: 'Paid',
      totalAmount: 90,
      trackingToken: 'tok102',
      createdAt: new Date(Date.now() - 360000).toISOString(),
      items: [
        { productId: 3, productName: 'Paneer Wrap', quantity: 1, unitPrice: 90, subtotal: 90 }
      ]
    },
    {
      orderId: 103,
      canteenId: 1,
      tableId: 5,
      orderNumber: 'ORD-20261103-8493',
      status: 'Ready',
      paymentStatus: 'Paid',
      totalAmount: 40,
      trackingToken: 'tok103',
      createdAt: new Date(Date.now() - 720000).toISOString(),
      items: [
        { productId: 4, productName: 'Cold Coffee', quantity: 1, unitPrice: 40, subtotal: 40 }
      ]
    }
  ];

  const sampleTvDevices: TvDeviceDto[] = [
    {
      tvDeviceId: 1,
      canteenId: 1,
      deviceName: 'Dining Hall TV',
      deviceIdentifier: 'TV_ID_1',
      stationScope: 'All',
      isActive: true,
      registeredAt: new Date().toISOString()
    }
  ];

  const samplePairingSession: TvPairingSessionResponseDto = {
    sessionId: 10,
    canteenId: 1,
    pairingCode: '582910',
    stationScope: 'All',
    expiresAt: new Date(Date.now() + 300000).toISOString(),
    expiresInSeconds: 300
  };

  beforeEach(async () => {
    mockOrderService = {
      getOrdersByCanteen: vi.fn().mockReturnValue(of(sampleOrders)),
      updateOrderStatus: vi.fn().mockReturnValue(of({
        ...sampleOrders[0],
        status: 'InPreparation'
      })),
      announceOrderOnTv: vi.fn().mockReturnValue(of({ message: 'Success', tokenNumber: '8493' }))
    };

    mockAuthService = {
      currentUser: signal({
        id: 1,
        name: 'Test Owner',
        email: 'owner@test.com',
        role: 'CanteenOwner',
        canteenId: 1
      })
    };

    mockAudioAlert = {
      playNewOrderChime: vi.fn(),
      playOrderReadyChime: vi.fn(),
      playUrgentAlert: vi.fn(),
      speakToken: vi.fn(),
      toggleAudio: vi.fn(),
      toggleVoice: vi.fn(),
      audioEnabled: signal(true),
      voiceEnabled: signal(true)
    };

    mockTvDeviceService = {
      getDevicesByCanteen: vi.fn().mockReturnValue(of(sampleTvDevices)),
      createPairingSession: vi.fn().mockReturnValue(of(samplePairingSession)),
      revokeDevice: vi.fn().mockReturnValue(of({ message: 'Success' }))
    };

    mockSignalRService = {
      startConnection: vi.fn().mockReturnValue(Promise.resolve()),
      newOrder$: newOrderSubject.asObservable(),
      orderStatusUpdated$: orderStatusUpdatedSubject.asObservable(),
      paymentConfirmed$: paymentConfirmedSubject.asObservable()
    };

    await TestBed.configureTestingModule({
      imports: [KitchenDisplayComponent],
      providers: [
        { provide: OrderService, useValue: mockOrderService },
        { provide: AuthService, useValue: mockAuthService },
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: AudioAlertService, useValue: mockAudioAlert },
        { provide: TvDeviceService, useValue: mockTvDeviceService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(KitchenDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the Kitchen Display component', () => {
    expect(component).toBeTruthy();
  });

  it('should load tickets on init and extract token numbers correctly (FIFO sorted)', () => {
    expect(component.tickets().length).toBe(3);
    expect(component.pendingCount()).toBe(1);
    expect(component.preparingCount()).toBe(1);
    expect(component.readyCount()).toBe(1);
    // Oldest order (103, 12 mins ago) comes first in FIFO
    expect(component.tickets()[0].tokenNumber).toBe('8493');
    expect(component.tickets()[2].tokenNumber).toBe('8491');
  });

  it('should filter tickets when tab is changed', () => {
    component.selectedTab.set('Pending');
    expect(component.filteredTickets().length).toBe(1);
    expect(component.filteredTickets()[0].orderId).toBe(101);

    component.selectedTab.set('Preparing');
    expect(component.filteredTickets().length).toBe(1);
    expect(component.filteredTickets()[0].orderId).toBe(102);

    component.selectedTab.set('Ready');
    expect(component.filteredTickets().length).toBe(1);
    expect(component.filteredTickets()[0].orderId).toBe(103);
  });

  it('should toggle plating items checklist state', () => {
    expect(component.isItemPlated(101, 1)).toBe(false);
    component.toggleItemPlated(101, 1);
    expect(component.isItemPlated(101, 1)).toBe(true);
    component.toggleItemPlated(101, 1);
    expect(component.isItemPlated(101, 1)).toBe(false);
  });

  it('should advance order status to InPreparation on startCooking', () => {
    const ticket = component.tickets().find(t => t.orderId === 101)!;
    component.startCooking(ticket);

    expect(mockOrderService.updateOrderStatus).toHaveBeenCalledWith(1, 101, 'InPreparation');
  });

  it('should advance order status to Ready and speak token on markReady', () => {
    const ticket = component.tickets().find(t => t.orderId === 102)!;
    mockOrderService.updateOrderStatus.mockReturnValue(of({
      ...ticket,
      status: 'Ready'
    }));

    component.markReady(ticket);

    expect(mockOrderService.updateOrderStatus).toHaveBeenCalledWith(1, 102, 'Ready');
    expect(mockAudioAlert.speakToken).toHaveBeenCalledWith('8492');
  });

  it('should complete order and remove it from active queue on bumpComplete', () => {
    const ticket = component.tickets().find(t => t.orderId === 103)!;
    mockOrderService.updateOrderStatus.mockReturnValue(of({
      ...ticket,
      status: 'Completed'
    }));

    component.bumpComplete(ticket);

    expect(mockOrderService.updateOrderStatus).toHaveBeenCalledWith(1, 103, 'Completed');
    expect(component.tickets().some(t => t.orderId === 103)).toBe(false);
  });

  it('should play chime and reload tickets on new order SignalR event', () => {
    newOrderSubject.next({
      orderId: 104,
      orderNumber: 'ORD-20261103-8494',
      canteenId: 1,
      tableId: 2,
      status: 'Pending',
      totalAmount: 150,
      createdAt: new Date().toISOString(),
      items: []
    });

    expect(mockAudioAlert.playNewOrderChime).toHaveBeenCalled();
  });

  it('should open and close TV management modal and load paired devices', () => {
    component.openTvModal();
    expect(component.showTvModal()).toBe(true);
    expect(mockTvDeviceService.getDevicesByCanteen).toHaveBeenCalledWith(1);
    expect(component.tvDevices().length).toBe(1);

    component.closeTvModal();
    expect(component.showTvModal()).toBe(false);
  });

  it('should generate a 6-digit TV pairing code with countdown', () => {
    component.newTvName.set('Beverage Counter TV');
    component.newTvStation.set('Beverage');

    component.generatePairingCode();

    expect(mockTvDeviceService.createPairingSession).toHaveBeenCalledWith(1, {
      deviceName: 'Beverage Counter TV',
      stationScope: 'Beverage'
    });
    expect(component.activePairingSession()?.pairingCode).toBe('582910');
    expect(component.formattedPairingCountdown()).toBe('05:00');
  });

  it('should revoke a connected TV device', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    component.revokeTvDevice(sampleTvDevices[0]);

    expect(mockTvDeviceService.revokeDevice).toHaveBeenCalledWith(1, 1);
  });
});
