import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import * as echarts from 'echarts';
import type { EChartsOption } from 'echarts';
import { AuthService } from '../../core/services/auth.service';
import { ReportsService } from '../../core/services/reports.service';
import { SignalRService } from '../../core/services/signalr.service';
import { EchartsDirective } from '../../shared/directives/echarts.directive';
import {
  ReportFilter,
  OverviewReport,
  SalesReport,
  OrderReport,
  ProductReport,
  PaymentReport,
  InventoryReport,
  CouponReport,
  StaffReport,
  StaffAttendanceReport,
  KitchenReport,
  PeakHourReport
} from '../../core/models/reports.models';

export type ReportTab =
  | 'overview'
  | 'sales'
  | 'orders'
  | 'products'
  | 'payments'
  | 'inventory'
  | 'coupons'
  | 'staff'
  | 'attendance'
  | 'kitchen'
  | 'peak-hours';

export type PeriodPreset =
  | 'today'
  | 'yesterday'
  | 'this_week'
  | 'last_week'
  | 'this_month'
  | 'last_month'
  | 'custom';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, EchartsDirective],
  templateUrl: './reports.html',
  styleUrls: ['./reports.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReportsComponent implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly reportsService = inject(ReportsService);
  private readonly signalRService = inject(SignalRService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;

  // Active Canteen Resolution
  get effectiveCanteenId(): number | null {
    const qId = this.route.snapshot.queryParams['canteenId'];
    if (qId) return Number(qId);
    return this.currentUser()?.canteenId ?? null;
  }

  // Active Tab & Filter State
  readonly activeTab = signal<ReportTab>('overview');
  readonly selectedPeriod = signal<PeriodPreset>('this_month');
  readonly customStartDate = signal<string>('');
  readonly customEndDate = signal<string>('');
  readonly searchFilter = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(10);

  // Loading & Error States
  readonly isLoading = signal<boolean>(false);
  readonly isExporting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);

  // Report Data Signals
  readonly overviewData = signal<OverviewReport | null>(null);
  readonly salesData = signal<SalesReport | null>(null);
  readonly orderData = signal<OrderReport | null>(null);
  readonly productData = signal<ProductReport | null>(null);
  readonly paymentData = signal<PaymentReport | null>(null);
  readonly inventoryData = signal<InventoryReport | null>(null);
  readonly couponData = signal<CouponReport | null>(null);
  readonly staffData = signal<StaffReport | null>(null);
  readonly attendanceData = signal<StaffAttendanceReport | null>(null);
  readonly kitchenData = signal<KitchenReport | null>(null);
  readonly peakHourData = signal<PeakHourReport | null>(null);

  // Subscriptions for SignalR & route changes
  private readonly subscriptions = new Subscription();

  readonly periods: { label: string; value: PeriodPreset }[] = [
    { label: 'Today', value: 'today' },
    { label: 'Yesterday', value: 'yesterday' },
    { label: 'This Week', value: 'this_week' },
    { label: 'Last Week', value: 'last_week' },
    { label: 'This Month', value: 'this_month' },
    { label: 'Last Month', value: 'last_month' },
    { label: 'Custom', value: 'custom' }
  ];

  readonly tabs: { id: ReportTab; label: string; icon: string }[] = [
    { id: 'overview', label: 'Executive Overview', icon: 'dashboard' },
    { id: 'sales', label: 'Sales & Revenue', icon: 'payments' },
    { id: 'orders', label: 'Orders & Fulfillment', icon: 'receipt_long' },
    { id: 'products', label: 'Food Items Performance', icon: 'restaurant' },
    { id: 'payments', label: 'Collections & Settlements', icon: 'account_balance_wallet' },
    { id: 'inventory', label: 'Inventory & Wastage', icon: 'inventory_2' },
    { id: 'coupons', label: 'Offers & Coupons', icon: 'local_offer' },
    { id: 'staff', label: 'Staff & Payroll', icon: 'groups' },
    { id: 'attendance', label: 'Attendance Records', icon: 'schedule' },
    { id: 'kitchen', label: 'Kitchen Velocity', icon: 'microwave' },
    { id: 'peak-hours', label: 'Peak Hours & Traffic', icon: 'timelapse' }
  ];

  ngOnInit(): void {
    // Check query params for tab deep linking
    const tabParam = this.route.snapshot.queryParams['tab'] as ReportTab;
    if (tabParam && this.tabs.some(t => t.id === tabParam)) {
      this.activeTab.set(tabParam);
    }

    this.loadActiveReport();
    this.setupSignalRRealtime();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private setupSignalRRealtime(): void {
    const canteenId = this.effectiveCanteenId;
    if (canteenId) {
      this.signalRService.startConnection();

      // When a new order or payment occurs, refresh if viewing real-time intervals
      const orderSub = this.signalRService.newOrder$.subscribe(notif => {
        if (notif.canteenId === canteenId && (this.selectedPeriod() === 'today' || this.selectedPeriod() === 'this_week')) {
          this.loadActiveReport(true);
        }
      });
      const paySub = this.signalRService.paymentConfirmed$.subscribe(notif => {
        if (notif.canteenId === canteenId && (this.selectedPeriod() === 'today' || this.selectedPeriod() === 'this_week')) {
          this.loadActiveReport(true);
        }
      });
      const statusSub = this.signalRService.orderStatusUpdated$.subscribe(notif => {
        if (notif.canteenId === canteenId && (this.activeTab() === 'kitchen' || this.activeTab() === 'orders')) {
          this.loadActiveReport(true);
        }
      });

      this.subscriptions.add(orderSub);
      this.subscriptions.add(paySub);
      this.subscriptions.add(statusSub);
    }
  }

  selectTab(tab: ReportTab): void {
    if (this.activeTab() === tab) return;
    this.activeTab.set(tab);
    this.currentPage.set(1);
    this.searchFilter.set('');
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
    this.loadActiveReport();
  }

  setPeriod(period: PeriodPreset): void {
    this.selectedPeriod.set(period);
    this.currentPage.set(1);
    if (period !== 'custom') {
      this.loadActiveReport();
    }
  }

  applyCustomDateRange(): void {
    if (this.customStartDate() && this.customEndDate()) {
      this.loadActiveReport();
    }
  }

  onSearchChange(): void {
    this.currentPage.set(1);
    this.loadActiveReport();
  }

  changePage(newPage: number): void {
    if (newPage < 1) return;
    this.currentPage.set(newPage);
    this.loadActiveReport();
  }

  loadActiveReport(silent: boolean = false): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) {
      this.errorMessage.set('No active canteen selected. Please set up or select a canteen.');
      return;
    }

    if (!silent) {
      this.isLoading.set(true);
    }
    this.errorMessage.set(null);

    const filter: ReportFilter = {
      canteenId,
      period: this.selectedPeriod(),
      startDate: this.selectedPeriod() === 'custom' ? this.customStartDate() : undefined,
      endDate: this.selectedPeriod() === 'custom' ? this.customEndDate() : undefined,
      page: this.currentPage(),
      pageSize: this.pageSize(),
      search: this.searchFilter().trim() || undefined
    };

    switch (this.activeTab()) {
      case 'overview':
        this.reportsService.getOverview(filter).subscribe({
          next: data => {
            this.overviewData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'sales':
        this.reportsService.getSales(filter).subscribe({
          next: data => {
            this.salesData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'orders':
        this.reportsService.getOrders(filter).subscribe({
          next: data => {
            this.orderData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'products':
        this.reportsService.getProducts(filter).subscribe({
          next: data => {
            this.productData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'payments':
        this.reportsService.getPayments(filter).subscribe({
          next: data => {
            this.paymentData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'inventory':
        this.reportsService.getInventory(filter).subscribe({
          next: data => {
            this.inventoryData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'coupons':
        this.reportsService.getCoupons(filter).subscribe({
          next: data => {
            this.couponData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'staff':
        this.reportsService.getStaff(filter).subscribe({
          next: data => {
            this.staffData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'attendance':
        this.reportsService.getAttendance(filter).subscribe({
          next: data => {
            this.attendanceData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'kitchen':
        this.reportsService.getKitchen(filter).subscribe({
          next: data => {
            this.kitchenData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;

      case 'peak-hours':
        this.reportsService.getPeakHours(filter).subscribe({
          next: data => {
            this.peakHourData.set(data);
            this.isLoading.set(false);
          },
          error: err => this.handleError(err)
        });
        break;
    }
  }

  private handleError(err: any): void {
    this.isLoading.set(false);
    this.isExporting.set(false);
    const msg = err?.error?.message || err?.message || 'Failed to load report analytics. Please try again.';
    this.errorMessage.set(msg);
  }

  exportCurrentReport(format: 'csv' | 'excel'): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isExporting.set(true);
    this.reportsService.exportReport({
      canteenId,
      reportType: this.activeTab(),
      format,
      period: this.selectedPeriod(),
      startDate: this.selectedPeriod() === 'custom' ? this.customStartDate() : undefined,
      endDate: this.selectedPeriod() === 'custom' ? this.customEndDate() : undefined,
      search: this.searchFilter().trim() || undefined
    }).subscribe({
      next: (blob: Blob) => {
        this.isExporting.set(false);
        const filename = `${this.activeTab()}_report_${new Date().toISOString().slice(0, 10)}.${format === 'excel' ? 'xls' : 'csv'}`;
        this.reportsService.triggerBlobDownload(blob, filename);
      },
      error: err => this.handleError(err)
    });
  }

  // ========================================================================
  // Professional Interactive ECharts Reactive Computations
  // ========================================================================

  /**
   * 1. Daily Sales Trend (Area Chart with Net Earnings Comparison)
   */
  readonly dailySalesTrendOption = computed<EChartsOption | null>(() => {
    const data = this.salesData()?.dailyTimeline;
    if (!data || data.length === 0) return null;

    const labels = data.map(d => d.dateLabel);
    const grossSales = data.map(d => d.grossSales);
    const netSales = data.map(d => d.netSales);

    return {
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => {
          if (!Array.isArray(params) || params.length === 0) return '';
          const idx = params[0].dataIndex;
          const item = data[idx];
          return `
            <div style="font-weight: 700; margin-bottom: 4px; color: #94A3B8;">${item.dateLabel}</div>
            <div style="display: flex; justify-content: space-between; gap: 16px; margin: 2px 0;">
              <span style="color: #60A5FA;">● Gross Sales:</span>
              <strong>₹${item.grossSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong>
            </div>
            <div style="display: flex; justify-content: space-between; gap: 16px; margin: 2px 0;">
              <span style="color: #34D399;">● Net Earnings:</span>
              <strong>₹${item.netSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong>
            </div>
            <div style="display: flex; justify-content: space-between; gap: 16px; margin: 2px 0; color: #CBD5E1;">
              <span>Orders:</span>
              <strong>${item.orderCount}</strong>
            </div>
          `;
        }
      },
      legend: {
        data: ['Gross Sales', 'Net Earnings'],
        right: '2%',
        top: '0%',
        icon: 'circle',
        textStyle: { color: '#64748B', fontSize: 12 }
      },
      grid: {
        left: '2%',
        right: '3%',
        top: '14%',
        bottom: '8%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: labels,
        axisLine: { lineStyle: { color: '#CBD5E1' } },
        axisLabel: { color: '#64748B', fontSize: 11 }
      },
      yAxis: {
        type: 'value',
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#F1F5F9', type: 'dashed' } },
        axisLabel: {
          color: '#64748B',
          fontSize: 11,
          formatter: (val: number) => '₹' + (val >= 1000 ? (val / 1000).toFixed(1) + 'k' : val)
        }
      },
      series: [
        {
          name: 'Gross Sales',
          type: 'line',
          smooth: true,
          showSymbol: false,
          lineStyle: { width: 3, color: '#2563EB' },
          itemStyle: { color: '#2563EB' },
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(37, 99, 235, 0.35)' },
              { offset: 1, color: 'rgba(37, 99, 235, 0.01)' }
            ])
          },
          data: grossSales
        },
        {
          name: 'Net Earnings',
          type: 'line',
          smooth: true,
          showSymbol: false,
          lineStyle: { width: 2.5, color: '#10B981', type: 'dashed' },
          itemStyle: { color: '#10B981' },
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(16, 185, 129, 0.2)' },
              { offset: 1, color: 'rgba(16, 185, 129, 0.0)' }
            ])
          },
          data: netSales
        }
      ]
    };
  });

  /**
   * 2. 24-Hour Order Traffic Heatmap (24 Hourly Cells with Intensity Scale)
   */
  readonly hourlyTrafficHeatmapOption = computed<EChartsOption | null>(() => {
    const data = this.peakHourData()?.hourlyTraffic;
    if (!data || data.length === 0) return null;

    const hours = data.map(h => h.hourLabel);
    const maxOrders = Math.max(...data.map(h => h.ordersCount), 1);
    const heatmapData = data.map((h, idx) => [idx, 0, h.ordersCount]);

    return {
      tooltip: {
        position: 'top',
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => {
          const idx = params.data[0];
          const h = data[idx];
          return `
            <div style="font-weight: 700; color: #F59E0B; margin-bottom: 4px;">${h.hourLabel} ${h.isPeakHour ? '★ Peak Rush' : ''}</div>
            <div style="margin: 2px 0;">Orders: <strong>${h.ordersCount}</strong> (${h.trafficPercentage}% of day)</div>
            <div style="margin: 2px 0;">Gross Revenue: <strong>₹${h.grossSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong></div>
            <div style="margin: 2px 0; color: #94A3B8;">Avg Ticket: <strong>₹${h.averageOrderValue.toFixed(2)}</strong></div>
          `;
        }
      },
      grid: {
        top: '6%',
        bottom: '22%',
        left: '2%',
        right: '2%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        data: hours,
        splitArea: { show: true },
        axisLabel: {
          interval: 1,
          color: '#64748B',
          fontSize: 10.5,
          rotate: 30
        },
        axisLine: { lineStyle: { color: '#E2E8F0' } }
      },
      yAxis: {
        type: 'category',
        data: ['Traffic'],
        show: false
      },
      visualMap: {
        min: 0,
        max: maxOrders,
        calculable: false,
        orient: 'horizontal',
        left: 'center',
        bottom: '0%',
        itemWidth: 14,
        itemHeight: 120,
        text: ['Peak', 'Quiet'],
        textStyle: { color: '#64748B', fontSize: 11 },
        inRange: {
          color: ['#F1F5F9', '#BAE6FD', '#3B82F6', '#F59E0B', '#EA580C']
        }
      },
      series: [
        {
          name: 'Hourly Orders',
          type: 'heatmap',
          data: heatmapData,
          label: {
            show: true,
            formatter: (p: any) => p.data[2] > 0 ? String(p.data[2]) : '',
            color: '#0F172A',
            fontWeight: 700,
            fontSize: 11
          },
          itemStyle: {
            borderColor: '#FFFFFF',
            borderWidth: 2,
            borderRadius: 4
          }
        }
      ]
    };
  });

  /**
   * 3. Day of the Week Traffic (Horizontal Bar Chart)
   */
  readonly dayOfWeekTrafficOption = computed<EChartsOption | null>(() => {
    const raw = this.peakHourData()?.dayOfWeekTraffic;
    if (!raw || raw.length === 0) return null;

    const data = [...raw].reverse();
    const dayNames = data.map(d => d.dayName);
    const orderCounts = data.map(d => d.ordersCount);
    const maxOrder = Math.max(...orderCounts, 1);

    return {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => {
          if (!Array.isArray(params) || params.length === 0) return '';
          const idx = params[0].dataIndex;
          const d = data[idx];
          return `
            <div style="font-weight: 700; color: #60A5FA; margin-bottom: 4px;">${d.dayName}</div>
            <div style="margin: 2px 0;">Orders: <strong>${d.ordersCount}</strong></div>
            <div style="margin: 2px 0;">Revenue: <strong>₹${d.grossSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong></div>
            <div style="margin: 2px 0; color: #94A3B8;">Traffic Share: <strong>${d.percentageOfTotal}%</strong></div>
          `;
        }
      },
      grid: {
        top: '6%',
        bottom: '6%',
        left: '3%',
        right: '14%',
        containLabel: true
      },
      xAxis: {
        type: 'value',
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#F1F5F9', type: 'dashed' } },
        axisLabel: { color: '#64748B', fontSize: 11 }
      },
      yAxis: {
        type: 'category',
        data: dayNames,
        axisLine: { lineStyle: { color: '#CBD5E1' } },
        axisTick: { show: false },
        axisLabel: { color: '#334155', fontWeight: 600, fontSize: 12 }
      },
      series: [
        {
          name: 'Orders',
          type: 'bar',
          barWidth: 16,
          data: orderCounts.map((val) => ({
            value: val,
            itemStyle: {
              color: val === maxOrder
                ? new echarts.graphic.LinearGradient(0, 0, 1, 0, [
                    { offset: 0, color: '#2563EB' },
                    { offset: 1, color: '#3B82F6' }
                  ])
                : new echarts.graphic.LinearGradient(0, 0, 1, 0, [
                    { offset: 0, color: '#93C5FD' },
                    { offset: 1, color: '#BFDBFE' }
                  ]),
              borderRadius: [0, 8, 8, 0]
            }
          })),
          label: {
            show: true,
            position: 'right',
            formatter: '{c} orders',
            color: '#475569',
            fontSize: 11,
            fontWeight: 600
          }
        }
      ]
    };
  });

  /**
   * 4. Dining Table Turnover & Revenue (Combo Bar + Line Chart)
   */
  readonly tableTurnoverComboOption = computed<EChartsOption | null>(() => {
    const data = this.peakHourData()?.tableUtilization;
    if (!data || data.length === 0) return null;

    const tableLabels = data.map(t => 'T-' + t.tableNumber);
    const orders = data.map(t => t.totalOrdersServed);
    const revenues = data.map(t => t.totalRevenueGenerated);

    return {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'cross' },
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => {
          if (!Array.isArray(params) || params.length === 0) return '';
          const idx = params[0].dataIndex;
          const t = data[idx];
          return `
            <div style="font-weight: 700; color: #94A3B8; margin-bottom: 4px;">Table ${t.tableNumber}</div>
            <div style="margin: 2px 0; color: #60A5FA;">● Orders Served: <strong>${t.totalOrdersServed}</strong></div>
            <div style="margin: 2px 0; color: #34D399;">● Revenue: <strong>₹${t.totalRevenueGenerated.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong></div>
            <div style="margin: 2px 0; color: #CBD5E1;">Turnover Share: <strong>${t.utilizationPercentage}%</strong></div>
          `;
        }
      },
      legend: {
        data: ['Orders Served', 'Revenue Generated'],
        right: '2%',
        top: '0%',
        icon: 'circle',
        textStyle: { color: '#64748B', fontSize: 12 }
      },
      grid: {
        left: '3%',
        right: '5%',
        top: '14%',
        bottom: '10%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        data: tableLabels,
        axisLine: { lineStyle: { color: '#CBD5E1' } },
        axisLabel: { color: '#64748B', fontSize: 11, rotate: tableLabels.length > 8 ? 30 : 0 }
      },
      yAxis: [
        {
          type: 'value',
          name: 'Orders',
          nameTextStyle: { color: '#64748B', fontSize: 11 },
          axisLine: { show: false },
          splitLine: { lineStyle: { color: '#F1F5F9', type: 'dashed' } },
          axisLabel: { color: '#64748B', fontSize: 11 }
        },
        {
          type: 'value',
          name: 'Revenue',
          nameTextStyle: { color: '#64748B', fontSize: 11 },
          axisLine: { show: false },
          splitLine: { show: false },
          axisLabel: {
            color: '#10B981',
            fontSize: 11,
            formatter: (v: number) => '₹' + (v >= 1000 ? (v / 1000).toFixed(1) + 'k' : v)
          }
        }
      ],
      series: [
        {
          name: 'Orders Served',
          type: 'bar',
          barWidth: 20,
          itemStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: '#3B82F6' },
              { offset: 1, color: '#60A5FA' }
            ]),
            borderRadius: [6, 6, 0, 0]
          },
          data: orders
        },
        {
          name: 'Revenue Generated',
          type: 'line',
          yAxisIndex: 1,
          smooth: true,
          symbol: 'circle',
          symbolSize: 8,
          itemStyle: { color: '#10B981' },
          lineStyle: { width: 3, color: '#10B981' },
          data: revenues
        }
      ]
    };
  });

  /**
   * 5. Revenue Trajectory (Smooth Line with Gradient Area)
   */
  readonly revenueTrajectoryOption = computed<EChartsOption | null>(() => {
    const data = this.overviewData()?.revenueTrend;
    if (!data || data.length === 0) return null;

    const dates = data.map(d => d.dateLabel);
    const grossSales = data.map(d => d.grossSales);
    const netSales = data.map(d => d.netSales);

    return {
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => {
          if (!Array.isArray(params) || params.length === 0) return '';
          const idx = params[0].dataIndex;
          const item = data[idx];
          return `
            <div style="font-weight: 700; color: #94A3B8; margin-bottom: 4px;">${item.dateLabel}</div>
            <div style="margin: 2px 0; color: #60A5FA;">● Gross Revenue: <strong>₹${item.grossSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong></div>
            <div style="margin: 2px 0; color: #34D399;">● Net Revenue: <strong>₹${item.netSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong></div>
            <div style="margin: 2px 0; color: #CBD5E1;">Order Volume: <strong>${item.orderCount} orders</strong></div>
          `;
        }
      },
      legend: {
        data: ['Gross Revenue', 'Net Revenue'],
        right: '2%',
        top: '0%',
        icon: 'circle',
        textStyle: { color: '#64748B', fontSize: 12 }
      },
      grid: {
        left: '2%',
        right: '3%',
        top: '14%',
        bottom: '8%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: dates,
        axisLine: { lineStyle: { color: '#CBD5E1' } },
        axisLabel: { color: '#64748B', fontSize: 11 }
      },
      yAxis: {
        type: 'value',
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#F1F5F9', type: 'dashed' } },
        axisLabel: {
          color: '#64748B',
          fontSize: 11,
          formatter: (v: number) => '₹' + (v >= 1000 ? (v / 1000).toFixed(1) + 'k' : v)
        }
      },
      series: [
        {
          name: 'Gross Revenue',
          type: 'line',
          smooth: true,
          symbol: 'circle',
          symbolSize: 6,
          lineStyle: { width: 3, color: '#2563EB' },
          itemStyle: { color: '#2563EB' },
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(37, 99, 235, 0.35)' },
              { offset: 1, color: 'rgba(37, 99, 235, 0.01)' }
            ])
          },
          data: grossSales
        },
        {
          name: 'Net Revenue',
          type: 'line',
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 2, color: '#10B981', type: 'dashed' },
          itemStyle: { color: '#10B981' },
          data: netSales
        }
      ]
    };
  });

  /**
   * 6. Order Status Distribution (Donut Chart with Centered Total)
   */
  readonly orderStatusDonutOption = computed<EChartsOption | null>(() => {
    const breakdown = this.overviewData()?.orderStatusBreakdown || this.orderData()?.statusBreakdown;
    if (!breakdown || breakdown.length === 0) return null;

    const colorMap: Record<string, string> = {
      'Completed': '#10B981',
      'Ready': '#34D399',
      'Preparing': '#F59E0B',
      'Pending': '#3B82F6',
      'Cancelled': '#EF4444'
    };

    const total = breakdown.reduce((acc, curr) => acc + curr.count, 0);

    const chartData = breakdown.map(item => ({
      name: item.label,
      value: item.count,
      itemStyle: {
        color: colorMap[item.label] || '#8B5CF6',
        borderRadius: 6,
        borderColor: '#FFFFFF',
        borderWidth: 2
      }
    }));

    return {
      tooltip: {
        trigger: 'item',
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => `
          <div style="font-weight: 700; color: ${params.color}; margin-bottom: 2px;">${params.name}</div>
          <div>Orders: <strong>${params.value}</strong> (${params.percent}%)</div>
        `
      },
      legend: {
        orient: 'horizontal',
        bottom: '0%',
        icon: 'circle',
        textStyle: { color: '#64748B', fontSize: 12 },
        formatter: (name: string) => {
          const item = breakdown.find(b => b.label === name);
          return `${name} (${item ? item.count : 0})`;
        }
      },
      title: {
        text: `${total}`,
        subtext: 'Total Orders',
        left: 'center',
        top: '36%',
        textStyle: { fontSize: 24, fontWeight: 700, color: '#0F172A' },
        subtextStyle: { fontSize: 12, color: '#94A3B8' }
      },
      series: [
        {
          name: 'Order Status',
          type: 'pie',
          radius: ['52%', '76%'],
          center: ['50%', '45%'],
          avoidLabelOverlap: false,
          label: { show: false },
          emphasis: {
            scale: true,
            scaleSize: 6,
            label: {
              show: true,
              fontSize: 13,
              fontWeight: 700,
              formatter: '{b}\n{d}%'
            }
          },
          data: chartData
        }
      ]
    };
  });

  /**
   * 7. Payment Methods Donut Chart (Collections by Payment Method)
   */
  readonly paymentMethodsDonutOption = computed<EChartsOption | null>(() => {
    const breakdown = this.salesData()?.paymentMethodSummary || this.paymentData()?.methodsSummary;
    if (!breakdown || breakdown.length === 0) return null;

    const colorMap: Record<string, string> = {
      'Razorpay': '#10B981',
      'Online': '#10B981',
      'UPI': '#06B6D4',
      'Counter Cash': '#3B82F6',
      'Cash': '#3B82F6',
      'Card': '#8B5CF6'
    };

    const chartData = breakdown.map((item: any) => ({
      name: item.paymentMethod,
      value: item.grossAmount ?? item.totalCollected ?? 0,
      itemStyle: {
        color: colorMap[item.paymentMethod] || '#F59E0B',
        borderRadius: 6,
        borderColor: '#FFFFFF',
        borderWidth: 2
      }
    }));

    const total = chartData.reduce((acc, c) => acc + c.value, 0);

    return {
      tooltip: {
        trigger: 'item',
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => `
          <div style="font-weight: 700; color: ${params.color}; margin-bottom: 2px;">${params.name}</div>
          <div>Collected: <strong>₹${params.value.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong> (${params.percent}%)</div>
        `
      },
      legend: {
        orient: 'horizontal',
        bottom: '0%',
        icon: 'circle',
        textStyle: { color: '#64748B', fontSize: 11.5 }
      },
      title: {
        text: '₹' + (total >= 1000 ? (total / 1000).toFixed(1) + 'k' : total.toFixed(0)),
        subtext: 'Total Collections',
        left: 'center',
        top: '36%',
        textStyle: { fontSize: 22, fontWeight: 700, color: '#0F172A' },
        subtextStyle: { fontSize: 11.5, color: '#94A3B8' }
      },
      series: [
        {
          name: 'Payment Methods',
          type: 'pie',
          radius: ['52%', '76%'],
          center: ['50%', '45%'],
          avoidLabelOverlap: false,
          label: { show: false },
          emphasis: {
            scale: true,
            scaleSize: 6
          },
          data: chartData
        }
      ]
    };
  });

  /**
   * 8. Category Sales Bar Chart
   */
  readonly categorySalesBarOption = computed<EChartsOption | null>(() => {
    const categories = this.salesData()?.categorySales;
    if (!categories || categories.length === 0) return null;

    const names = categories.map(c => c.categoryName);
    const sales = categories.map(c => c.totalSales);

    return {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => {
          if (!Array.isArray(params) || params.length === 0) return '';
          const idx = params[0].dataIndex;
          const c = categories[idx];
          return `
            <div style="font-weight: 700; color: #60A5FA; margin-bottom: 2px;">${c.categoryName}</div>
            <div>Revenue: <strong>₹${c.totalSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong> (${c.sharePercentage}%)</div>
            <div>Units Sold: <strong>${c.quantitySold}</strong></div>
          `;
        }
      },
      grid: {
        left: '3%',
        right: '4%',
        top: '8%',
        bottom: '8%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        data: names,
        axisLine: { lineStyle: { color: '#CBD5E1' } },
        axisLabel: { color: '#64748B', fontSize: 11 }
      },
      yAxis: {
        type: 'value',
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#F1F5F9', type: 'dashed' } },
        axisLabel: {
          color: '#64748B',
          fontSize: 11,
          formatter: (v: number) => '₹' + (v >= 1000 ? (v / 1000).toFixed(1) + 'k' : v)
        }
      },
      series: [
        {
          name: 'Category Revenue',
          type: 'bar',
          barWidth: 24,
          itemStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: '#3B82F6' },
              { offset: 1, color: '#60A5FA' }
            ]),
            borderRadius: [6, 6, 0, 0]
          },
          data: sales
        }
      ]
    };
  });

  /**
   * 9. Top Selling Dishes Horizontal Bar Chart
   */
  readonly topProductsBarOption = computed<EChartsOption | null>(() => {
    const prods = this.overviewData()?.topSellingProducts;
    if (!prods || prods.length === 0) return null;

    const list = [...prods].reverse();
    const names = list.map(p => p.name);
    const quantities = list.map(p => p.quantitySold);

    return {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        backgroundColor: 'rgba(15, 23, 42, 0.95)',
        borderColor: '#334155',
        textStyle: { color: '#F8FAFC', fontSize: 12 },
        formatter: (params: any) => {
          if (!Array.isArray(params) || params.length === 0) return '';
          const idx = params[0].dataIndex;
          const p = list[idx];
          return `
            <div style="font-weight: 700; color: #34D399; margin-bottom: 2px;">${p.name}</div>
            <div>Category: <strong>${p.categoryName}</strong></div>
            <div>Quantity Sold: <strong>${p.quantitySold} units</strong></div>
            <div>Total Sales: <strong>₹${p.totalSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong></div>
          `;
        }
      },
      grid: {
        left: '3%',
        right: '12%',
        top: '6%',
        bottom: '6%',
        containLabel: true
      },
      xAxis: {
        type: 'value',
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#F1F5F9', type: 'dashed' } },
        axisLabel: { color: '#64748B', fontSize: 11 }
      },
      yAxis: {
        type: 'category',
        data: names,
        axisLine: { lineStyle: { color: '#CBD5E1' } },
        axisTick: { show: false },
        axisLabel: { color: '#334155', fontWeight: 600, fontSize: 11.5 }
      },
      series: [
        {
          name: 'Units Sold',
          type: 'bar',
          barWidth: 16,
          itemStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
              { offset: 0, color: '#10B981' },
              { offset: 1, color: '#34D399' }
            ]),
            borderRadius: [0, 8, 8, 0]
          },
          label: {
            show: true,
            position: 'right',
            formatter: '{c} sold',
            color: '#059669',
            fontWeight: 600,
            fontSize: 11
          },
          data: quantities
        }
      ]
    };
  });

  // Visual chart calculation helpers
  getMaxTrendSales(): number {
    const points = this.overviewData()?.revenueTrend || [];
    if (points.length === 0) return 100;
    const max = Math.max(...points.map(p => p.grossSales));
    return max > 0 ? max : 100;
  }

  getMaxDailySales(): number {
    const points = this.salesData()?.dailyTimeline || [];
    if (points.length === 0) return 100;
    const max = Math.max(...points.map(p => p.grossSales));
    return max > 0 ? max : 100;
  }

  getMaxPeakHourOrders(): number {
    const points = this.peakHourData()?.hourlyTraffic || [];
    if (points.length === 0) return 10;
    const max = Math.max(...points.map(p => p.ordersCount));
    return max > 0 ? max : 10;
  }
}
