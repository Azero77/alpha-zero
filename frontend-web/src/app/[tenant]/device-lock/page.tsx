'use client';
import { useState } from 'react';
import { apiClient } from '@/api/client';
import { useRouter } from 'next/navigation';

export default function DeviceLock({ params }: { params: { tenant: string } }) {
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const router = useRouter();

  const handleSetDevice = async () => {
    setError('');
    setIsLoading(true);

    try {
      const fingerprint = typeof window !== 'undefined' && localStorage.getItem('device_fp') 
        || crypto.randomUUID();

      await apiClient.identity.alphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint({
        deviceId: fingerprint
      });

      // Redirect back to login so they can authenticate successfully
      router.push(`/${params.tenant}/login`);
    } catch (err: any) {
      setError('Failed to set main device. You may need to contact an administrator.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex justify-center items-center min-h-[calc(100vh-80px)] p-4">
      <div className="bg-white dark:bg-gray-800 p-8 rounded shadow-lg w-full max-w-md text-center">
        <div className="text-4xl mb-4">🔒</div>
        <h2 className="text-2xl font-bold mb-4 text-[var(--color-primary)]">Device Limit Reached</h2>
        
        <p className="text-gray-600 dark:text-gray-300 mb-6">
          Your account is restricted to a single primary device. You are attempting to log in from a new device.
          Would you like to set this device as your main device?
        </p>

        {error && <div className="mb-4 text-red-500 text-sm bg-red-50 dark:bg-red-900/20 p-3 rounded">{error}</div>}
        
        <button 
          onClick={handleSetDevice}
          disabled={isLoading}
          className="w-full bg-[var(--color-primary)] text-white py-3 rounded font-semibold hover:opacity-90 transition disabled:opacity-50 mb-3"
        >
          {isLoading ? 'Processing...' : 'Set as Main Device'}
        </button>
        
        <button 
          onClick={() => router.push(`/${params.tenant}/login`)}
          className="w-full border border-gray-300 dark:border-gray-600 py-3 rounded font-semibold hover:bg-gray-50 dark:hover:bg-gray-700 transition"
        >
          Cancel & Return
        </button>
      </div>
    </div>
  );
}
