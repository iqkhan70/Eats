import AsyncStorage from '@react-native-async-storage/async-storage';
import { api } from './api';

export interface LoginCredentials {
  email: string;
  password: string;
}

export interface RegisterCredentials {
  firstName: string;
  lastName: string;
  displayName?: string;
  email: string;
  password: string;
  phoneNumber: string;
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

  async forgotPassword(email: string): Promise<{ success: boolean; message: string }> {
    try {
      const response = await api.post<{ success: boolean; message: string }>(
        '/MobileBff/auth/forgot-password',
        { email }
      );
      return response.data ?? { success: true, message: 'If an account with that email exists, a password reset link has been sent.' };
    } catch (error: any) {
      const msg = error.response?.data?.message ?? 'Failed to send reset link. Please try again.';
      throw new Error(msg);
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

  /**
   * ✅ More reliable auth check:
   * - token exists
   * - token decodes
   * - token is not expired (based on exp claim)
   * If expired, clears tokens.
   */
  async isAuthenticated(): Promise<boolean> {
    const token = await this.getAccessToken();
    if (!token) return false;

    const decoded = this.decodeToken(token);
    if (!decoded) return false;

    if (this.isTokenExpired(decoded)) {
      await this.clearTokens();
      return false;
    }

    return true;
  }

  /**
   * Decode JWT token to extract claims
   * React Native compatible base64 decoding
   */
  private decodeToken(token: string): any {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) {
        return null;
      }

      const base64Url = parts[1];
      if (!base64Url) {
        return null;
      }

      // Convert base64url to base64
      let base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');

      // Add padding if needed
      while (base64.length % 4) {
        base64 += '=';
      }

      // React Native compatible base64 decode
      const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
      const lookup = new Array(256);
      for (let i = 0; i < chars.length; i++) {
        lookup[chars.charCodeAt(i)] = i;
      }

      let bufferLength = base64.length * 0.75;
      if (base64[base64.length - 1] === '=') {
        bufferLength--;
        if (base64[base64.length - 2] === '=') {
          bufferLength--;
        }
      }

      const bytes = new Uint8Array(bufferLength);
      let p = 0;

      for (let i = 0; i < base64.length; i += 4) {
        const encoded1 = lookup[base64.charCodeAt(i)];
        const encoded2 = lookup[base64.charCodeAt(i + 1)];
        const encoded3 = lookup[base64.charCodeAt(i + 2)];
        const encoded4 = lookup[base64.charCodeAt(i + 3)];

        bytes[p++] = (encoded1 << 2) | (encoded2 >> 4);
        bytes[p++] = ((encoded2 & 15) << 4) | (encoded3 >> 2);
        bytes[p++] = ((encoded3 & 3) << 6) | (encoded4 & 63);
      }

      // Convert bytes to string using TextDecoder if available, otherwise manual conversion
      let decoded: string;
      if (typeof TextDecoder !== 'undefined') {
        decoded = new TextDecoder('utf-8').decode(bytes);
      } else {
        decoded = '';
        for (let i = 0; i < bytes.length; i++) {
          decoded += String.fromCharCode(bytes[i]);
        }
      }

      return JSON.parse(decoded);
    } catch (error) {
      console.error('Error decoding token:', error);
      return null;
    }
  }

  /**
   * ✅ Extract email from JWT claims
   * Handles common claim names across identity providers.
   */
  async getUserEmail(): Promise<string | null> {
    const token = await this.getAccessToken();
    if (!token) return null;

    const decoded = this.decodeToken(token);
    if (!decoded) return null;

    return (
      decoded.email ||
      decoded.preferred_username ||
      decoded.unique_name ||
      decoded.upn ||
      decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
      decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ||
      null
    );
  }

  /**
   * ✅ Token expiration based on exp (seconds since epoch)
   */
  private isTokenExpired(decoded: any): boolean {
    const exp = decoded?.exp;
    if (!exp) return false; // if exp not present, treat as not expired
    const nowSeconds = Math.floor(Date.now() / 1000);
    return nowSeconds >= exp;
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

    // JWT roles can be in different claim names:
    // 'role', 'roles', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
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
