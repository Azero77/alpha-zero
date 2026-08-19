'use client';
import { useState } from 'react';
import { apiClient } from '@/api/client';
import { useRouter } from 'next/navigation';

export default function PrincipalLogin() {
  const [tenantId, setTenantId] = useState('00000000-0000-0000-0000-000000000000');
  const [username, setUsername] = useState('superadmin');
  const [password, setPassword] = useState('admin');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const router = useRouter();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const res = await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsLoginPrincipalLoginPrincipalEndpoint({
        tenantId,
        username,
        password
      });

      if (res.data?.token) {
        localStorage.setItem('principal_token', res.data.token);
        
        // If it's the global superadmin, go to dashboard
        if (tenantId === '00000000-0000-0000-0000-000000000000') {
          localStorage.setItem('auth_token', res.data.token);
          localStorage.setItem('tenant_id', tenantId);
          router.push('/');
        } else {
          // Redirect to tenant exchange via subdomain, passing principal token
          window.location.href = `http://${tenantId}.localhost:3000/login?pt=${res.data.token}`;
        }
      }
    } catch (err: any) {
      setError('Login failed. Please check your credentials.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex justify-center items-center min-h-screen bg-gray-50 dark:bg-gray-900 text-[var(--text-primary)]">
      <div className="bg-[var(--bg-color)] p-8 shadow-lg w-full max-w-md border-[3px] border-[var(--text-primary)]">
        <h2 className="text-2xl font-bold mb-6 text-center uppercase tracking-tighter">Principal Login</h2>
        
        {error && <div className="mb-4 text-red-500 text-sm p-3 border-2 border-red-500 font-bold">{error}</div>}
        
        <form onSubmit={handleLogin} className="space-y-4">
          <div>
            <label className="block text-sm font-bold mb-1 uppercase">Tenant ID</label>
            <input 
              type="text" 
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              className="w-full border-[3px] border-[var(--text-primary)] p-2 bg-transparent focus:outline-none focus:ring-0"
              required 
            />
          </div>
          <div>
            <label className="block text-sm font-bold mb-1 uppercase">Username</label>
            <input 
              type="text" 
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full border-[3px] border-[var(--text-primary)] p-2 bg-transparent focus:outline-none focus:ring-0"
              required 
            />
          </div>
          <div>
            <label className="block text-sm font-bold mb-1 uppercase">Password</label>
            <input 
              type="password" 
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full border-[3px] border-[var(--text-primary)] p-2 bg-transparent focus:outline-none focus:ring-0"
              required 
            />
          </div>
          <button 
            type="submit" 
            disabled={isLoading}
            className="w-full bg-[var(--text-primary)] text-[var(--bg-color)] py-3 font-bold uppercase hover:opacity-80 disabled:opacity-50 transition-opacity border-[3px] border-transparent hover:border-[var(--text-primary)]"
          >
            {isLoading ? 'Authenticating...' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  );
}
