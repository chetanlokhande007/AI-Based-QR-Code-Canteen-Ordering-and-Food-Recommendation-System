import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { StaffService } from '../../core/services/staff.service';
import {
  StaffSummaryMetrics,
  StaffMember,
  StaffRoleConfig,
  StaffAttendance,
  StaffActivityLog,
  CreateStaffMember,
  UpdateStaffMember
} from '../../core/models/staff.models';

export type StaffTab = 'members' | 'roles' | 'attendance' | 'schedules' | 'activity';

@Component({
  selector: 'app-staff',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './staff.html',
  styleUrls: ['./staff.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StaffComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly staffService = inject(StaffService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly currentUser = this.authService.currentUser;

  get effectiveCanteenId(): number | null {
    const qId = this.route.snapshot.queryParams['canteenId'];
    if (qId) return Number(qId);
    return this.currentUser()?.canteenId ?? null;
  }

  // Active Tab
  readonly activeTab = signal<StaffTab>('members');

  // Summary Metrics
  readonly metrics = signal<StaffSummaryMetrics | null>(null);
  readonly isMetricsLoading = signal<boolean>(false);
  readonly metricsError = signal<string | null>(null);

  // Staff Table State
  readonly staffMembers = signal<StaffMember[]>([]);
  readonly totalStaffCount = signal<number>(0);
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(10);
  readonly totalPages = signal<number>(1);
  readonly isStaffLoading = signal<boolean>(false);
  readonly staffError = signal<string | null>(null);

  // Filters
  readonly searchTerm = signal<string>('');
  readonly selectedRole = signal<string>('All');
  readonly selectedStatus = signal<string>('All');
  readonly selectedShift = signal<string>('All');

  // Filter Dropdown Options
  readonly roleOptions = ['All', 'Chef', 'Kitchen Staff', 'Counter Staff', 'Order Manager', 'Inventory Staff', 'Cashier', 'Cleaner'];
  readonly statusOptions = ['All', 'Active', 'On Leave', 'Inactive'];
  readonly shiftOptions = ['All', 'Morning (6:00 AM - 2:00 PM)', 'Evening (2:00 PM - 10:00 PM)', 'Night (10:00 PM - 6:00 AM)', 'Full Day (9:00 AM - 9:00 PM)'];

  // Other Tabs State
  readonly roleConfigs = signal<StaffRoleConfig[]>([]);
  readonly isRolesLoading = signal<boolean>(false);

  readonly attendanceLogs = signal<StaffAttendance[]>([]);
  readonly isAttendanceLoading = signal<boolean>(false);

  readonly activityLogs = signal<StaffActivityLog[]>([]);
  readonly isActivityLoading = signal<boolean>(false);

  // Add / Edit Modal
  readonly showStaffModal = signal<boolean>(false);
  readonly isEditing = signal<boolean>(false);
  readonly editingStaffId = signal<number | null>(null);
  readonly profilePhotoPreview = signal<string | null>(null);
  readonly isSavingStaff = signal<boolean>(false);
  readonly staffModalError = signal<string | null>(null);
  readonly staffModalSuccess = signal<string | null>(null);
  readonly staffForm: FormGroup;

  // View Details Modal
  readonly showDetailsModal = signal<boolean>(false);
  readonly activeDetailsStaff = signal<StaffMember | null>(null);

  // Import Modal
  readonly showImportModal = signal<boolean>(false);
  readonly isImporting = signal<boolean>(false);
  readonly importDataText = signal<string>('');
  readonly importMessage = signal<string | null>(null);
  readonly importError = signal<string | null>(null);

  // Delete Modal
  readonly showDeleteModal = signal<boolean>(false);
  readonly staffToDelete = signal<StaffMember | null>(null);
  readonly isDeleting = signal<boolean>(false);

  // Clock In/Out Action & Attendance Roster
  readonly isClocking = signal<boolean>(false);
  readonly actionToast = signal<string | null>(null);
  readonly selectedAttendanceDate = signal<string>(this.formatIsoDate(new Date()));
  readonly showMarkAttendanceModal = signal<boolean>(false);
  readonly selectedStaffForAttendance = signal<StaffMember | null>(null);
  readonly attendanceActionType = signal<'CheckIn' | 'CheckOut' | 'MarkAbsent' | 'MarkLeave'>('CheckIn');
  readonly attendanceNotes = signal<string>('');
  readonly markAttendanceError = signal<string | null>(null);

  private formatIsoDate(d: Date): string {
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  constructor() {
    this.staffForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      email: ['', [Validators.email]],
      mobileNumber: ['', [Validators.required, Validators.pattern('^[0-9+\\s\\-]{10,15}$')]],
      role: ['Kitchen Staff', [Validators.required]],
      shift: ['Morning (6:00 AM - 2:00 PM)', [Validators.required]],
      status: ['Active', [Validators.required]],
      salary: [null, [Validators.min(0)]],
      emergencyContact: [''],
      address: [''],
      notes: ['']
    });
  }

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['tab']) {
        const t = params['tab'] as StaffTab;
        if (['members', 'roles', 'attendance', 'schedules', 'activity'].includes(t)) {
          this.activeTab.set(t);
        }
      }
      this.loadAll();
    });
  }

  loadAll(): void {
    this.loadMetrics();
    this.loadStaff();
    this.loadRoles();
    this.loadAttendance();
    this.loadActivity();
  }

  loadMetrics(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isMetricsLoading.set(true);
    this.metricsError.set(null);

    this.staffService.getSummaryMetrics(canteenId).subscribe({
      next: (data) => {
        this.metrics.set(data);
        this.isMetricsLoading.set(false);
      },
      error: (err) => {
        this.metricsError.set(err?.error?.message || 'Failed to load staff metrics.');
        this.isMetricsLoading.set(false);
      }
    });
  }

  loadStaff(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isStaffLoading.set(true);
    this.staffError.set(null);

    this.staffService.getPagedStaff(canteenId, {
      page: this.currentPage(),
      pageSize: this.pageSize(),
      search: this.searchTerm(),
      role: this.selectedRole(),
      status: this.selectedStatus(),
      shift: this.selectedShift()
    }).subscribe({
      next: (res) => {
        this.staffMembers.set(res.items || []);
        this.totalStaffCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.isStaffLoading.set(false);
      },
      error: (err) => {
        this.staffError.set(err?.error?.message || 'Failed to load staff roster.');
        this.isStaffLoading.set(false);
      }
    });
  }

  loadRoles(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isRolesLoading.set(true);
    this.staffService.getRoleConfigs(canteenId).subscribe({
      next: (data) => {
        this.roleConfigs.set(data || []);
        this.isRolesLoading.set(false);
      },
      error: () => this.isRolesLoading.set(false)
    });
  }

  loadAttendance(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isAttendanceLoading.set(true);
    this.staffService.getAttendance(canteenId, this.selectedAttendanceDate()).subscribe({
      next: (data) => {
        this.attendanceLogs.set(data || []);
        this.isAttendanceLoading.set(false);
      },
      error: () => this.isAttendanceLoading.set(false)
    });
  }

  loadActivity(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId) return;

    this.isActivityLoading.set(true);
    this.staffService.getActivityLogs(canteenId).subscribe({
      next: (data) => {
        this.activityLogs.set(data || []);
        this.isActivityLoading.set(false);
      },
      error: () => this.isActivityLoading.set(false)
    });
  }

  onTabChange(tab: StaffTab): void {
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
  }

  onSearch(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchTerm.set(val);
    this.currentPage.set(1);
    this.loadStaff();
  }

  onRoleFilter(role: string): void {
    this.selectedRole.set(role);
    this.currentPage.set(1);
    this.loadStaff();
  }

  onStatusFilter(status: string): void {
    this.selectedStatus.set(status);
    this.currentPage.set(1);
    this.loadStaff();
  }

  onShiftFilter(shift: string): void {
    this.selectedShift.set(shift);
    this.currentPage.set(1);
    this.loadStaff();
  }

  resetFilters(): void {
    this.searchTerm.set('');
    this.selectedRole.set('All');
    this.selectedStatus.set('All');
    this.selectedShift.set('All');
    this.currentPage.set(1);
    this.loadStaff();
  }

  onPageChange(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadStaff();
  }

  openAddModal(): void {
    this.isEditing.set(false);
    this.editingStaffId.set(null);
    this.profilePhotoPreview.set(null);
    this.staffModalError.set(null);
    this.staffModalSuccess.set(null);
    this.staffForm.reset({
      fullName: '',
      email: '',
      mobileNumber: '',
      role: 'Kitchen Staff',
      shift: 'Morning (6:00 AM - 2:00 PM)',
      status: 'Active',
      salary: null,
      emergencyContact: '',
      address: '',
      notes: ''
    });
    this.showStaffModal.set(true);
  }

  openEditModal(staff: StaffMember): void {
    this.isEditing.set(true);
    this.editingStaffId.set(staff.staffId);
    this.profilePhotoPreview.set(staff.profilePhotoUrl || null);
    this.staffModalError.set(null);
    this.staffModalSuccess.set(null);
    this.staffForm.patchValue({
      fullName: staff.fullName,
      email: staff.email,
      mobileNumber: staff.mobileNumber,
      role: staff.role,
      shift: staff.shift,
      status: staff.status,
      salary: staff.salary,
      emergencyContact: staff.emergencyContact || '',
      address: staff.address || '',
      notes: staff.notes || ''
    });
    this.showStaffModal.set(true);
  }

  closeStaffModal(): void {
    this.showStaffModal.set(false);
    this.isEditing.set(false);
    this.editingStaffId.set(null);
    this.profilePhotoPreview.set(null);
  }

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      const file = input.files[0];
      if (file.size > 2 * 1024 * 1024) {
        this.staffModalError.set('Photo file size must be less than 2MB.');
        return;
      }
      const reader = new FileReader();
      reader.onload = () => {
        this.profilePhotoPreview.set(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  removePhoto(): void {
    this.profilePhotoPreview.set(null);
  }

  saveStaff(): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId || this.staffForm.invalid) {
      this.staffForm.markAllAsTouched();
      return;
    }

    this.isSavingStaff.set(true);
    this.staffModalError.set(null);
    this.staffModalSuccess.set(null);

    const formVal = this.staffForm.value;
    const photoUrl = this.profilePhotoPreview();

    if (this.isEditing()) {
      const staffId = this.editingStaffId()!;
      const updatePayload: UpdateStaffMember = {
        fullName: formVal.fullName,
        email: formVal.email,
        mobileNumber: formVal.mobileNumber,
        role: formVal.role,
        shift: formVal.shift,
        status: formVal.status,
        salary: formVal.salary,
        emergencyContact: formVal.emergencyContact,
        address: formVal.address,
        notes: formVal.notes,
        profilePhotoUrl: photoUrl
      };

      this.staffService.updateStaff(canteenId, staffId, updatePayload).subscribe({
        next: (res) => {
          this.isSavingStaff.set(false);
          this.staffModalSuccess.set(`Updated staff details for ${res.fullName}.`);
          setTimeout(() => {
            this.closeStaffModal();
            this.loadAll();
          }, 900);
        },
        error: (err) => {
          this.isSavingStaff.set(false);
          this.staffModalError.set(err?.error?.message || 'Failed to update staff member.');
        }
      });
    } else {
      const createPayload: CreateStaffMember = {
        fullName: formVal.fullName,
        email: formVal.email,
        mobileNumber: formVal.mobileNumber,
        role: formVal.role,
        shift: formVal.shift,
        status: formVal.status,
        salary: formVal.salary,
        emergencyContact: formVal.emergencyContact,
        address: formVal.address,
        notes: formVal.notes,
        profilePhotoUrl: photoUrl
      };

      this.staffService.createStaff(canteenId, createPayload).subscribe({
        next: (res) => {
          this.isSavingStaff.set(false);
          this.staffModalSuccess.set(`Onboarded ${res.fullName} (${res.employeeId}).`);
          setTimeout(() => {
            this.closeStaffModal();
            this.loadAll();
          }, 900);
        },
        error: (err) => {
          this.isSavingStaff.set(false);
          this.staffModalError.set(err?.error?.message || 'Failed to onboard staff member.');
        }
      });
    }
  }

  openDetailsModal(staff: StaffMember): void {
    this.activeDetailsStaff.set(staff);
    this.showDetailsModal.set(true);
  }

  closeDetailsModal(): void {
    this.showDetailsModal.set(false);
    this.activeDetailsStaff.set(null);
  }

  confirmDelete(staff: StaffMember): void {
    this.staffToDelete.set(staff);
    this.showDeleteModal.set(true);
  }

  closeDeleteModal(): void {
    this.showDeleteModal.set(false);
    this.staffToDelete.set(null);
  }

  deleteStaffConfirmed(): void {
    const staff = this.staffToDelete();
    const canteenId = this.effectiveCanteenId;
    if (!staff || !canteenId) return;

    this.isDeleting.set(true);
    this.staffService.deleteStaff(canteenId, staff.staffId).subscribe({
      next: () => {
        this.isDeleting.set(false);
        this.closeDeleteModal();
        this.loadAll();
      },
      error: () => {
        this.isDeleting.set(false);
      }
    });
  }

  openImportModal(): void {
    this.importDataText.set('');
    this.importMessage.set(null);
    this.importError.set(null);
    this.showImportModal.set(true);
  }

  closeImportModal(): void {
    this.showImportModal.set(false);
  }

  submitImport(): void {
    const canteenId = this.effectiveCanteenId;
    const text = this.importDataText().trim();
    if (!canteenId || !text) {
      this.importError.set('Please provide CSV or JSON data to import.');
      return;
    }

    this.isImporting.set(true);
    this.importError.set(null);
    this.importMessage.set(null);

    let membersToImport: CreateStaffMember[] = [];

    try {
      if (text.startsWith('[') || text.startsWith('{')) {
        const parsed = JSON.parse(text);
        membersToImport = Array.isArray(parsed) ? parsed : [parsed];
      } else {
        // Parse CSV format: FullName, Mobile, Role, Shift, Email
        const lines = text.split('\n').map(l => l.trim()).filter(l => l.length > 0);
        for (const line of lines) {
          const parts = line.split(',').map(p => p.trim());
          if (parts.length >= 3) {
            membersToImport.push({
              fullName: parts[0],
              mobileNumber: parts[1],
              role: parts[2] || 'Kitchen Staff',
              shift: parts[3] || 'Morning (6:00 AM - 2:00 PM)',
              email: parts[4] || ''
            });
          }
        }
      }
    } catch {
      this.isImporting.set(false);
      this.importError.set('Invalid data format. Please provide valid JSON array or CSV text.');
      return;
    }

    if (membersToImport.length === 0) {
      this.isImporting.set(false);
      this.importError.set('No valid staff records found.');
      return;
    }

    this.staffService.importStaff(canteenId, membersToImport).subscribe({
      next: (res) => {
        this.isImporting.set(false);
        this.importMessage.set(res.message);
        setTimeout(() => {
          this.closeImportModal();
          this.loadAll();
        }, 1200);
      },
      error: (err) => {
        this.isImporting.set(false);
        this.importError.set(err?.error?.message || 'Failed to import staff.');
      }
    });
  }

  quickClock(staffId: number, action: 'CheckIn' | 'CheckOut' | 'MarkAbsent' | 'MarkLeave', notes?: string): void {
    const canteenId = this.effectiveCanteenId;
    if (!canteenId || this.isClocking()) return;

    this.isClocking.set(true);
    const status = action === 'MarkAbsent'
      ? 'Absent'
      : (action === 'MarkLeave' ? 'On Leave' : (action === 'CheckOut' ? 'Completed' : 'Present'));

    this.staffService.markAttendance(canteenId, {
      staffId,
      action,
      date: this.selectedAttendanceDate(),
      status,
      notes: notes || undefined
    }).subscribe({
      next: (record) => {
        this.isClocking.set(false);
        const name = (record as any).staffName || (record as any).StaffName || 'Staff';
        const empId = (record as any).employeeId || (record as any).EmployeeId || '';
        let verb = 'clocked in';
        if (action === 'CheckOut') verb = 'clocked out';
        else if (action === 'MarkAbsent') verb = 'marked absent';
        else if (action === 'MarkLeave') verb = 'marked on leave';

        this.actionToast.set(`${name} ${empId ? '(' + empId + ')' : ''} ${verb}.`);
        setTimeout(() => this.actionToast.set(null), 3000);
        this.loadAttendance();
        this.loadActivity();
      },
      error: (err) => {
        this.isClocking.set(false);
        this.actionToast.set(err?.error?.message || 'Failed to update attendance.');
        setTimeout(() => this.actionToast.set(null), 3000);
      }
    });
  }

  navigateAttendanceDate(offsetDays: number): void {
    const parts = this.selectedAttendanceDate().split('-').map(Number);
    const curr = new Date(parts[0], parts[1] - 1, parts[2]);
    curr.setDate(curr.getDate() + offsetDays);
    this.selectedAttendanceDate.set(this.formatIsoDate(curr));
    this.loadAttendance();
  }

  onAttendanceDateChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    if (val) {
      this.selectedAttendanceDate.set(val);
      this.loadAttendance();
    }
  }

  setAttendanceToday(): void {
    this.selectedAttendanceDate.set(this.formatIsoDate(new Date()));
    this.loadAttendance();
  }

  isAttendanceToday(): boolean {
    return this.selectedAttendanceDate() === this.formatIsoDate(new Date());
  }

  getAttendanceClockedInCount(): number {
    return this.attendanceLogs().filter(a => !!a.checkInTime && !a.checkOutTime && (a.status === 'Present' || a.status === 'Active')).length;
  }

  getAttendanceNotClockedInCount(): number {
    return this.attendanceLogs().filter(a => a.status === 'Not Clocked In' || (!a.checkInTime && a.status !== 'Absent' && a.status !== 'On Leave')).length;
  }

  getAttendanceLeaveAbsentCount(): number {
    return this.attendanceLogs().filter(a => a.status === 'On Leave' || a.status === 'Absent').length;
  }

  getAttendanceStatusClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'present' || s === 'active') return 'status-active';
    if (s === 'completed') return 'status-completed';
    if (s === 'on leave' || s === 'leave' || s === 'half day') return 'status-leave';
    if (s === 'absent' || s === 'inactive') return 'status-inactive';
    return 'status-pending';
  }

  openMarkAttendanceModal(): void {
    const staff = this.staffMembers()[0] || null;
    this.selectedStaffForAttendance.set(staff);
    this.attendanceActionType.set('CheckIn');
    this.attendanceNotes.set('');
    this.markAttendanceError.set(null);
    this.showMarkAttendanceModal.set(true);
  }

  openMarkAttendanceModalForStaff(att: StaffAttendance): void {
    const staff = this.staffMembers().find(s => s.staffId === att.staffId) || null;
    this.selectedStaffForAttendance.set(staff);
    if (!att.checkInTime || att.status === 'Not Clocked In') {
      this.attendanceActionType.set('CheckIn');
    } else if (att.checkInTime && !att.checkOutTime) {
      this.attendanceActionType.set('CheckOut');
    } else {
      this.attendanceActionType.set('CheckIn');
    }
    this.attendanceNotes.set(att.notes || '');
    this.markAttendanceError.set(null);
    this.showMarkAttendanceModal.set(true);
  }

  closeMarkAttendanceModal(): void {
    this.showMarkAttendanceModal.set(false);
  }

  onStaffSelectForAttendance(event: Event): void {
    const sId = Number((event.target as HTMLSelectElement).value);
    const staff = this.staffMembers().find(s => s.staffId === sId) || null;
    this.selectedStaffForAttendance.set(staff);
  }

  onAttendanceActionTypeChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value as 'CheckIn' | 'CheckOut' | 'MarkAbsent' | 'MarkLeave';
    this.attendanceActionType.set(val);
  }

  submitMarkAttendance(): void {
    const staff = this.selectedStaffForAttendance();
    if (!staff) {
      this.markAttendanceError.set('Please select a staff member.');
      return;
    }
    this.quickClock(staff.staffId, this.attendanceActionType(), this.attendanceNotes());
    this.closeMarkAttendanceModal();
  }

  getInitialsColor(initials?: string): string {
    if (!initials) return 'avatar-teal';
    const code = initials.charCodeAt(0) % 5;
    const colors = ['avatar-purple', 'avatar-blue', 'avatar-pink', 'avatar-amber', 'avatar-teal'];
    return colors[code];
  }

  getStaffForShift(shiftKeyword: string): StaffMember[] {
    return this.staffMembers().filter(s => s.shift?.toLowerCase().includes(shiftKeyword.toLowerCase()));
  }

  getPageNumbers(): (number | string)[] {
    const total = this.totalPages();
    const curr = this.currentPage();
    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }
    const pages: (number | string)[] = [];
    pages.push(1);
    if (curr > 3) pages.push('...');
    const start = Math.max(2, curr - 1);
    const end = Math.min(total - 1, curr + 1);
    for (let i = start; i <= end; i++) pages.push(i);
    if (curr < total - 2) pages.push('...');
    pages.push(total);
    return pages;
  }
}
