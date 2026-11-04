export interface StaffSummaryMetrics {
  totalStaff: number;
  activeStaff: number;
  activePercentage: number;
  onLeaveStaff: number;
  onLeavePercentage: number;
  inactiveStaff: number;
  inactivePercentage: number;
  monthlyGrowth: number;
}

export interface StaffMember {
  staffId: number;
  canteenId: number;
  employeeId: string;
  fullName: string;
  email: string;
  mobileNumber: string;
  role: string;
  shift: string;
  status: string;
  joinedOn: string;
  profilePhotoUrl?: string | null;
  initials: string;
  emergencyContact?: string | null;
  address?: string | null;
  salary?: number | null;
  notes?: string | null;
  createdAt: string;
}

export interface CreateStaffMember {
  fullName: string;
  email?: string;
  mobileNumber: string;
  role: string;
  shift: string;
  status?: string;
  joinedOn?: string;
  profilePhotoUrl?: string | null;
  emergencyContact?: string;
  address?: string;
  salary?: number;
  notes?: string;
}

export interface UpdateStaffMember {
  fullName?: string;
  email?: string;
  mobileNumber?: string;
  role?: string;
  shift?: string;
  status?: string;
  profilePhotoUrl?: string | null;
  emergencyContact?: string;
  address?: string;
  salary?: number;
  notes?: string;
}

export interface StaffPagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface StaffRoleConfig {
  roleId: string;
  roleName: string;
  description: string;
  permissions: string[];
  staffCount: number;
}

export interface StaffAttendance {
  attendanceId: number;
  staffId: number;
  staffName: string;
  employeeId: string;
  role?: string;
  shift?: string;
  date: string;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  status: string;
  notes?: string | null;
}

export interface MarkAttendance {
  staffId: number;
  date?: string;
  status?: string;
  action: 'CheckIn' | 'CheckOut' | 'MarkAbsent' | 'MarkLeave';
  notes?: string;
}

export interface StaffActivityLog {
  activityId: number;
  staffId: number;
  staffName: string;
  employeeId: string;
  action: string;
  timestamp: string;
  details?: string | null;
}

export interface ImportStaffResult {
  totalImported: number;
  failed: number;
  message: string;
}

