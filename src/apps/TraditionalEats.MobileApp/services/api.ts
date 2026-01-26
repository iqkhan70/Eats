import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { APP_CONFIG } from '../config/app.config';
import { cartSessionService } from './cartSession';

class ApiClient {
  private client: AxiosInstance;

  constructor() {
    this.client = axios.create({
      baseURL: APP_CONFIG.API_BASE_URL,
      timeout: 10000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Request interceptor to add auth token and cart session ID
    this.client.interceptors.request.use(
      async (config) => {
        // Add auth token if available
        const token = await AsyncStorage.getItem('access_token');
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }

        // Add cart session ID for guest cart management
        const sessionId = await cartSessionService.getOrCreateSessionId();
        config.headers['X-Cart-Session-Id'] = sessionId;

        return config;
      },
      (error) => {
        return Promise.reject(error);
      }
    );

    // Response interceptor for error handling
    this.client.interceptors.response.use(
      (response) => response,
      async (error) => {
        // Log detailed error information
        if (error.code === 'ECONNREFUSED' || error.code === 'ENOTFOUND') {
          console.error('❌ Connection Error:', {
            message: error.message,
            code: error.code,
            baseURL: this.client.defaults.baseURL,
            url: error.config?.url,
            hint: `Make sure Mobile BFF is running and accessible. Current API URL: ${APP_CONFIG.API_BASE_URL}. For phone testing, update config/app.config.ts with your computer's IP address.`,
          });
        } else if (error.response) {
          const url = error.config?.url || '';
          const method = error.config?.method?.toUpperCase() || '';
          const status = error.response.status;

          // Don't log 404/204 errors for GET cart endpoints (empty cart is valid)
          const isCartGetEndpoint = url.includes('/cart') && method === 'GET';
          const isExpectedEmptyResponse = status === 404 || status === 204;

          // Don't log 401 errors for vendor/admin endpoints if user might not be authenticated
          // These are handled by the UI components with user-friendly messages
          const isAuthEndpoint = url.includes('/vendor/') || url.includes('/admin/');
          const isUnauthorized = status === 401;

          // Only suppress logging for:
          // 1. GET cart requests that return 404/204 (empty cart is valid)
          // 2. 401 errors on vendor/admin endpoints (handled by UI)
          const shouldSuppress =
            (isCartGetEndpoint && isExpectedEmptyResponse) ||
            (isAuthEndpoint && isUnauthorized);

          if (!shouldSuppress) {
            console.error('❌ API Error:', {
              status: status,
              statusText: error.response.statusText,
              data: error.response.data,
              url: url,
              method: method,
            });
          }
        } else if (error.request) {
          console.error('❌ Network Error:', {
            message: error.message,
            baseURL: this.client.defaults.baseURL,
            url: error.config?.url,
            hint: 'No response received. Check network connection and BFF status.',
          });
        }

        if (error.response?.status === 401) {
          // Handle unauthorized - clear token and redirect to login
          await AsyncStorage.removeItem('access_token');
          // TODO: Navigate to login screen
        }

        return Promise.reject(error);
      }
    );
  }

  async get<T>(url: string, config?: AxiosRequestConfig): Promise<AxiosResponse<T>> {
    return this.client.get<T>(url, config);
  }

  async post<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<AxiosResponse<T>> {
    return this.client.post<T>(url, data, config);
  }

  async put<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<AxiosResponse<T>> {
    return this.client.put<T>(url, data, config);
  }

  // ✅ FIX: implement PATCH
  async patch<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<AxiosResponse<T>> {
    return this.client.patch<T>(url, data, config);
  }

  async delete<T>(url: string, config?: AxiosRequestConfig): Promise<AxiosResponse<T>> {
    return this.client.delete<T>(url, config);
  }
}

export const api = new ApiClient();
