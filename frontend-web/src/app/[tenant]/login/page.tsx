'use client';
import { useState } from 'react';
import { apiClient } from '@/api/client';
import { useRouter } from 'next/navigation';

export default function Login({ params }: { params: { tenant: string } }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const router = useRouter();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      // Basic mock fingerprint for now
      const fingerprint = typeof window !== 'undefined' && localStorage.getItem('device_fp') 
        || crypto.randomUUID();
      if (typeof window !== 'undefined') localStorage.setItem('device_fp', fingerprint);

      const res = await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint({
        tenantId: params.tenant, // Assuming params.tenant is the ID or subdomain mapped to ID
        username,
        password,
        deviceFingerprint: fingerprint,
        platform: 0 // Web
      });

      // Assuming res.data contains the token
      if (res.data?.token) {
        // In a real app, save to HTTP-only cookie or secure storage
        localStorage.setItem('auth_token', res.data.token);
        if (res.data.tenantUserId) {
          localStorage.setItem('student_id', res.data.tenantUserId);
        }
        router.push(`/${params.tenant}`);
      }
    } catch (err: any) {
      if (err.status === 403 || err.data?.detail?.includes('Device') || err.data?.title?.includes('Device')) {
        // Device limit warning
        setError('Device limit reached. Please manage your devices.');
        router.push(`/${params.tenant}/device-lock`);
      } else {
        setError('Login failed. Please check your credentials.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex justify-center items-center min-h-[calc(100vh-80px)]">
      <div className="bg-white dark:bg-gray-800 p-8 rounded shadow-lg w-full max-w-md">
        <h2 className="text-2xl font-bold mb-6 text-center text-[var(--color-primary)]">Welcome Back</h2>
        
        {error && <div className="mb-4 text-red-500 text-sm bg-red-50 dark:bg-red-900/20 p-3 rounded">{error}</div>}
        
        <form onSubmit={handleLogin} className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-1">Username</label>
            <input 
              type="text" 
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full border p-2 rounded focus:ring-2 focus:ring-[var(--color-primary)] focus:outline-none dark:bg-gray-700 dark:border-gray-600"
              required 
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">Password</label>
            <input 
              type="password" 
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full border p-2 rounded focus:ring-2 focus:ring-[var(--color-primary)] focus:outline-none dark:bg-gray-700 dark:border-gray-600"
              required 
            />
          </div>
          <button 
            type="submit" 
            disabled={isLoading}
            className="w-full bg-[var(--color-primary)] text-white py-2 rounded font-semibold hover:opacity-90 transition disabled:opacity-50"
          >
            {isLoading ? 'Logging in...' : 'Login'}
          </button>
        </form>
        
        <div className="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
          Don't have an account? <a href={`/${params.tenant}/register`} className="text-[var(--color-secondary)] hover:underline">Register</a>
        </div>
      </div>
    </div>
  );
}
