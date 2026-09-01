import { QueryClient, useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { DashboardLayout } from '@/components/layouts/dashboard-layout';
import { TenantsRoute, tenantsLoader } from './routes/tenants/tenants';

const createAppRouter = (queryClient: QueryClient) => {
  return createBrowserRouter([
    
    {
      path: '/tenants',
      element: (
        <DashboardLayout>
          <TenantsRoute />
        </DashboardLayout>
      ),
      loader: tenantsLoader(queryClient),
    },
  ]);
};

export const AppRouter = () => {
  const queryClient = useQueryClient();
  const router = useMemo(() => createAppRouter(queryClient), [queryClient]);
  return <RouterProvider router={router} />;
};
