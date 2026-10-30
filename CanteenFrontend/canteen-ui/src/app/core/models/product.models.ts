export interface ProductDto {
  productId: number;
  canteenId: number;
  categoryId: number;
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  isAvailable: boolean;
  foodType?: string;
  imageUrl?: string;
  createdAt?: string;
}

export interface CreateProductRequest {
  canteenId?: number;
  categoryId: number;
  name: string;
  description?: string;
  price: number;
  stockQuantity?: number;
  foodType?: string;
  imageUrl?: string;
}

export interface UpdateProductRequest {
  canteenId?: number;
  categoryId?: number;
  name: string;
  description?: string;
  price: number;
  stockQuantity: number;
  isAvailable: boolean;
  foodType?: string;
  imageUrl?: string;
}
