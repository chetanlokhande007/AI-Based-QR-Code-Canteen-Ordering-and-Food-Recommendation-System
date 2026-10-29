export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  ownerId: number;
  fullName: string;
  email: string;
  role: string;
  canteenId: number | null;
  expiresAt: string;
}

export interface RegisterOwnerRequest {
  fullName: string;
  email: string;
  password: string;
  canteenName?: string;
  contactPhone?: string;
  canteenAddress?: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ForgotPasswordResponse {
  message: string;
  resetToken?: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}
