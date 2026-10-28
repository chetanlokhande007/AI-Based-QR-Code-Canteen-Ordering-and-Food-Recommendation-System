export interface CanteenTableDto {
  tableId: number;
  canteenId: number;
  tableNumber: number;
  capacity: number;
  qrToken: string;
  isAvailable: boolean;
}

export interface CreateTableRequest {
  canteenId?: number;
  tableNumber: number;
  capacity: number;
}
