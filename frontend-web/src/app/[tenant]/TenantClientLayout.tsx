'use client';

import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { notFound } from 'next/navigation';

export default function TenantClientLayout({
  tenantSubdomain,
  children
}: {
  tenantSubdomain: string;
  children: React.ReactNode;
}) {
  const { data: tenant, isLoading, error } = useQuery({
    queryKey: ['tenant', tenantSubdomain],
    queryFn: async () => {
      const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint({
        subdomain: tenantSubdomain
      });
      return res.data;
    },
    retry: false
  });

  if (isLoading) {
    // We must render children to avoid Next.js layout constraint errors,
    // but we can wrap it in a generic/loading shell
    return (
      <div className="min-h-screen bg-white dark:bg-gray-900 font-sans text-gray-900 dark:text-gray-100">
        <header className="p-4 flex items-center gap-4 bg-blue-600 text-white shadow-md">
          <h1 className="text-xl font-bold animate-pulse">Loading Academy...</h1>
        </header>
        <main className="opacity-0 pointer-events-none absolute">{children}</main>
      </div>
    );
  }

  if (error || !tenant) {
    return (
      <div className="min-h-screen bg-white dark:bg-gray-900 font-sans text-gray-900 dark:text-gray-100">
        <header className="p-4 flex items-center gap-4 bg-red-600 text-white shadow-md">
          <h1 className="text-xl font-bold">Tenant Not Found</h1>
        </header>
        <main className="p-8 text-center">
          <p className="text-lg">The academy you are looking for does not exist or is currently unavailable.</p>
          <div className="hidden">{children}</div>
        </main>
      </div>
    );
  }

  const branding = tenant.branding;
  const style = {
    '--color-primary': branding?.primaryColor || '#1A73E8',
    '--color-secondary': branding?.secondaryColor || '#F4B400',
  } as React.CSSProperties;

  return (
    <div style={style} className="min-h-screen bg-white dark:bg-gray-900 font-sans text-gray-900 dark:text-gray-100">
      <header className="p-4 flex items-center gap-4 bg-[var(--color-primary)] text-white shadow-md">
        {branding?.logoUrl && (
          <img src={branding.logoUrl} alt={`${tenant.name} Logo`} className="h-10 w-auto" />
        )}
        <h1 className="text-xl font-bold">{tenant.name || 'AlphaZero Academy'}</h1>
      </header>
      <main>{children}</main>
    </div>
  );
}
