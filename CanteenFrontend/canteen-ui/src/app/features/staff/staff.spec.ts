import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { StaffComponent } from './staff';
import { StaffService } from '../../core/services/staff.service';
import { AuthService } from '../../core/services/auth.service';
import {
  StaffSummaryMetrics,
  StaffMember,
  StaffPagedResult,
  StaffRoleConfig,
  StaffAttendance,
  StaffActivityLog,
  ImportStaffResult
} from '../../core/models/staff.models';

describe('StaffComponent', () => {
  let component: StaffComponent;
  let fixture: ComponentFixture<StaffComponent>;

  const mockMetrics: StaffSummaryMetrics = {
    totalStaff: 12,
    activeStaff: 10,
    activePercentage: 83.3,
    onLeaveStaff: 1,
    onLeavePercentage: 8.3,
    inactiveStaff: 1,
    inactivePercentage: 8.3,
    monthlyGrowth: 2
  };

  const mockStaffList: StaffMember[] = [
    {
      staffId: 1,
      canteenId: 10,
      employeeId: 'EMP001',
      fullName: 'Amit More',
      email: 'amit.more@canteenhub.com',
      mobileNumber: '+91 98765 43210',
      role: 'Chef',
      shift: 'Morning (6:00 AM - 2:00 PM)',
      status: 'Active',
      joinedOn: '2026-10-10T00:00:00Z',
      profilePhotoUrl: null,
      initials: 'AM',
      salary: 22000,
      createdAt: '2026-10-10T00:00:00Z'
    },
    {
      staffId: 2,
      canteenId: 10,
      employeeId: 'EMP002',
      fullName: 'Pooja Deshmukh',
      email: 'pooja.d@canteenhub.com',
      mobileNumber: '+91 98765 43211',
      role: 'Kitchen Staff',
      shift: 'Morning (6:00 AM - 2:00 PM)',
      status: 'Active',
      joinedOn: '2026-10-12T00:00:00Z',
      profilePhotoUrl: 'https://example.com/photos/pooja.png',
      initials: 'PD',
      salary: 16000,
      createdAt: '2026-10-12T00:00:00Z'
    },
    {
      staffId: 3,
      canteenId: 10,
      employeeId: 'EMP003',
      fullName: 'Sneha Naik',
      email: 'sneha.n@canteenhub.com',
      mobileNumber: '+91 98765 43213',
      role: 'Order Manager',
      shift: 'Evening (2:00 PM - 10:00 PM)',
      status: 'On Leave',
      joinedOn: '2026-10-16T00:00:00Z',
      profilePhotoUrl: null,
      initials: 'SN',
      salary: 18000,
      createdAt: '2026-10-16T00:00:00Z'
    }
  ];

  const mockPagedResult: StaffPagedResult<StaffMember> = {
    items: mockStaffList,
    totalCount: 3,
    page: 1,
    pageSize: 10,
    totalPages: 1
  };

  const mockRoles: StaffRoleConfig[] = [
    {
      roleId: 'chef',
      roleName: 'Chef',
      description: 'Prepares hot meals and manages recipe standardizations.',
      permissions: ['Kitchen KDS Order View', 'Bump/Done Order Items', 'Recipe Prep View'],
      staffCount: 2
    },
    {
      roleId: 'kitchen_staff',
      roleName: 'Kitchen Staff',
      description: 'Assists cooking station and ingredient prep.',
      permissions: ['View KDS Orders', 'Mark Item Cooking'],
      staffCount: 4
    }
  ];

  const mockAttendance: StaffAttendance[] = [
    {
      attendanceId: 1,
      staffId: 1,
      staffName: 'Amit More',
      employeeId: 'EMP001',
      date: '2026-11-03T00:00:00Z',
      checkInTime: '2026-11-03T06:05:00Z',
      checkOutTime: null,
      status: 'Present',
      notes: 'Morning shift on-time'
    }
  ];

  const mockActivityLogs: StaffActivityLog[] = [
    {
      activityId: 1,
      staffId: 1,
      staffName: 'Amit More',
      employeeId: 'EMP001',
      action: 'CheckIn',
      timestamp: '2026-11-03T06:05:00Z',
      details: 'Clocked in for Morning Shift.'
    }
  ];

  const mockStaffService = {
    getSummaryMetrics: vi.fn(() => of(mockMetrics)),
    getPagedStaff: vi.fn(() => of(mockPagedResult)),
    getRoleConfigs: vi.fn(() => of(mockRoles)),
    getAttendance: vi.fn(() => of(mockAttendance)),
    getActivityLogs: vi.fn(() => of(mockActivityLogs)),
    createStaff: vi.fn(),
    updateStaff: vi.fn(),
    deleteStaff: vi.fn(),
    markAttendance: vi.fn(),
    importStaff: vi.fn()
  };

  const mockAuthService = {
    currentUser: () => ({
      ownerId: 5,
      id: 'owner1',
      canteenId: 10,
      email: 'owner@canteen.com',
      role: 'Owner'
    })
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    mockStaffService.getSummaryMetrics.mockReturnValue(of(mockMetrics));
    mockStaffService.getPagedStaff.mockReturnValue(of(mockPagedResult));
    mockStaffService.getRoleConfigs.mockReturnValue(of(mockRoles));
    mockStaffService.getAttendance.mockReturnValue(of(mockAttendance));
    mockStaffService.getActivityLogs.mockReturnValue(of(mockActivityLogs));

    await TestBed.configureTestingModule({
      imports: [StaffComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: StaffService, useValue: mockStaffService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StaffComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create StaffComponent and load initial data', () => {
    expect(component).toBeTruthy();
    expect(mockStaffService.getSummaryMetrics).toHaveBeenCalledWith(10);
    expect(mockStaffService.getPagedStaff).toHaveBeenCalled();
    expect(component.metrics()?.totalStaff).toBe(12);
    expect(component.staffMembers().length).toBe(3);
  });

  it('should switch navigation tabs', () => {
    expect(component.activeTab()).toBe('members');

    component.onTabChange('roles');
    expect(component.activeTab()).toBe('roles');

    component.onTabChange('attendance');
    expect(component.activeTab()).toBe('attendance');

    component.onTabChange('schedules');
    expect(component.activeTab()).toBe('schedules');

    component.onTabChange('activity');
    expect(component.activeTab()).toBe('activity');
  });

  it('should search staff members and reset page to 1', () => {
    const inputEvent = { target: { value: 'Amit' } } as unknown as Event;
    component.onSearch(inputEvent);

    expect(component.searchTerm()).toBe('Amit');
    expect(component.currentPage()).toBe(1);
    expect(mockStaffService.getPagedStaff).toHaveBeenCalled();
  });

  it('should filter staff by role, status, and shift', () => {
    component.onRoleFilter('Chef');
    expect(component.selectedRole()).toBe('Chef');
    expect(component.currentPage()).toBe(1);

    component.onStatusFilter('Active');
    expect(component.selectedStatus()).toBe('Active');

    component.onShiftFilter('Morning (6:00 AM - 2:00 PM)');
    expect(component.selectedShift()).toBe('Morning (6:00 AM - 2:00 PM)');
  });

  it('should reset all filters', () => {
    component.searchTerm.set('Filter');
    component.selectedRole.set('Chef');
    component.selectedStatus.set('Active');
    component.selectedShift.set('Morning (6:00 AM - 2:00 PM)');

    component.resetFilters();

    expect(component.searchTerm()).toBe('');
    expect(component.selectedRole()).toBe('All');
    expect(component.selectedStatus()).toBe('All');
    expect(component.selectedShift()).toBe('All');
  });

  it('should open and populate add staff modal with clean form', () => {
    component.openAddModal();

    expect(component.showStaffModal()).toBe(true);
    expect(component.isEditing()).toBe(false);
    expect(component.editingStaffId()).toBeNull();
    expect(component.staffForm.get('fullName')?.value).toBe('');
    expect(component.staffForm.get('status')?.value).toBe('Active');
  });

  it('should open edit modal with staff data pre-filled', () => {
    const target = mockStaffList[0];
    component.openEditModal(target);

    expect(component.showStaffModal()).toBe(true);
    expect(component.isEditing()).toBe(true);
    expect(component.editingStaffId()).toBe(target.staffId);
    expect(component.staffForm.get('fullName')?.value).toBe(target.fullName);
    expect(component.staffForm.get('mobileNumber')?.value).toBe(target.mobileNumber);
    expect(component.staffForm.get('role')?.value).toBe(target.role);
  });

  it('should save newly created staff member', () => {
    component.openAddModal();
    component.staffForm.patchValue({
      fullName: 'Ganesh Shinde',
      email: 'ganesh@canteen.com',
      mobileNumber: '+91 91234 56789',
      role: 'Chef',
      shift: 'Morning (6:00 AM - 2:00 PM)',
      status: 'Active',
      salary: 20000
    });

    const newStaff: StaffMember = {
      staffId: 99,
      canteenId: 10,
      employeeId: 'EMP004',
      fullName: 'Ganesh Shinde',
      email: 'ganesh@canteen.com',
      mobileNumber: '+91 91234 56789',
      role: 'Chef',
      shift: 'Morning (6:00 AM - 2:00 PM)',
      status: 'Active',
      joinedOn: '2026-11-03T00:00:00Z',
      initials: 'GS',
      createdAt: '2026-11-03T00:00:00Z'
    };

    mockStaffService.createStaff.mockReturnValue(of(newStaff));

    component.saveStaff();

    expect(mockStaffService.createStaff).toHaveBeenCalled();
    expect(component.staffModalSuccess()).toContain('Onboarded Ganesh Shinde');
  });

  it('should update existing staff member', () => {
    const target = mockStaffList[0];
    component.openEditModal(target);
    component.staffForm.patchValue({
      fullName: 'Amit More (Head Chef)'
    });

    const updated: StaffMember = {
      ...target,
      fullName: 'Amit More (Head Chef)'
    };

    mockStaffService.updateStaff.mockReturnValue(of(updated));

    component.saveStaff();

    expect(mockStaffService.updateStaff).toHaveBeenCalled();
    expect(component.staffModalSuccess()).toContain('Updated staff details');
  });

  it('should open and close details modal', () => {
    const target = mockStaffList[1];
    component.openDetailsModal(target);

    expect(component.showDetailsModal()).toBe(true);
    expect(component.activeDetailsStaff()).toEqual(target);

    component.closeDetailsModal();
    expect(component.showDetailsModal()).toBe(false);
    expect(component.activeDetailsStaff()).toBeNull();
  });

  it('should confirm and execute staff deletion', () => {
    const target = mockStaffList[2];
    component.confirmDelete(target);

    expect(component.showDeleteModal()).toBe(true);
    expect(component.staffToDelete()).toEqual(target);

    mockStaffService.deleteStaff.mockReturnValue(of({ success: true, message: 'Deleted' }));

    component.deleteStaffConfirmed();

    expect(mockStaffService.deleteStaff).toHaveBeenCalledWith(10, target.staffId);
  });

  it('should submit bulk staff import with CSV', () => {
    component.openImportModal();
    component.importDataText.set('Ramesh Rao, +919876543219, Chef, Morning (6:00 AM - 2:00 PM), ramesh@test.com');

    const importRes: ImportStaffResult = {
      totalImported: 1,
      failed: 0,
      message: 'Successfully imported 1 staff members.'
    };

    mockStaffService.importStaff.mockReturnValue(of(importRes));

    component.submitImport();

    expect(mockStaffService.importStaff).toHaveBeenCalled();
    expect(component.importMessage()).toBe('Successfully imported 1 staff members.');
  });

  it('should handle quick clock in and out', () => {
    const clockRes: StaffAttendance = {
      attendanceId: 5,
      staffId: 1,
      staffName: 'Amit More',
      employeeId: 'EMP001',
      date: '2026-11-03T00:00:00Z',
      checkInTime: '2026-11-03T06:00:00Z',
      checkOutTime: null,
      status: 'Present'
    };

    mockStaffService.markAttendance.mockReturnValue(of(clockRes));

    component.quickClock(1, 'CheckIn');

    expect(mockStaffService.markAttendance).toHaveBeenCalledWith(10, expect.objectContaining({
      staffId: 1,
      action: 'CheckIn',
      status: 'Present'
    }));
    expect(component.actionToast()).toContain('clocked in');
  });

  it('should compute initial colors and shift grouping helpers correctly', () => {
    expect(component.getInitialsColor('AM')).toBeDefined();
    expect(component.getInitialsColor(undefined)).toBe('avatar-teal');

    const morningStaff = component.getStaffForShift('Morning');
    expect(morningStaff.length).toBe(2);
    expect(morningStaff[0].fullName).toBe('Amit More');
  });

  it('should compute pagination numbers correctly', () => {
    component.totalPages.set(5);
    component.currentPage.set(2);

    const pages = component.getPageNumbers();
    expect(pages).toEqual([1, 2, 3, 4, 5]);
  });
});
