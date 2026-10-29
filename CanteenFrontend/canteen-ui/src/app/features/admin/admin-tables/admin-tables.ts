import { Component, OnInit, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminCanteenItem } from '../../../core/models/admin.models';
import { CanteenTableDto } from '../../../core/models/table.models';

@Component({
  selector: 'app-admin-tables',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-tables.html',
  styleUrl: './admin-tables.css'
})
export class AdminTables implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly currentUser = this.authService.currentUser;

  // Canteens list & selection
  readonly canteens = signal<AdminCanteenItem[]>([]);
  readonly selectedCanteenId = signal<number | null>(null);
  readonly selectedCanteen = computed(() => {
    const id = this.selectedCanteenId();
    return this.canteens().find(c => c.canteenId === id) || null;
  });

  // Tables data
  readonly tables = signal<CanteenTableDto[]>([]);
  readonly loading = signal<boolean>(true);
  readonly tablesLoading = signal<boolean>(false);
  readonly errorMessage = signal<string>('');

  // Search & Filter within selected canteen's tables
  searchTerm = '';
  selectedStatus = 'all'; // 'all' | 'available' | 'in-use'

  // Modal inspection
  readonly showQrModal = signal<boolean>(false);
  readonly selectedQrTable = signal<CanteenTableDto | null>(null);

  // Computed metrics for selected canteen
  readonly totalTables = computed(() => this.tables().length);
  readonly availableTables = computed(() => this.tables().filter(t => t.isAvailable).length);
  readonly inUseTables = computed(() => this.tables().filter(t => !t.isAvailable).length);
  readonly totalCapacity = computed(() => this.tables().reduce((sum, t) => sum + (t.capacity || 0), 0));
  readonly qrConfiguredCount = computed(() => this.tables().filter(t => !!t.qrToken && t.qrToken.trim().length > 0).length);

  readonly filteredTables = computed(() => {
    let list = [...this.tables()];
    const query = this.searchTerm.trim();
    if (query) {
      list = list.filter(t => t.tableNumber.toString().includes(query));
    }

    if (this.selectedStatus === 'available') {
      list = list.filter(t => t.isAvailable);
    } else if (this.selectedStatus === 'in-use') {
      list = list.filter(t => !t.isAvailable);
    }

    return list.sort((a, b) => a.tableNumber - b.tableNumber);
  });

  ngOnInit(): void {
    this.loadCanteens();
  }

  loadCanteens(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getCanteens().subscribe({
      next: (list) => {
        this.canteens.set(list || []);
        this.loading.set(false);

        // Check if query param canteenId is passed or auto-select first
        const qId = this.route.snapshot.queryParamMap.get('canteenId');
        if (qId && list.some(c => c.canteenId === Number(qId))) {
          this.selectCanteen(Number(qId));
        } else if (list.length > 0) {
          this.selectCanteen(list[0].canteenId);
        }
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to load canteens directory.');
        this.cdr.markForCheck();
      }
    });
  }

  selectCanteen(canteenId: number): void {
    this.selectedCanteenId.set(canteenId);
    this.loadTablesForCanteen(canteenId);
  }

  onCanteenChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    if (val) {
      this.selectCanteen(Number(val));
    }
  }

  loadTablesForCanteen(canteenId: number): void {
    this.tablesLoading.set(true);
    this.errorMessage.set('');

    this.adminService.getCanteenTables(canteenId).subscribe({
      next: (tables) => {
        this.tables.set(tables || []);
        this.tablesLoading.set(false);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.tables.set([]);
        this.tablesLoading.set(false);
        this.errorMessage.set(err.error?.message || `Failed to load tables for Canteen #${canteenId}.`);
        this.cdr.markForCheck();
      }
    });
  }

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

  getAdminQrImageUrl(table: CanteenTableDto): string {
    return this.adminService.getAdminQrImageUrl(table.canteenId, table.tableId);
  }

  getTableMenuUrl(table: CanteenTableDto): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:4200';
    return `${origin}/menu?canteenId=${table.canteenId}&tableId=${table.tableId}&token=${table.qrToken}`;
  }

  printQrStandCard(): void {
    const table = this.selectedQrTable();
    if (!table) return;

    if (typeof window === 'undefined' || typeof document === 'undefined') return;

    const qrImgUrl = this.getAdminQrImageUrl(table);
    const canteenName = this.selectedCanteen()?.name || ('Canteen #' + table.canteenId);

    let iframe = document.getElementById('admin-qr-print-frame') as HTMLIFrameElement;
    if (!iframe) {
      iframe = document.createElement('iframe');
      iframe.id = 'admin-qr-print-frame';
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
          @page { size: A4 portrait; margin: 10mm; }
          * { box-sizing: border-box; margin: 0; padding: 0; }
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
          .cut-guide-wrap { display: flex; flex-direction: column; align-items: center; gap: 8px; }
          .cut-instructions { font-size: 11px; font-weight: 700; color: #64748b; text-transform: uppercase; letter-spacing: 0.06em; }
          .table-stand-sticker {
            width: 105mm;
            border: 2px dashed #047857;
            border-radius: 16px;
            padding: 22px 18px 16px;
            display: flex;
            flex-direction: column;
            align-items: center;
            text-align: center;
            background: #ffffff;
            page-break-inside: avoid;
          }
          .header-box { display: flex; flex-direction: column; align-items: center; gap: 4px; width: 100%; }
          .canteen-name { font-size: 13px; font-weight: 800; color: #047857; text-transform: uppercase; letter-spacing: 0.1em; }
          .table-number-title { font-size: 36px; font-weight: 900; color: #0f172a; letter-spacing: -0.02em; line-height: 1.1; margin: 2px 0; }
          .capacity-tag { font-size: 11.5px; font-weight: 600; color: #475569; background: #f1f5f9; padding: 3px 12px; border-radius: 9999px; display: inline-block; }
          .cta-banner { background: #047857; color: #ffffff; width: 100%; padding: 8px 12px; border-radius: 10px; margin: 12px 0 10px; display: flex; flex-direction: column; align-items: center; gap: 2px; }
          .cta-main { font-size: 14.5px; font-weight: 800; letter-spacing: 0.05em; text-transform: uppercase; }
          .cta-sub { font-size: 10.5px; font-weight: 500; opacity: 0.95; }
          .qr-image-container { background: #ffffff; padding: 10px; border: 1.5px solid #cbd5e1; border-radius: 14px; margin: 6px 0 12px; display: flex; align-items: center; justify-content: center; }
          .qr-img { width: 175px; height: 175px; display: block; }
          .steps-row { display: flex; align-items: center; justify-content: center; gap: 6px; width: 100%; margin: 4px 0 10px; }
          .step-pill { flex: 1; background: #f8fafc; border: 1px solid #e2e8f0; padding: 6px 4px; border-radius: 8px; display: flex; flex-direction: column; align-items: center; gap: 2px; }
          .step-num { font-size: 9.5px; font-weight: 800; color: #047857; text-transform: uppercase; }
          .step-text { font-size: 10.5px; font-weight: 700; color: #0f172a; }
          .footer-note { font-size: 10.5px; color: #64748b; font-weight: 500; line-height: 1.35; }
          .brand-watermark { font-size: 9.5px; font-weight: 700; color: #94a3b8; letter-spacing: 0.08em; text-transform: uppercase; margin-top: 10px; border-top: 1px solid #f1f5f9; padding-top: 8px; width: 100%; }
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

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

