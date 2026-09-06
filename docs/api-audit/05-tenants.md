# 📝 Tenants Module API Audit

This file details all audited endpoints, request/response models, FluentValidation constraints, and ErrorOr error codes for the Tenants module.

---

### 1. `POST /tenants`
- **File:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/CreateTenant/CreateTenantEndpoint.cs`
- **Endpoint:** `CreateTenantEndpoint`
- **Command:** `CreateTenantCommand(string Name, string Subdomain, string? LogoUrl, string? PrimaryColor, string? SecondaryColor)`
- **Request DTO:** `CreateTenantRequest { string Name, string Subdomain, string? LogoUrl, string? PrimaryColor, string? SecondaryColor }`
- **Response DTO:** `CreateTenantResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `tenants:Manage` on `ResourceArn.ForRoot()`
- **FluentValidation Rules (`CreateTenantCommandValidator`):**
  - `Name`: NotEmpty, MaximumLength(256)
  - `Subdomain`: NotEmpty, MaximumLength(64), Matches `^[a-z0-9-]+$`
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `tenants:Manage` permission
  - `409 Conflict` (`ProblemDetails`): `Tenant.SubdomainNotUnique` ("The subdomain '{Subdomain}' is already in use.")

---

### 2. `GET /tenants/{Id}`
- **File:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/GetTenant/GetTenantEndpoint.cs`
- **Endpoint:** `GetTenantEndpoint`
- **Query:** `GetTenantQuery(Guid Id)`
- **Request DTO:** `GetTenantRequest { Guid Id }`
- **Response DTO:** `TenantDto`
- **Success Status:** `200 OK`
- **Authorization:** `tenants:Manage` on `ResourceArn.ForTenant(req.Id)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `tenants:Manage` permission
  - `404 Not Found` (`ProblemDetails`): `Tenant.NotFound` ("Tenant not found.")

---

### 3. `GET /tenants`
- **File:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/ListTenants/ListTenantsEndpoint.cs`
- **Endpoint:** `ListTenantsEndpoint`
- **Query:** `ListTenantsQuery(int Page = 1, int PerPage = 10)`
- **Request DTO:** `ListTenantsRequest { int Page, int PerPage }`
- **Response DTO:** `PagedResult<TenantDto>`
- **Success Status:** `200 OK`
- **Authorization:** `tenants:Manage` on `ResourceArn.ForRoot()`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `tenants:Manage` permission

---

### 4. `GET /tenants/lookup`
- **File:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/LookupTenant/LookupTenantEndpoint.cs`
- **Endpoint:** `LookupTenantEndpoint`
- **Query:** `GetTenantBySubdomainQuery(string Subdomain)`
- **Request DTO:** `LookupTenantRequest { string Subdomain }`
- **Response DTO:** `TenantDto`
- **Success Status:** `200 OK`
- **Authorization:** `AllowAnonymous()`
- **Error Codes & Status Mappings:**
  - `404 Not Found` (`ProblemDetails`): `Tenant.NotFound` ("No academy found for subdomain '{Subdomain}'.")

---

### 5. `PUT /tenants/{Id}`
- **File:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/UpdateTenant/UpdateTenantEndpoint.cs`
- **Endpoint:** `UpdateTenantEndpoint`
- **Command:** `UpdateTenantCommand(Guid Id, string Name, string? LogoUrl, string? PrimaryColor, string? SecondaryColor)`
- **Request DTO:** `UpdateTenantRequest { Guid Id, string Name, string? LogoUrl, string? PrimaryColor, string? SecondaryColor }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `tenants:Manage` on `ResourceArn.ForTenant(req.Id)`
- **FluentValidation Rules (`UpdateTenantCommandValidator`):**
  - `Id`: NotEmpty
  - `Name`: NotEmpty, MaximumLength(256)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `tenants:Manage` permission
  - `404 Not Found` (`ProblemDetails`): `Tenant.NotFound` ("Tenant not found.")

---

### 6. `DELETE /tenants/{Id}`
- **File:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/DeleteTenant/DeleteTenantEndpoint.cs`
- **Endpoint:** `DeleteTenantEndpoint`
- **Command:** `DeleteTenantCommand(Guid Id)`
- **Request DTO:** `DeleteTenantRequest { Guid Id }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `tenants:Manage` on `ResourceArn.ForTenant(req.Id)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `tenants:Manage` permission
  - `404 Not Found` (`ProblemDetails`): `Tenant.NotFound` ("Tenant not found.")
