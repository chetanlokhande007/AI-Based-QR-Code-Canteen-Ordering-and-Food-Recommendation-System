import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { CanteenService } from '../../core/services/canteen.service';
import { TableService } from '../../core/services/table.service';
import { ThemeService } from '../../core/services/theme.service';
import { CanteenTableDto, CreateTableRequest } from '../../core/models/table.models';
import { CanteenSummaryDto, CanteenDto } from '../../core/models/canteen.models';

@Component({
  selector: 'app-tables',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './tables.html',
  styleUrl: './tables.css'
})
export class TablesComponent implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly canteenService = inject(CanteenService);
  private readonly tableService = inject(TableService);
  private readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly subscriptions = new Subscription();

  readonly currentUser = this.authService.currentUser;
  readonly activeTheme = this.themeService.activeTheme;

  // Data signals
  readonly tables = signal<CanteenTableDto[]>([]);
  readonly canteens = signal<CanteenSummaryDto[]>([]);
  readonly activeCanteen = signal<CanteenDto | null>(null);

  // UI state signals
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string>('');
  readonly isCanteenSelectorOpen = signal<boolean>(false);
  readonly isSwitching = signal<boolean>(false);

  // Search and filter signals
  searchTerm = '';
  readonly selectedStatusFilter = signal<'all' | 'available' | 'in-use'>('all');

  // Modal signals
  readonly showAddModal = signal<boolean>(false);
  readonly showQrModal = signal<boolean>(false);
  readonly selectedQrTable = signal<CanteenTableDto | null>(null);
  readonly modalSaving = signal<boolean>(false);
  readonly modalError = signal<string>('');
  readonly modalSuccess = signal<string>('');

  // Form inputs for Add Table
  formTableNumber: number | null = null;
  formCapacity: number = 4;

  // Computed properties
  readonly currentCanteenName = computed(() => {
    return this.activeCanteen()?.name || (this.currentUser()?.canteenId ? ('Canteen #' + this.currentUser()?.canteenId) : 'Select Canteen');
  });

  readonly totalTablesCount = computed(() => this.tables().length);

  readonly availableTablesCount = computed(() => {
    return this.tables().filter(t => t.isAvailable).length;
  });

  readonly inUseTablesCount = computed(() => {
    return this.tables().filter(t => !t.isAvailable).length;
  });

  readonly totalCapacity = computed(() => {
    return this.tables().reduce((sum, t) => sum + (t.capacity || 0), 0);
  });

  readonly qrCodesReadyCount = computed(() => {
    return this.tables().filter(t => !!t.qrToken && t.qrToken.trim().length > 0).length;
  });

  readonly filteredTables = computed(() => {
    let list = [...this.tables()];

    const query = this.searchTerm.trim();
    if (query) {
      list = list.filter(t => t.tableNumber.toString().includes(query));
    }

    const status = this.selectedStatusFilter();
    if (status === 'available') {
      list = list.filter(t => t.isAvailable);
    } else if (status === 'in-use') {
      list = list.filter(t => !t.isAvailable);
    }

    return list.sort((a, b) => a.tableNumber - b.tableNumber);
  });

  get ownerInitials(): string {
    const name = this.currentUser()?.fullName?.trim();
    if (!name) return 'CO';
    const parts = name.split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  ngOnInit(): void {
    this.subscriptions.add(
      this.route.queryParams.subscribe(params => {
        const qCanteenId = params['canteenId'] ? Number(params['canteenId']) : null;
        if (qCanteenId && qCanteenId !== this.currentUser()?.canteenId) {
          this.authService.switchCanteen(qCanteenId).subscribe({
            next: () => {
              this.loadTablesData();
              this.loadOwnerCanteens();
            },
            error: () => this.loadTablesData()
          });
        } else {
          this.loadTablesData();
          this.loadOwnerCanteens();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadTablesData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) {
      this.loading.set(false);
      this.errorMessage.set('No active canteen selected. Please set up or select a canteen first.');
      this.cdr.markForCheck();
      return;
    }

    // Load active canteen details
    this.canteenService.getById(canteenId).subscribe({
      next: (canteen) => {
        this.activeCanteen.set(canteen);
        this.cdr.markForCheck();
      },
      error: () => { }
    });

    // Load tables for current authorized canteen
    this.tableService.getTablesByCanteen(canteenId).subscribe({
      next: (tables) => {
        this.tables.set(tables || []);
        this.loading.set(false);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to load tables. Please verify canteen status.');
        this.cdr.markForCheck();
      }
    });
  }

  loadOwnerCanteens(): void {
    this.canteenService.getMyCanteens().subscribe({
      next: (list) => {
        this.canteens.set(list || []);
        this.cdr.markForCheck();
      },
      error: () => { }
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
    if (canteen.canteenId === this.currentUser()?.canteenId) return;

    this.isSwitching.set(true);
    this.authService.switchCanteen(canteen.canteenId).subscribe({
      next: () => {
        this.isSwitching.set(false);
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { canteenId: canteen.canteenId },
          queryParamsHandling: 'merge'
        });
        this.loadTablesData();
        this.loadOwnerCanteens();
      },
      error: (err) => {
        this.isSwitching.set(false);
        alert(err.error?.message || 'Failed to switch canteen.');
      }
    });
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  // View QR Modal
  openQrModal(table: CanteenTableDto): void {
    this.selectedQrTable.set(table);
    this.showQrModal.set(true);
    this.cdr.markForCheck();
  }

  closeQrModal(): void {
    this.showQrModal.set(false);
    this.selectedQrTable.set(null);
    this.cdr.markForCheck();
  }

  getTableQrImageUrl(table: CanteenTableDto): string {
    return this.tableService.getQrImageUrl(table.canteenId, table.tableId);
  }

  getTableMenuUrl(table: CanteenTableDto): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:4200';
    return `${origin}/menu?canteenId=${table.canteenId}&tableId=${table.tableId}&token=${table.qrToken}`;
  }

  printQrStandCard(): void {
    const table = this.selectedQrTable();
    if (!table) return;

    if (typeof window === 'undefined' || typeof document === 'undefined') return;

    const qrImgUrl = this.getTableQrImageUrl(table);
    const canteenName = this.currentCanteenName();

    let iframe = document.getElementById('table-qr-print-frame') as HTMLIFrameElement;
    if (!iframe) {
      iframe = document.createElement('iframe');
      iframe.id = 'table-qr-print-frame';
      iframe.style.position = 'fixed';
      iframe.style.right = '0';
      iframe.style.bottom = '0';
      iframe.style.width = '0';
      iframe.style.height = '0';
      iframe.style.border = '0';
      iframe.style.visibility = 'hidden';
      document.body.appendChild(iframe);
    }

    const doc = iframe.contentWindow?.document;
    if (!doc) return;

    doc.open();
    doc.write(`
      <!DOCTYPE html>
      <html lang="en">
      <head>
        <meta charset="UTF-8">
        <title>Table #${table.tableNumber} - ${canteenName} QR Stand</title>
        <style>
          @page {
            size: A4 portrait;
            margin: 10mm;
          }
          * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
          }
          body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
            background: #ffffff;
            color: #0f172a;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
          }
          .cut-guide-wrap {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 8px;
          }
          .cut-instructions {
            font-size: 11px;
            font-weight: 700;
            color: #64748b;
            text-transform: uppercase;
            letter-spacing: 0.06em;
          }
          .table-stand-sticker {
            width: 105mm; /* Standard 4x6" / A6 acrylic stand size */
            border: 2px dashed #0d9488;
            border-radius: 16px;
            padding: 22px 18px 16px;
            display: flex;
            flex-direction: column;
            align-items: center;
            text-align: center;
            background: #ffffff;
            page-break-inside: avoid;
          }
          .header-box {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 4px;
            width: 100%;
          }
          .canteen-name {
            font-size: 13px;
            font-weight: 800;
            color: #0d9488;
            text-transform: uppercase;
            letter-spacing: 0.1em;
          }
          .table-number-title {
            font-size: 36px;
            font-weight: 900;
            color: #0f172a;
            letter-spacing: -0.02em;
            line-height: 1.1;
            margin: 2px 0;
          }
          .capacity-tag {
            font-size: 11.5px;
            font-weight: 600;
            color: #475569;
            background: #f1f5f9;
            padding: 3px 12px;
            border-radius: 9999px;
            display: inline-block;
          }
          .cta-banner {
            background: #0d9488;
            color: #ffffff;
            width: 100%;
            padding: 8px 12px;
            border-radius: 10px;
            margin: 12px 0 10px;
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 2px;
          }
          .cta-main {
            font-size: 14.5px;
            font-weight: 800;
            letter-spacing: 0.05em;
            text-transform: uppercase;
          }
          .cta-sub {
            font-size: 10.5px;
            font-weight: 500;
            opacity: 0.95;
          }
          .qr-image-container {
            background: #ffffff;
            padding: 10px;
            border: 1.5px solid #cbd5e1;
            border-radius: 14px;
            margin: 6px 0 12px;
            display: flex;
            align-items: center;
            justify-content: center;
          }
          .qr-img {
            width: 175px;
            height: 175px;
            display: block;
          }
          .steps-row {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 6px;
            width: 100%;
            margin: 4px 0 10px;
          }
          .step-pill {
            flex: 1;
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            padding: 6px 4px;
            border-radius: 8px;
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 2px;
          }
          .step-num {
            font-size: 9.5px;
            font-weight: 800;
            color: #0d9488;
            text-transform: uppercase;
          }
          .step-text {
            font-size: 10.5px;
            font-weight: 700;
            color: #0f172a;
          }
          .footer-note {
            font-size: 10.5px;
            color: #64748b;
            font-weight: 500;
            line-height: 1.35;
          }
          .brand-watermark {
            font-size: 9.5px;
            font-weight: 700;
            color: #94a3b8;
            letter-spacing: 0.08em;
            text-transform: uppercase;
            margin-top: 10px;
            border-top: 1px solid #f1f5f9;
            padding-top: 8px;
            width: 100%;
          }
        </style>
      </head>
      <body>
        <div class="cut-guide-wrap">
          <span class="cut-instructions">✂ Cut along dashed border & stick to table or slide into acrylic stand</span>
          <div class="table-stand-sticker">
            <div class="header-box">
              <span class="canteen-name">${canteenName}</span>
              <h1 class="table-number-title">TABLE #${table.tableNumber}</h1>
              <span class="capacity-tag">Capacity: ${table.capacity} Persons</span>
            </div>

            <div class="cta-banner">
              <span class="cta-main">⚡ SCAN TO ORDER & PAY</span>
              <span class="cta-sub">Instant Table-Side Contactless Service</span>
            </div>

            <div class="qr-image-container">
              <img src="${qrImgUrl}" alt="Table #${table.tableNumber} QR Code" class="qr-img" />
            </div>

            <div class="steps-row">
              <div class="step-pill">
                <span class="step-num">Step 1</span>
                <span class="step-text">📷 Open Camera</span>
              </div>
              <div class="step-pill">
                <span class="step-num">Step 2</span>
                <span class="step-text">📲 Scan QR</span>
              </div>
              <div class="step-pill">
                <span class="step-num">Step 3</span>
                <span class="step-text">🍔 Order & Pay</span>
              </div>
            </div>

            <p class="footer-note">No app download or sign-in required.<br>Your order streams directly to the kitchen live.</p>
            <div class="brand-watermark">Powered by CanteenHub • Smart Dining OS</div>
          </div>
        </div>
      </body>
      </html>
    `);
    doc.close();

    const img = doc.querySelector('.qr-img') as HTMLImageElement;
    const triggerPrint = () => {
      setTimeout(() => {
        iframe.contentWindow?.focus();
        iframe.contentWindow?.print();
      }, 150);
    };

    if (img) {
      img.onload = triggerPrint;
      img.onerror = triggerPrint;
    } else {
      setTimeout(triggerPrint, 250);
    }
  }

  // Add Table Modal
  openAddModal(): void {
    // Auto suggest next table number
    const maxTable = this.tables().reduce((max, t) => Math.max(max, t.tableNumber), 0);
    this.formTableNumber = maxTable + 1;
    this.formCapacity = 4;
    this.modalError.set('');
    this.modalSuccess.set('');
    this.showAddModal.set(true);
    this.cdr.markForCheck();
  }

  closeAddModal(): void {
    this.showAddModal.set(false);
    this.modalError.set('');
    this.modalSuccess.set('');
    this.cdr.markForCheck();
  }

  saveTable(): void {
    this.modalError.set('');
    this.modalSuccess.set('');

    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) {
      this.modalError.set('No active canteen selected.');
      return;
    }

    if (!this.formTableNumber || this.formTableNumber < 1) {
      this.modalError.set('Please provide a valid table number (1 or higher).');
      return;
    }

    if (!this.formCapacity || this.formCapacity < 1 || this.formCapacity > 50) {
      this.modalError.set('Table capacity must be between 1 and 50 seats.');
      return;
    }

    // Check if table number already exists
    const exists = this.tables().some(t => t.tableNumber === this.formTableNumber);
    if (exists) {
      this.modalError.set(`Table #${this.formTableNumber} already exists in this canteen.`);
      return;
    }

    this.modalSaving.set(true);
    const payload: CreateTableRequest = {
      canteenId,
      tableNumber: this.formTableNumber,
      capacity: this.formCapacity
    };

    this.tableService.createTable(canteenId, payload).subscribe({
      next: (created) => {
        this.modalSaving.set(false);
        this.tables.update(prev => [...prev, created]);
        this.closeAddModal();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.modalSaving.set(false);
        this.modalError.set(err.error?.message || 'Failed to create table. Please try again.');
        this.cdr.markForCheck();
      }
    });
  }

  deleteTable(table: CanteenTableDto): void {
    if (!confirm(`Are you sure you want to delete Table #${table.tableNumber}? Existing printed QR codes for this table will become inactive.`)) {
      return;
    }

    const canteenId = this.currentUser()?.canteenId;
    if (!canteenId) return;

    this.tableService.deleteTable(canteenId, table.tableId).subscribe({
      next: () => {
        this.tables.update(prev => prev.filter(t => t.tableId !== table.tableId));
        this.cdr.markForCheck();
      },
      error: (err) => {
        alert(err.error?.message || 'Failed to delete table.');
      }
    });
  }
}

