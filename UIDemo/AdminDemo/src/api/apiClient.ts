import axios from 'axios';
import { config } from '../config';

export const apiClient = axios.create({
  baseURL: config.BASE_URL,
  headers: {
    'Content-Type': 'application/json',
    'X-Tenant-Id': config.TENANT_ID
  }
});

// Add a request interceptor for logging or auth if needed later
apiClient.interceptors.request.use((config) => {
  // Mocking auth header for RBAC
  config.headers.Authorization = `Bearer mock-token-admin`;
  return config;
});
