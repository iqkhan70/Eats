import AsyncStorage from '@react-native-async-storage/async-storage';
import { api } from './api';

export interface LoginCredentials {
  email: string;
  password: string;
}

export interface RegisterData {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  expiresIn: number;
}

class AuthService {
  private readonly TOKEN_KEY = 'auth_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';

  async login(credentials: LoginCredentials): Promise<AuthResponse> {
    const response = await api.post<AuthResponse>('/auth/login', credentials);
    await this.storeTokens(response.data);
    return response.data;
  }

  async register(data: RegisterData): Promise<AuthResponse> {
    const response = await api.post<AuthResponse>('/auth/register', data);
    await this.storeTokens(response.data);
    return response.data;
  }

  async logout(): Promise<void> {
    await AsyncStorage.multiRemove([this.TOKEN_KEY, this.REFRESH_TOKEN_KEY]);
  }

  async getToken(): Promise<string | null> {
    return await AsyncStorage.getItem(this.TOKEN_KEY);
  }

  async isAuthenticated(): Promise<boolean> {
    const token = await this.getToken();
    return token !== null;
  }

  private async storeTokens(authResponse: AuthResponse): Promise<void> {
    await AsyncStorage.multiSet([
      [this.TOKEN_KEY, authResponse.token],
      [this.REFRESH_TOKEN_KEY, authResponse.refreshToken],
    ]);
  }

  async refreshToken(): Promise<AuthResponse> {
    const refreshToken = await AsyncStorage.getItem(this.REFRESH_TOKEN_KEY);
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    const response = await api.post<AuthResponse>('/auth/refresh', {
      refreshToken,
    });
    await this.storeTokens(response.data);
    return response.data;
  }
}

export const authService = new AuthService();
