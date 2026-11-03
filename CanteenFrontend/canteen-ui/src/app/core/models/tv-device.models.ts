export interface CreateTvPairingRequestDto {
  deviceName?: string;
  stationScope?: 'All' | 'Food' | 'Beverage';
}

export interface TvPairingSessionResponseDto {
  sessionId: number;
  canteenId: number;
  pairingCode: string;
  stationScope: string;
  expiresAt: string;
  expiresInSeconds: number;
}

export interface ClaimTvPairingDto {
  pairingCode: string;
  deviceIdentifier: string;
  deviceName?: string;
}

export interface TvClaimResultDto {
  success: boolean;
  message: string;
  deviceToken?: string;
  canteenId: number;
  canteenName: string;
  tvDeviceId: number;
  deviceName: string;
  stationScope: string;
}

export interface TvDeviceDto {
  tvDeviceId: number;
  canteenId: number;
  deviceName: string;
  deviceIdentifier: string;
  stationScope: string;
  isActive: boolean;
  registeredAt: string;
  lastConnectedAt?: string;
  revokedAt?: string;
}

export interface UpdateTvDeviceDto {
  deviceName: string;
  stationScope: string;
}

