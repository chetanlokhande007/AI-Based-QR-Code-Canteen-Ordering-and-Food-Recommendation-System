export interface DaySchedule {
  day: string;
  isOpen: boolean;
  openTime: string;
  closeTime: string;
}

export interface CanteenDto {
  canteenId: number;
  name: string;
  location: string;
  canteenType?: string;
  description?: string;
  contactPhone?: string;
  contactEmail?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  landmark?: string;
  operatingHoursJson?: string;
  imageUrl?: string;
  isActive: boolean;
  createdAt: string;
  token?: string;
}

export interface CanteenSummaryDto {
  canteenId: number;
  name: string;
  location: string;
  description?: string;
  imageUrl?: string;
  status: 'Active' | 'Setup Pending' | 'Inactive' | string;
  menuItemsCount: number;
  tablesCount: number;
  todayOrdersCount: number;
  isActive: boolean;
  isSetupComplete: boolean;
  createdAt: string;
}

export interface CreateCanteenRequest {
  name: string;
  location: string;
  canteenType?: string;
  description?: string;
  contactPhone?: string;
  contactEmail?: string;
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
  location: string;
  canteenType?: string;
  description?: string;
  contactPhone?: string;
  contactEmail?: string;
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
