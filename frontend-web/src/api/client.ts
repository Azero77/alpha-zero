import { Api } from './ApiClient';

// Assuming the API is running on localhost:5053 for development
export const apiClient = new Api({
  baseUrl: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5053',
  securityWorker: (securityData: any | null) => {
    if (typeof window !== 'undefined') {
      const token = localStorage.getItem('auth_token');
      if (token) {
        return {
          headers: {
            Authorization: `Bearer ${token}`
          }
        };
      }
    }
    return {};
  }
});
