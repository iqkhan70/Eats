export interface Restaurant {
  restaurantId: string;
  name: string;
  cuisineType?: string;
  address: string;
  rating?: number;
  reviewCount?: number;
  imageUrl?: string;
  deliveryTime?: string;
  minimumOrder?: number;
}

export interface MenuItem {
  menuItemId: string;
  name: string;
  description?: string;
  price: number;
  imageUrl?: string;
  categoryId: string;
  dietaryTags?: string[];
}

export interface Order {
  orderId: string;
  restaurantId: string;
  restaurantName: string;
  status: OrderStatus;
  items: OrderItem[];
  total: number;
  createdAt: string;
  estimatedDeliveryTime?: string;
}

export interface OrderItem {
  menuItemId: string;
  name: string;
  quantity: number;
  price: number;
  options?: OrderItemOption[];
}

export interface OrderItemOption {
  optionId: string;
  name: string;
  value: string;
}

export type OrderStatus = 
  | 'Pending'
  | 'Confirmed'
  | 'Preparing'
  | 'Ready'
  | 'OutForDelivery'
  | 'Delivered'
  | 'Cancelled';

export interface Address {
  addressId: string;
  street: string;
  city: string;
  state: string;
  zipCode: string;
  country: string;
  isDefault: boolean;
}

export interface User {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  addresses?: Address[];
}
