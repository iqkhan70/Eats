import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { APP_CONFIG } from '../config/app.config';

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

    // Request interceptor to add auth token
    this.client.interceptors.request.use(
      async (config) => {
        const token = await AsyncStorage.getItem('access_token');
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
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
            hint: `Make sure Mobile BFF is running and accessible. Current API URL: ${APP_CONFIG.API_BASE_URL}. For phone testing, update config/app.config.ts with your computer's IP address.`
          });
        } else if (error.response) {
          console.error('❌ API Error:', {
            status: error.response.status,
            statusText: error.response.statusText,
            data: error.response.data,
            url: error.config?.url
          });
        } else if (error.request) {
          console.error('❌ Network Error:', {
            message: error.message,
            baseURL: this.client.defaults.baseURL,
            url: error.config?.url,
            hint: 'No response received. Check network connection and BFF status.'
          });
        }

        if (error.response?.status === 401) {
          // Handle unauthorized - clear token and redirect to login
          await AsyncStorage.removeItem('auth_token');
          // TODO: Navigate to login screen
        }
        return Promise.reject(error);
      }
    );
  }

  async get<T>(url: string, config?: AxiosRequestConfig) {
    return this.client.get<T>(url, config);
  }

  async post<T>(url: string, data?: any, config?: AxiosRequestConfig) {
    return this.client.post<T>(url, data, config);
  }

  async put<T>(url: string, data?: any, config?: AxiosRequestConfig) {
    return this.client.put<T>(url, data, config);
  }

  async delete<T>(url: string, config?: AxiosRequestConfig) {
    return this.client.delete<T>(url, config);
  }
}

export const api = new ApiClient();
