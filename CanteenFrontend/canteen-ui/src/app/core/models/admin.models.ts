export interface PlatformAdminMetrics {
  totalCanteens: number;
  activeCanteens: number;
  suspendedCanteens: number;
  totalOwners: number;
  recentCanteens: AdminCanteenItem[];
}

export interface AdminCanteenItem {
  canteenId: number;
  name: string;
  location: string;
  canteenType?: string;
  ownerId?: number;
  ownerName: string;
  ownerEmail: string;
  isActive: boolean;
  tenantStatus?: 'Active' | 'Suspended' | 'TrialExpired';
  setupStatus?: 'Draft' | 'Completed';
  menuItemsCount: number;
  tablesCount: number;
  createdAt: string;
}

export interface UpdateCanteenStatusRequest {
  isActive?: boolean;
  tenantStatus?: 'Active' | 'Suspended' | 'TrialExpired';
}
