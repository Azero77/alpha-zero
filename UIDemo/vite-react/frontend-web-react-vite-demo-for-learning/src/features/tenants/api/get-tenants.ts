import { queryOptions, useQuery } from "@tanstack/react-query";
import { api } from "../../../lib/api-client";
export type GetTenantsQueryRequest = {
    page?: number;
    perPage?: number;
    q?: string;
};

export const getTenants =  ({ page = 1, perPage = 10, q = '' }: GetTenantsQueryRequest = {}) : Promise<{data : GetTenantsResponse}> => 
    {
        return api.get('/tenants', {
            params: {
                page: page,
                perPage : perPage,
                q : q
            }
        })
    };

export const getTenantsQueryOptions = (getTenantsQueryRequest : GetTenantsQueryRequest = {}) => {
    return queryOptions({
        queryKey: ['tenants', getTenantsQueryRequest],
        queryFn: () => getTenants(getTenantsQueryRequest)
        });
}

export const useGetTenantsQuery = (getTenantsQueryRequest : GetTenantsQueryRequest = {}) => {
    return useQuery({...getTenantsQueryOptions(getTenantsQueryRequest)});
}
export type Tenant = {
  id: string;
  name: string;
  subdomain: string;
  logoUrl: string;
  primaryColor: string;
  secondaryColor: string;
  status: string;
  createdAt: string;
};

export type GetTenantsResponse = {
  items: Tenant[];
  totalCount: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
};