import axios from 'axios';
import { config } from '../../core/config';

export const apiClient = axios.create({
  baseURL: config.apiUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add tenant and authorization headers to every request
apiClient.interceptors.request.use((req) => {
  if (config.tenantId) {
    req.headers['X-TenantId'] = config.tenantId;
  }
  if (config.authToken) {
    req.headers['Authorization'] = `Bearer ${config.authToken}`;
  }
  return req;
});
