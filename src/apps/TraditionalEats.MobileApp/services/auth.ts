import AsyncStorage from '@react-native-async-storage/async-storage';
import { api } from './api';

export interface LoginCredentials {
  email: string;
  password: string;
}

export interface RegisterCredentials {
  email: string;
  password: string;
  phoneNumber?: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

class AuthService {
  private readonly ACCESS_TOKEN_KEY = 'access_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';

  async login(credentials: LoginCredentials): Promise<AuthResponse> {
    try {
      const response = await api.post<AuthResponse>('/MobileBff/auth/login', credentials);
      
      if (response.data) {
        await this.storeTokens(response.data.accessToken, response.data.refreshToken);
        return response.data;
      }
      
      throw new Error('Invalid response from server');
    } catch (error: any) {
      if (error.response?.status === 401) {
        throw new Error('Invalid email or password');
      }
      throw new Error(error.response?.data?.message || 'Login failed');
    }
  }

  async register(credentials: RegisterCredentials): Promise<void> {
    try {
      const response = await api.post('/MobileBff/auth/register', credentials);
      
      if (!response.data) {
        throw new Error('Registration failed');
      }
    } catch (error: any) {
      if (error.response?.status === 400) {
        throw new Error(error.response.data?.message || 'Email already exists');
      }
      throw new Error(error.response?.data?.message || 'Registration failed');
    }
  }

  async logout(): Promise<void> {
    try {
      const refreshToken = await AsyncStorage.getItem(this.REFRESH_TOKEN_KEY);
      if (refreshToken) {
        await api.post('/MobileBff/auth/logout', { refreshToken });
      }
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      await this.clearTokens();
    }
  }

  async getAccessToken(): Promise<string | null> {
    return await AsyncStorage.getItem(this.ACCESS_TOKEN_KEY);
  }

  async getRefreshToken(): Promise<string | null> {
    return await AsyncStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  async isAuthenticated(): Promise<boolean> {
    const token = await this.getAccessToken();
    return token !== null;
  }

  /**
   * Decode JWT token to extract claims
   * React Native compatible base64 decoding
   */
  private decodeToken(token: string): any {
    try {
      const base64Url = token.split('.')[1];
      let base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      
      // Add padding if needed
      while (base64.length % 4) {
        base64 += '=';
      }
      
      // React Native compatible base64 decode
      // Manual base64 decode implementation
      const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
      let decoded = '';
      let i = 0;
      
      while (i < base64.length) {
        const encoded1 = chars.indexOf(base64.charAt(i++));
        const encoded2 = chars.indexOf(base64.charAt(i++));
        const encoded3 = chars.indexOf(base64.charAt(i++));
        const encoded4 = chars.indexOf(base64.charAt(i++));
        
        const bitmap = (encoded1 << 18) | (encoded2 << 12) | (encoded3 << 6) | encoded4;
        
        if (encoded3 !== 64) decoded += String.fromCharCode((bitmap >> 16) & 255);
        if (encoded4 !== 64) decoded += String.fromCharCode((bitmap >> 8) & 255);
        if (encoded4 !== 64) decoded += String.fromCharCode(bitmap & 255);
      }
      
      return JSON.parse(decoded);
    } catch (error) {
      console.error('Error decoding token:', error);
      return null;
    }
  }

  /**
   * Get user roles from JWT token
   */
  async getUserRoles(): Promise<string[]> {
    const token = await this.getAccessToken();
    if (!token) {
      return [];
    }

    const decoded = this.decodeToken(token);
    if (!decoded) {
      return [];
    }

    // JWT roles can be in different claim names: 'role', 'roles', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
    const roles: string[] = [];
    
    if (decoded.role) {
      roles.push(...(Array.isArray(decoded.role) ? decoded.role : [decoded.role]));
    }
    if (decoded.roles) {
      roles.push(...(Array.isArray(decoded.roles) ? decoded.roles : [decoded.roles]));
    }
    if (decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']) {
      const roleClaim = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      roles.push(...(Array.isArray(roleClaim) ? roleClaim : [roleClaim]));
    }

    return [...new Set(roles)]; // Remove duplicates
  }

  /**
   * Check if user has a specific role
   */
  async isInRole(role: string): Promise<boolean> {
    const roles = await this.getUserRoles();
    return roles.includes(role);
  }

  /**
   * Check if user is an admin
   */
  async isAdmin(): Promise<boolean> {
    return await this.isInRole('Admin');
  }

  /**
   * Check if user is a vendor
   */
  async isVendor(): Promise<boolean> {
    return await this.isInRole('Vendor');
  }

  private async storeTokens(accessToken: string, refreshToken: string): Promise<void> {
    await AsyncStorage.setItem(this.ACCESS_TOKEN_KEY, accessToken);
    await AsyncStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
  }

  private async clearTokens(): Promise<void> {
    await AsyncStorage.removeItem(this.ACCESS_TOKEN_KEY);
    await AsyncStorage.removeItem(this.REFRESH_TOKEN_KEY);
  }

  async refreshAccessToken(): Promise<string | null> {
    try {
      const refreshToken = await this.getRefreshToken();
      if (!refreshToken) {
        return null;
      }

      const response = await api.post<AuthResponse>('/MobileBff/auth/refresh', { refreshToken });
      
      if (response.data) {
        await this.storeTokens(response.data.accessToken, response.data.refreshToken);
        return response.data.accessToken;
      }
      
      return null;
    } catch (error) {
      await this.clearTokens();
      return null;
    }
  }
}

export const authService = new AuthService();
