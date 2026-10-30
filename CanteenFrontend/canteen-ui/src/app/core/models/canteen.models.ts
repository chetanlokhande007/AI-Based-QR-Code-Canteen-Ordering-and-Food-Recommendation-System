export interface DaySchedule {
  day: string;
  isOpen: boolean;
  openTime: string;
  closeTime: string;
}

export interface CanteenDto {
  canteenId: number;
  name: string;
  slug?: string;
  location: string;
  canteenType?: string;
  description?: string;
  contactPhone?: string;
  contactEmail?: string;

  // Owner Bank Details
  bankAccountHolder?: string;
  bankName?: string;
  accountNumber?: string;
  ifscCode?: string;
  accountType?: string;
  upiId?: string;

  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  landmark?: string;
  operatingHoursJson?: string;
  imageUrl?: string;
  feeAbsorptionPolicy?: 'CustomerPays' | 'CanteenAbsorbs' | string;
  taxPercentage?: number;
  isActive: boolean;
  tenantStatus?: 'Active' | 'Suspended' | 'TrialExpired';
  setupStatus?: 'Draft' | 'Completed';
  createdAt: string;
  token?: string;
}

export interface PublicCanteenDto {
  canteenId: number;
  name: string;
  slug?: string;
  location: string;
  canteenType?: string;
  description?: string;
  addressLine1?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  operatingHoursJson?: string;
  imageUrl?: string;
  feeAbsorptionPolicy?: 'CustomerPays' | 'CanteenAbsorbs' | string;
  taxPercentage?: number;
  isActive: boolean;
}

export interface CanteenSummaryDto {
  canteenId: number;
  name: string;
  slug?: string;
  location: string;
  description?: string;
  imageUrl?: string;
  status: 'Active' | 'Setup Pending' | 'Inactive' | string;
  menuItemsCount: number;
  tablesCount: number;
  todayOrdersCount: number;
  isActive: boolean;
  tenantStatus?: 'Active' | 'Suspended' | 'TrialExpired';
  setupStatus?: 'Draft' | 'Completed';
  isSetupComplete: boolean;
  createdAt: string;
}

export interface CreateCanteenRequest {
  name: string;
  slug?: string;
  location: string;
  canteenType?: string;
  description?: string;
  contactPhone?: string;
  contactEmail?: string;

  // Owner Bank Details
  bankAccountHolder?: string;
  bankName?: string;
  accountNumber?: string;
  ifscCode?: string;
  accountType?: string;
  upiId?: string;

  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  landmark?: string;
  operatingHoursJson?: string;
  imageUrl?: string;
}

export interface UpdateCanteenRequest {
  name: string;
  slug?: string;
  location: string;
  canteenType?: string;
  description?: string;
  contactPhone?: string;
  contactEmail?: string;

  // Owner Bank Details
  bankAccountHolder?: string;
  bankName?: string;
  accountNumber?: string;
  ifscCode?: string;
  accountType?: string;
  upiId?: string;

  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  landmark?: string;
  operatingHoursJson?: string;
  imageUrl?: string;
  isActive?: boolean;
}
