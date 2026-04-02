import axios from 'axios';
import { config } from '../../core/config';

export const apiClient = axios.create({
  baseURL: config.apiUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add tenant header if available
apiClient.interceptors.request.use((req) => {
  if (config.tenantId) {
    req.headers['X-TenantId'] = config.tenantId;
  }
  return req;
});
