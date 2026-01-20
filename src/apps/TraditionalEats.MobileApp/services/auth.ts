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
