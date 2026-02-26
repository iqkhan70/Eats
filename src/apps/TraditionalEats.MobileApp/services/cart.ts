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
      if (response.status === 200 || response.status === 204) {
        // Handle empty response (204 No Content)
        if (response.status === 204 || !response.data) {
          return null;
        }
        // Ensure items array exists
        if (response.data && !response.data.items) {
          response.data.items = [];
        }
        return response.data;
      }
      return null;
    } catch (error: any) {
      // Handle 404 (cart not found) as a valid empty cart
      if (error.response?.status === 404 || error.response?.status === 204) {
        return null;
      }
      if (error.response) {
        console.error('Response status:', error.response.status);
        console.error('Response data:', error.response.data);
      }
      return null;
    }
  }

  async createCart(restaurantId?: string): Promise<string> {
    try {
      const request = restaurantId ? { restaurantId } : {};
      const response = await api.post<{ cartId: string }>('/MobileBff/cart', request);
      
      if (!response.data || !response.data.cartId) {
        throw new Error('Cart creation failed: No cartId returned');
      }
      
      return response.data.cartId;
    } catch (error: any) {
      if (error.response) {
        console.error('Response status:', error.response.status);
        console.error('Response data:', JSON.stringify(error.response.data));
      }
      throw error;
    }
  }

  async addItemToCart(
    cartId: string,
    menuItemId: string,
    name: string,
    price: number,
    quantity: number = 1,
    isCustomRequest: boolean = false,
  ): Promise<void> {
    try {
      // Validate cartId
      if (!cartId || cartId.trim() === '') {
        throw new Error(`Invalid CartId: cartId is empty or null`);
      }
      
      // Validate GUID format for cartId
      if (!/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(cartId)) {
        throw new Error(`Invalid CartId format: ${cartId}`);
      }
      
      // Validate menuItemId
      if (!menuItemId || menuItemId.trim() === '') {
        throw new Error(`Invalid MenuItemId: menuItemId is empty or null`);
      }
      
      // Validate GUID format for menuItemId
      if (!/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(menuItemId)) {
        throw new Error(`Invalid MenuItemId format: ${menuItemId}`);
      }
      
      const requestBody = {
        menuItemId: menuItemId,
        isCustomRequest: isCustomRequest,
        name: name,
        price: price,
        quantity: quantity,
        options: null as { [key: string]: string } | null
      };
      
      await api.post(`/MobileBff/cart/${cartId}/items`, requestBody);
    } catch (error: any) {
      console.error('Error adding item to cart:', error);
      if (error.response) {
        console.error('Response status:', error.response.status);
        console.error('Response data:', JSON.stringify(error.response.data, null, 2));
        // Re-throw with more context
        const errorMessage = error.response.data?.error || error.response.data?.message || error.response.data?.title || `Failed to add item: ${error.response.status}`;
        throw new Error(errorMessage);
      }
      throw error;
    }
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

  async placeOrder(
    cartId: string,
    deliveryAddress: string,
    specialInstructions?: string,
    successUrl?: string,
    cancelUrl?: string
  ): Promise<{ orderId: string; checkoutUrl?: string; error?: string }> {
    try {
      const body: Record<string, unknown> = {
        cartId,
        deliveryAddress,
        specialInstructions: specialInstructions || null,
        idempotencyKey: null,
      };
      if (successUrl) body.successUrl = successUrl;
      if (cancelUrl) body.cancelUrl = cancelUrl;
      const response = await api.post<{
        orderId: string;
        checkoutUrl?: string;
        error?: string;
      }>('/MobileBff/orders/place', body);
      return {
        orderId: response.data.orderId,
        checkoutUrl: response.data.checkoutUrl ?? undefined,
        error: response.data.error ?? undefined,
      };
    } catch (error: any) {
      // Handle BadRequest (400) - vendor not set up for payments
      if (error.response?.status === 400) {
        const errorMessage = error.response.data?.error || error.response.data?.message || 
          'This restaurant is not set up to accept payments yet. Please contact the restaurant directly.';
        throw new Error(errorMessage);
      }
      throw error;
    }
  }
}

export const cartService = new CartService();
