import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Orders } from './orders';
import { OrderService } from '../../core/services/order.service';
import { PaymentService } from '../../core/services/payment.service';
import { SignalRService } from '../../core/services/signalr.service';
import { of } from 'rxjs';

describe('Orders', () => {
  let component: Orders;
  let fixture: ComponentFixture<Orders>;

  const mockOrderService = {
    getOrdersByCanteen: () => of([]),
    updateOrderStatus: () => of({ status: 'InPreparation' })
  };

  const mockPaymentService = {
    confirmCounterPayment: () => of({ status: 'Success' })
  };

  const mockSignalRService = {
    startConnection: () => Promise.resolve(),
    newOrder$: of(),
    paymentConfirmed$: of(),
    orderStatusUpdated$: of()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Orders],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: OrderService, useValue: mockOrderService },
        { provide: PaymentService, useValue: mockPaymentService },
        { provide: SignalRService, useValue: mockSignalRService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Orders);
    component = fixture.componentInstance;
    component.canteenId = 8;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
