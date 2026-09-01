import * as React from 'react';                                                                                                                                                                                                        
    import { useSearchParams } from 'react-router';                                                                                                                                                                                        
    import { Spinner } from '@/components/ui/spinner';                                                                                                                                                                                     
    import { Table } from '@/components/ui/table';                                                                                                                                                                                         
    import { Button } from '@/components/ui/button';                                                                                                                                                                                       
    import { Link } from '@/components/ui/link';                                                                                                                                                                                           
    import { useGetTenantsQuery, type GetTenantsQueryRequest, type Tenant } from '../api/get-tenants';                                                                                                                                                                               
                                                                                                                                                                                                                                           
    export type TenantsListProps = {                                                                                                                                                                                                       
      getTenantsQueryRequest?: GetTenantsQueryRequest;                                                                                                                                                                                     
    };                                                                                                                                                                                                                                     
                                                                                                                                                                                                                                           
    export const TenantsList: React.FC<TenantsListProps> = ({                                                                                                                                                                              
      getTenantsQueryRequest,                                                                                                                                                                                                              
    }) => {                                                                                                                                                                                                                                
      const [searchParams] = useSearchParams();                                                                                                                                                                                            
      const page =                                                                                                                                                                                                                         
        getTenantsQueryRequest?.page ??                                                                                                                                                                                                    
        (Number(searchParams.get('page')) || 1);                                                                                                                                                                                           
      const perPage =                                                                                                                                                                                                                      
        getTenantsQueryRequest?.perPage ??                                                                                                                                                                                                 
        (Number(searchParams.get('perPage')) || 10);                                                                                                                                                                                       
      const q = getTenantsQueryRequest?.q ?? (searchParams.get('q') || '');                                                                                                                                                                
                                                                                                                                                                                                                                           
      const tenantsQuery = useGetTenantsQuery({                                                                                                                                                                                                                      
          page,                                                                                                                                                                                                                            
          perPage,                                                                                                                                                                                                                         
          q,                                                                                                                                                                                                                               
      });                                                                                                                                                                                                                                  
                                                                                                                                                                                                                                           
      // 1. Loading State                                                                                                                                                                                                                  
      if (tenantsQuery.isLoading) {                                                                                                                                                                                                        
        return (                                                                                                                                                                                                                           
          <div className="flex h-48 w-full items-center justify-center">                                                                                                                                                                   
            <Spinner size="lg" />                                                                                                                                                                                                          
          </div>                                                                                                                                                                                                                           
        );                                                                                                                                                                                                                                 
      }                                                                                                                                                                                                                                    
                                                                                                                                                                                                                                           
      // 2. Error State with Retry                                                                                                                                                                                                         
      if (tenantsQuery.isError) {                                                                                                                                                                                                          
        return (                                                                                                                                                                                                                           
          <div className="rounded-md bg-red-50 p-4 border border-red-200 text-center">                                                                                                                                                     
            <p className="text-sm font-medium text-red-800">                                                                                                                                                                               
              Failed to load tenants: {tenantsQuery.error.message}                                                                                                                                                                         
            </p>                                                                                                                                                                                                                           
            <Button                                                                                                                                                                                                                        
              variant="outline"                                                                                                                                                                                                            
              size="sm"                                                                                                                                                                                                                    
              className="mt-2"                                                                                                                                                                                                             
              onClick={() => tenantsQuery.refetch()}                                                                                                                                                                                       
            >                                                                                                                                                                                                                              
              Try Again                                                                                                                                                                                                                    
            </Button>                                                                                                                                                                                                                      
          </div>                                                                                                                                                                                                                           
        );                                                                                                                                                                                                                                 
      }                                                                                                                                                                                                                                    
                                                                                                                                                                                                                                           
      const tenantsData = tenantsQuery.data;                                                                                                                                                                                               
      const items = tenantsData?.data.items ?? [];                                                                                                                                                                                              
                                                                                                                                                                                                                                           
      // 3. Render Table                                                                                                                                                                                                                   
      return (                                                                                                                                                                                                                             
        <div className="space-y-4">                                                                                                                                                                                                        
          <Table<Tenant>                                                                                                                                                                                                                   
            data={items}                                                                                                                                                                                                                   
            columns={[                                                                                                                                                                                                                     
              {                                                                                                                                                                                                                            
                title: 'Tenant Name',                                                                                                                                                                                                      
                field: 'name',                                                                                                                                                                                                             
                Cell({ entry }) {                                                                                                                                                                                                          
                  return (                                                                                                                                                                                                                 
                    <div className="flex items-center gap-3">                                                                                                                                                                              
                      {entry.logoUrl ? (                                                                                                                                                                                                   
                        <img                                                                                                                                                                                                               
                          src={entry.logoUrl}                                                                                                                                                                                              
                          alt={entry.name}                                                                                                                                                                                                 
                          className="size-9 rounded-full object-cover border border-gray-200"                                                                                                                                              
                        />                                                                                                                                                                                                                 
                      ) : (                                                                                                                                                                                                                
                        <div                                                                                                                                                                                                               
                          className="flex size-9 items-center justify-center rounded-full text-xs font-bold text-white shadow-sm"                                                                                                          
                          style={{                                                                                                                                                                                                         
                            backgroundColor: entry.primaryColor || '#4f46e5',                                                                                                                                                              
                          }}                                                                                                                                                                                                               
                        >                                                                                                                                                                                                                  
                          {entry.name.slice(0, 2).toUpperCase()}                                                                                                                                                                           
                        </div>                                                                                                                                                                                                             
                      )}                                                                                                                                                                                                                   
                      <div>                                                                                                                                                                                                                
                        <div className="font-semibold text-gray-900">                                                                                                                                                                      
                          {entry.name}                                                                                                                                                                                                     
                        </div>                                                                                                                                                                                                             
                        <div className="text-xs text-gray-500">                                                                                                                                                                            
                          {entry.subdomain}.alphazero.academy                                                                                                                                                                              
                        </div>                                                                                                                                                                                                             
                      </div>                                                                                                                                                                                                               
                    </div>                                                                                                                                                                                                                 
                  );                                                                                                                                                                                                                       
                },                                                                                                                                                                                                                         
              },                                                                                                                                                                                                                           
              {                                                                                                                                                                                                                            
                title: 'Subdomain',                                                                                                                                                                                                        
                field: 'subdomain',                                                                                                                                                                                                        
                Cell({ entry }) {                                                                                                                                                                                                          
                  return (                                                                                                                                                                                                                 
                    <span className="inline-flex items-center rounded-md bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10">                                                                       
                      {entry.subdomain}                                                                                                                                                                                                    
                    </span>                                                                                                                                                                                                                
                  );                                                                                                                                                                                                                       
                },                                                                                                                                                                                                                         
              },                                                                                                                                                                                                                           
              {                                                                                                                                                                                                                            
                title: 'Status',                                                                                                                                                                                                           
                field: 'status',                                                                                                                                                                                                           
                Cell({ entry }) {                                                                                                                                                                                                          
                  const status = entry.status?.toLowerCase() || 'active';                                                                                                                                                                  
                  const isStatusActive = status === 'active';                                                                                                                                                                              
                  return (                                                                                                                                                                                                                 
                    <span                                                                                                                                                                                                                  
                      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${                                                                                                                             
                        isStatusActive                                                                                                                                                                                                     
                          ? 'bg-green-100 text-green-800'                                                                                                                                                                                  
                          : 'bg-yellow-100 text-yellow-800'                                                                                                                                                                                
                      }`}                                                                                                                                                                                                                  
                    >                                                                                                                                                                                                                      
                      {entry.status || 'Active'}                                                                                                                                                                                           
                    </span>                                                                                                                                                                                                                
                  );                                                                                                                                                                                                                       
                },                                                                                                                                                                                                                         
              },                                                                                                                                                                                                                           
              {                                                                                                                                                                                                                            
                title: 'Created At',                                                                                                                                                                                                       
                field: 'createdAt',                                                                                                                                                                                                        
                Cell({ entry }) {                                                                                                                                                                                                          
                  return (                                                                                                                                                                                                                 
                    <span className="text-sm text-gray-600">                                                                                                                                                                               
                      {entry.createdAt                                                                                                                                                                                                     
                        ? new Date(entry.createdAt).toLocaleDateString(undefined, {                                                                                                                                                        
                            year: 'numeric',
                            month: 'short',
                            day: 'numeric',
                          })
                        : 'N/A'}
                    </span>
                  );
                },
              },
              {
                title: 'Actions',
                field: 'id',
                Cell({ entry }) {
                  return (
                    <div className="flex items-center gap-2">
                      <Link
                        to={`/app/tenants/${entry.id}`}
                        className="text-sm font-medium text-indigo-600 hover:text-indigo-900"
                      >
                        View
                      </Link>
                    </div>
                  );
                },
              },
            ]}
            pagination={
              tenantsData
                ? {
                    totalPages: tenantsData.data.totalPages || 1,
                    currentPage: tenantsData.data.currentPage || page,
                    rootUrl: '/tenants',
                  }
                : undefined
            }
          />
        </div>
      );
    };
