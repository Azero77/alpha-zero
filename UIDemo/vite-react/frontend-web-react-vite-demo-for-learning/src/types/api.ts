  /**                                                                                                                                                                                                                                    
     * Base entity representing the minimum fields guaranteed by the backend                                                                                                                                                               
     * for persistent database records across all domains (Tenants, Courses, Users, etc.).                                                                                                                                                 
     */                                                                                                                                                                                                                                    
    export type BaseEntity = {                                                                                                                                                                                                             
      id: string;                                                                                                                                                                                                                          
      createdAt: number | string;                                                                                                                                                                                                          
    };                                                                                                                                                                                                                                     
                                                                                                                                                                                                                                           
    /**                                                                                                                                                                                                                                    
     * Utility type to turn any domain shape into a full database entity                                                                                                                                                                   
     * by attaching BaseEntity fields (id, createdAt).                                                                                                                                                                                     
     */                                                                                                                                                                                                                                    
    export type Entity<T> = {                                                                                                                                                                                                              
      [K in keyof T]: T[K];                                                                                                                                                                                                                
    } & BaseEntity;                                                                                                                                                                                                                        
                                                                                                                                                                                                                                           
    /**                                                                                                                                                                                                                                    
     * Standard single-item API envelope response.
     */
    export type ApiResponse<T> = {
      data: T;
      message?: string;
      success?: boolean;
    };
  
    /**
     * Generic paginated response contract for list endpoints across modules.
     */
    export type PaginatedResponse<T> = {
      items: T[];
      totalCount: number;
      currentPage: number;
      pageSize: number;
      totalPages: number;
      hasNextPage: boolean;
      hasPreviousPage: boolean;
    };
  
    /**
     * Standard query parameters for paginated and searchable list queries.
     */
    export type PaginationQueryParams = {
      page?: number;
      perPage?: number;
      q?: string;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
    };
