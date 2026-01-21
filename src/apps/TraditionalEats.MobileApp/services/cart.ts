import { api } from './api';

export interface Cart {
  cartId: string;
  customerId?: string;
  restaurantId?: string;
  subtotal: number;
  tax: number;
  deliveryFee: number;
  total: number;
  updatedAt: string;
  items: CartItem[];
}

export interface CartItem {
  cartItemId: string;
  cartId: string;
  menuItemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  selectedOptionsJson?: string;
}

class CartService {
  async getCart(): Promise<Cart | null> {
    try {
      const response = await api.get<Cart>('/MobileBff/cart');
      return response.data;
    } catch (error) {
      console.error('Error getting cart:', error);
      return null;
    }
  }

  async createCart(restaurantId?: string): Promise<string> {
    const request = restaurantId ? { restaurantId } : {};
    const response = await api.post<{ cartId: string }>('/MobileBff/cart', request);
    return response.data.cartId;
  }

  async addItemToCart(
    cartId: string,
    menuItemId: string,
    name: string,
    price: number,
    quantity: number = 1
  ): Promise<void> {
    await api.post(`/MobileBff/cart/${cartId}/items`, {
      menuItemId,
      name,
      price,
      quantity,
      options: null,
    });
  }

  async updateCartItemQuantity(cartId: string, cartItemId: string, quantity: number): Promise<void> {
    await api.put(`/MobileBff/cart/${cartId}/items/${cartItemId}`, { quantity });
  }

  async removeCartItem(cartId: string, cartItemId: string): Promise<void> {
    await api.delete(`/MobileBff/cart/${cartId}/items/${cartItemId}`);
  }

  async clearCart(cartId: string): Promise<void> {
    await api.delete(`/MobileBff/cart/${cartId}`);
  }

  async placeOrder(cartId: string, deliveryAddress: string): Promise<string> {
    const response = await api.post<{ orderId: string }>('/MobileBff/orders/place', {
      cartId,
      deliveryAddress,
      idempotencyKey: null,
    });
    return response.data.orderId;
  }
}

export const cartService = new CartService();
