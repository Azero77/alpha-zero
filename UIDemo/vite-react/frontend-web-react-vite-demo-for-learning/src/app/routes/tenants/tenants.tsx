import { QueryClient } from '@tanstack/react-query';
import { type LoaderFunctionArgs } from 'react-router';
import { ContentLayout } from '@/components/layouts';
import { getTenantsQueryOptions } from '@/features/tenants/api/get-tenants';
import { TenantsList } from '@/features/tenants/components/tenants-list';

// eslint-disable-next-line react-refresh/only-export-components
export const tenantsLoader =
  (queryClient: QueryClient) =>
  async ({ request }: LoaderFunctionArgs) => {
    const url = new URL(request.url);
    const page = Number(url.searchParams.get('page')) || 1;
    const perPage = Number(url.searchParams.get('perPage')) || 10;
    const q = url.searchParams.get('q') || '';

    const query = getTenantsQueryOptions({ page, perPage, q });
    return (
      queryClient.getQueryData(query.queryKey) ??
      (await queryClient.query(query))
    );
  };

export const TenantsRoute = () => {
  return (
    <ContentLayout title="Tenants">
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <p className="text-sm text-gray-500">
            View and manage tenant academies, subdomains, and organizational settings.
          </p>
        </div>
        <TenantsList />
      </div>
    </ContentLayout>
  );
};

export const TenantsPage = TenantsRoute;
