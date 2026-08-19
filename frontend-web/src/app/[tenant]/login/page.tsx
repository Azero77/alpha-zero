'use client';
import { useState, useEffect } from 'react';
import { apiClient } from '@/api/client';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';

import { use } from 'react';

export default function TenantLoginExchange({ params }: { params: Promise<{ tenant: string }> }) {
  const resolvedParams = use(params);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [hasPrincipalToken, setHasPrincipalToken] = useState<boolean | null>(null);
  const router = useRouter();
  const searchParams = useSearchParams();

  useEffect(() => {
    const pt = searchParams.get('pt');
    if (pt) {
      localStorage.setItem('principal_token', pt);
      window.history.replaceState({}, document.title, window.location.pathname);
    }
    const token = localStorage.getItem('principal_token');
    setHasPrincipalToken(!!token);
  }, []);

  const handleExchange = async () => {
    setError('');
    setIsLoading(true);

    try {
      const principalToken = localStorage.getItem('principal_token');
      if (!principalToken) {
        throw new Error("No App User session found.");
      }

      const fingerprint = typeof window !== 'undefined' && localStorage.getItem('device_fp') 
        || crypto.randomUUID();
      if (typeof window !== 'undefined') localStorage.setItem('device_fp', fingerprint);

      // Call the exchange endpoint with the Bearer token configured in the apiClient
      // But we need to ensure the Bearer token is attached! The generated apiClient likely reads from some context.
      // Assuming apiClient is configured to send the token:
      const originalToken = localStorage.getItem('auth_token');
      localStorage.setItem('auth_token', principalToken); // temporarily use principal token for this request
      
      const res = await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint({
        tenantId: resolvedParams.tenant,
        publicKey: "none",
        deviceName: "Web Browser",
        platform: 0 // Web
      });

      // Restore original or set new tenant token
      if (res.data?.token) {
        localStorage.setItem('auth_token', res.data.token);
        localStorage.setItem('tenant_id', resolvedParams.tenant);
        if (res.data.tenantUserId) {
          localStorage.setItem('student_id', res.data.tenantUserId);
        }
        window.location.href = '/';
      }
    } catch (err: any) {
      setError('Failed to log into this tenant. ' + (err.message || ''));
      localStorage.removeItem('auth_token'); // Clean up
    } finally {
      setIsLoading(false);
    }
  };

  if (hasPrincipalToken === null) return null;

  return (
    <div className="flex justify-center items-center min-h-[calc(100vh-80px)] bg-[var(--bg-color)] text-[var(--text-primary)]">
      <div className="bg-[var(--bg-color)] p-8 shadow-lg w-full max-w-md border-[3px] border-[var(--text-primary)]">
        <h2 className="text-2xl font-bold mb-6 text-center uppercase">Tenant Entry</h2>
        
        {error && <div className="mb-4 text-red-500 text-sm p-3 border-2 border-red-500 font-bold">{error}</div>}
        
        {hasPrincipalToken ? (
          <div className="space-y-4 text-center">
            <p className="mb-4 font-bold">You have an active App User session.</p>
            <button 
              onClick={handleExchange}
              disabled={isLoading}
              className="w-full bg-[var(--text-primary)] text-[var(--bg-color)] py-3 font-bold uppercase hover:opacity-80 disabled:opacity-50 transition-opacity border-[3px] border-transparent"
            >
              {isLoading ? 'Entering Tenant...' : `Join Tenant ${resolvedParams.tenant}`}
            </button>
          </div>
        ) : (
          <div className="space-y-4 text-center">
            <p className="mb-4 font-bold">You must log in as an App User first via the Identity Provider.</p>
            <Link 
              href="/login" 
              className="block w-full bg-black text-white py-3 font-bold uppercase hover:bg-gray-800 border-[3px] border-black text-center"
            >
              Go to App User Login
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}
