import axios from 'axios';

// Default to the provided localhost URL
const BASE_URL = 'https://localhost:7016';

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: {
    'Content-Type': 'application/json',
    // Hardcoded for demo purposes as requested
    'X-Tenant-Id': 'tenant-1' 
  }
});

// Add a request interceptor for logging or auth if needed later
apiClient.interceptors.request.use((config) => {
  // Mocking auth header for RBAC
  config.headers.Authorization = `Bearer mock-token-admin`;
  return config;
});
