import { apiClient } from '@/api/client';
import { notFound } from 'next/navigation';

export default async function TenantLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ tenant: string }>;
}) {
  let tenant;
  try {
    const resolvedParams = await params;
    // Lookup tenant
    const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint({
      subdomain: resolvedParams.tenant
    });
    tenant = res.data;
  } catch {
    // If tenant not found or other error
    notFound();
  }

  if (!tenant) {
    notFound();
  }

  // Apply branding as CSS variables on a wrapper div
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
