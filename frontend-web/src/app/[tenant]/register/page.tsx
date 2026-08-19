'use client';
import { useState } from 'react';
import { apiClient } from '@/api/client';
import { useRouter } from 'next/navigation';

export default function Register({ params }: { params: { tenant: string } }) {
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const router = useRouter();

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const fingerprint = typeof window !== 'undefined' && localStorage.getItem('device_fp') 
        || crypto.randomUUID();
      if (typeof window !== 'undefined') localStorage.setItem('device_fp', fingerprint);

      // The registration endpoint
      // Registration is not fully mapped in backend, simulate a delay
      await new Promise(resolve => setTimeout(resolve, 1000));

      // Assuming res.data contains success or token
      router.push(`/${params.tenant}/login`);
    } catch (err: any) {
      setError('Registration failed. Please check your inputs.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex justify-center items-center min-h-[calc(100vh-80px)]">
      <div className="bg-white dark:bg-gray-800 p-8 rounded shadow-lg w-full max-w-md">
        <h2 className="text-2xl font-bold mb-6 text-center text-[var(--color-primary)]">Create an Account</h2>
        
        {error && <div className="mb-4 text-red-500 text-sm bg-red-50 dark:bg-red-900/20 p-3 rounded">{error}</div>}
        
        <form onSubmit={handleRegister} className="space-y-4">
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
            <label className="block text-sm font-medium mb-1">Email</label>
            <input 
              type="email" 
              value={email}
              onChange={(e) => setEmail(e.target.value)}
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
            className="w-full bg-[var(--color-secondary)] text-white py-2 rounded font-semibold hover:opacity-90 transition disabled:opacity-50"
          >
            {isLoading ? 'Registering...' : 'Register'}
          </button>
        </form>
        
        <div className="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
          Already have an account? <a href={`/${params.tenant}/login`} className="text-[var(--color-primary)] hover:underline">Login</a>
        </div>
      </div>
    </div>
  );
}
