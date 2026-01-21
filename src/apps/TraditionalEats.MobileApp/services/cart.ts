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
      // Check if user is authenticated
      const { authService } = await import('./auth');
      const isAuthenticated = await authService.isAuthenticated();
      const token = await authService.getAccessToken();
      console.log('Getting cart from MobileBff...', {
        isAuthenticated,
        hasToken: !!token,
        tokenPreview: token ? token.substring(0, 20) + '...' : 'none'
      });
      
      const response = await api.get<Cart>('/MobileBff/cart');
      console.log('Cart response status:', response.status);
      if (response.status === 200 || response.status === 204) {
        // Handle empty response (204 No Content)
        if (response.status === 204 || !response.data) {
          console.log('Cart is empty (204 or no data)');
          return null;
        }
        // Ensure items array exists
        if (response.data && !response.data.items) {
          response.data.items = [];
        }
        console.log('Cart loaded successfully:', {
          cartId: response.data.cartId,
          itemCount: response.data.items?.length || 0,
          customerId: response.data.customerId
        });
        return response.data;
      }
      return null;
    } catch (error: any) {
      // Handle 404 (cart not found) as a valid empty cart
      if (error.response?.status === 404 || error.response?.status === 204) {
        console.log('Cart not found (404/204) - returning null');
        console.log('This might mean:', {
          notLoggedIn: 'User might not be logged in on mobile',
          noCart: 'User has no cart yet',
          differentAccount: 'Logged in with different account than webapp'
        });
        return null;
      }
      console.error('Error getting cart:', error);
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
      console.log('Creating cart with request:', request);
      const response = await api.post<{ cartId: string }>('/MobileBff/cart', request);
      
      if (!response.data || !response.data.cartId) {
        throw new Error('Cart creation failed: No cartId returned');
      }
      
      console.log('Cart created successfully:', response.data.cartId);
      return response.data.cartId;
    } catch (error: any) {
      console.error('Error creating cart:', error);
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
    quantity: number = 1
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
      
      console.log('addItemToCart: Adding item to cart:', { cartId, menuItemId, name, price, quantity });
      
      const requestBody = {
        menuItemId: menuItemId,
        name: name,
        price: price,
        quantity: quantity,
        options: null as { [key: string]: string } | null
      };
      
      console.log('addItemToCart: Request body:', JSON.stringify(requestBody));
      console.log('addItemToCart: Sending to URL:', `/MobileBff/cart/${cartId}/items`);
      
      const response = await api.post(`/MobileBff/cart/${cartId}/items`, requestBody);
      console.log('Item added to cart successfully:', response.status);
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
