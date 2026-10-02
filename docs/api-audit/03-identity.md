# 📝 Identity Module API Audit

This file details all audited endpoints, request/response models, FluentValidation constraints, and ErrorOr error codes for the Identity module.

---

## Authentication & Registration

### 1. `POST /identity/auth/register-student`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Auth/Commands/RegisterStudent/RegisterStudent.cs`
- **Endpoint:** `RegisterStudentEndpoint`
- **Command:** `RegisterStudentCommand(Guid TenantId, string Username, string Password, string Name)`
- **Request DTO:** `RegisterStudentRequest { Guid TenantId, string Username, string Password, string Name }`
- **Response DTO:** `RegisterStudentResponse(Guid PrincipalId)`
- **Success Status:** `201 Created`
- **Authorization:** `AllowAnonymous()`
- **FluentValidation Rules (`RegisterStudentCommandValidator`):**
  - `TenantId`: NotEmpty
  - `Username`: NotEmpty, MaximumLength(150)
  - `Password`: NotEmpty, MinimumLength(8)
  - `Name`: NotEmpty, MaximumLength(200)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `404 Not Found` (`ProblemDetails`): `ManagedPolicy.NotFound` ("StudentAccess policy not found.")
  - `409 Conflict` (`ProblemDetails`): If username already exists in Keycloak/Identity

---

### 2. `POST /identity/auth/login-principal`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Auth/Commands/LoginPrincipal/LoginPrincipal.cs`
- **Endpoint:** `LoginPrincipalEndpoint`
- **Command:** `LoginPrincipalCommand(string Username, string Password)`
- **Request DTO:** `LoginPrincipalRequest { string Username, string Password }`
- **Response DTO:** `LoginPrincipalResponse(string AccessToken, int ExpiresIn)`
- **Success Status:** `200 OK`
- **Authorization:** `AllowAnonymous()`
- **FluentValidation Rules:** None
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`):
    - `Auth.NotFoundCredentials` ("Principal not found.")
    - `Auth.InvalidCredentials` ("Invalid username or password.")

---

### 3. `POST /identity/auth/exchange-tenant-token`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Auth/Commands/LoginAsTenantUser/LoginAsTenantUser.cs`
- **Endpoint:** `LoginAsTenantUserEndpoint`
- **Command:** `LoginAsTenantUserCommand(string Subject, string Email, string? Name, string? DeviceFingerprint, string? Platform)`
- **Request DTO:** `LoginAsTenantUserRequest { string Subject, string Email, string? Name, string? DeviceFingerprint, string? Platform }`
- **Response DTO:** `LoginAsTenantUserResponse(string AccessToken, int ExpiresIn, string TokenType)`
- **Success Status:** `200 OK`
- **Authorization:** Authenticated user with claims
- **FluentValidation Rules (`LoginAsTenantUserCommandValidator`):**
  - `Platform`: IsEnumName(typeof(DevicePlatform), caseSensitive: false)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): Invalid Platform enum value
  - `401 Unauthorized` (`ProblemDetails`): Invalid or missing token claims

---

## Devices

### 4. `POST /identity/users/devices`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Users/Devices/RegisterDeviceEndpoint.cs`
- **Endpoint:** `RegisterDeviceEndpoint`
- **Command:** `RegisterDeviceCommand(string DeviceName, string PublicKey, string Platform, string? PushToken)`
- **Request DTO:** `RegisterDeviceRequest { string DeviceName, string PublicKey, string Platform, string? PushToken }`
- **Response DTO:** `RegisterDeviceResponse(Guid DeviceId, string DeviceName, bool IsMainDevice)`
- **Success Status:** `200 OK`
- **Authorization:** Authenticated user
- **FluentValidation Rules (`RegisterDeviceCommandValidator`):**
  - `DeviceName`: NotEmpty, MaximumLength(100)
  - `PublicKey`: NotEmpty
  - `Platform`: NotEmpty, IsEnumName(typeof(DevicePlatform), caseSensitive: false)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `409 Conflict` (`ProblemDetails`): `Device.Exists` ("This device is already registered.")

---

### 5. `POST /identity/users/devices/main`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Users/Devices/SetMainDeviceEndpoint.cs`
- **Endpoint:** `SetMainDeviceEndpoint`
- **Command:** `SetMainDeviceCommand(Guid DeviceId)`
- **Request DTO:** `SetMainDeviceRequest { Guid DeviceId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** Authenticated user
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `404 Not Found` (`ProblemDetails`): `Device.NotFound` ("Device not found in user's registered devices.")

---

## Policies

### 6. `POST /identity/policies/managed`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Policies/Commands/CreateManagedPolicy/CreateManagedPolicy.cs`
- **Endpoint:** `CreateManagedPolicyEndpoint`
- **Command:** `CreateManagedPolicyCommand(string Name, string? Description, string Scope, List<PolicyStatementDto> Statements)`
- **Request DTO:** `CreateManagedPolicyRequest { string Name, string? Description, string Scope, List<PolicyStatementDto> Statements }`
- **Response DTO:** `CreateManagedPolicyResponse(Guid PolicyId)`
- **Success Status:** `201 Created`
- **Authorization:** `identity:ManagePolicies` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`CreateManagedPolicyCommandValidator`):**
  - `Name`: NotEmpty, MaximumLength(100)
  - `Statements`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules, `Identity.Policy.Validation` ("Scope is required.")
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePolicies` permission

---

### 7. `DELETE /identity/policies/managed/{PolicyId}`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Policies/Commands/DeleteManagedPolicy/DeleteManagedPolicy.cs`
- **Endpoint:** `DeleteManagedPolicyEndpoint`
- **Command:** `DeleteManagedPolicyCommand(Guid PolicyId)`
- **Request DTO:** `DeleteManagedPolicyRequest { Guid PolicyId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `identity:ManagePolicies` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`DeleteManagedPolicyCommandValidator`):**
  - `PolicyId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePolicies` permission
  - `404 Not Found` (`ProblemDetails`): `ManagedPolicy.NotFound` ("Managed policy not found.")

---

## Principals & Authorization

### 8. `POST /identity/principals`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Principals/Commands/CreatePrincipal/CreatePrincipal.cs`
- **Endpoint:** `CreatePrincipalEndpoint`
- **Command:** `CreatePrincipalCommand(string Username, string Password, string Name, string PrincipalType, string? Scope)`
- **Request DTO:** `CreatePrincipalRequest { string Username, string Password, string Name, string PrincipalType, string? Scope }`
- **Response DTO:** `CreatePrincipalResponse(Guid PrincipalId)`
- **Success Status:** `201 Created`
- **Authorization:** `identity:ManagePrincipals` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`CreatePrincipalCommandValidator`):**
  - `Username`: NotEmpty
  - `Password`: NotEmpty, MinimumLength(8)
  - `Name`: NotEmpty, MaximumLength(200)
  - `PrincipalType`: NotEmpty, IsEnumName(typeof(PrincipalType), caseSensitive: false)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePrincipals` permission

---

### 9. `POST /identity/principals/{PrincipalId}/policies/inline`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Principals/Commands/AttachInlinePolicy/AttachInlinePolicy.cs`
- **Endpoint:** `AttachInlinePolicyEndpoint`
- **Command:** `AttachInlinePolicyCommand(Guid PrincipalId, string PolicyName, List<PolicyStatementDto> Statements)`
- **Request DTO:** `AttachInlinePolicyRequest { Guid PrincipalId, string PolicyName, List<PolicyStatementDto> Statements }`
- **Response DTO:** `AttachInlinePolicyResponse(Guid PolicyId)`
- **Success Status:** `200 OK`
- **Authorization:** `identity:ManagePrincipals` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`AttachInlinePolicyCommandValidator`):**
  - `PrincipalId`: NotEmpty
  - `PolicyName`: NotEmpty, MaximumLength(100)
  - `Statements`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePrincipals` permission
  - `404 Not Found` (`ProblemDetails`): `Principal.NotFound` ("Principal not found.")

---

### 10. `DELETE /identity/principals/{PrincipalId}/policies/inline/{PolicyId}`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Principals/Commands/DetachInlinePolicy/DetachInlinePolicy.cs`
- **Endpoint:** `DetachInlinePolicyEndpoint`
- **Command:** `DetachInlinePolicyCommand(Guid PrincipalId, Guid PolicyId)`
- **Request DTO:** `DetachInlinePolicyRequest { Guid PrincipalId, Guid PolicyId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `identity:ManagePrincipals` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`DetachInlinePolicyCommandValidator`):**
  - `PrincipalId`, `PolicyId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePrincipals` permission
  - `404 Not Found` (`ProblemDetails`): `Principal.NotFound` ("Principal not found.")

---

### 11. `POST /identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Principals/Commands/AttachManagedPolicy/AttachManagedPolicy.cs`
- **Endpoint:** `AttachManagedPolicyEndpoint`
- **Command:** `AttachManagedPolicyCommand(Guid PrincipalId, Guid ManagedPolicyId)`
- **Request DTO:** `AttachManagedPolicyRequest { Guid PrincipalId, Guid ManagedPolicyId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `identity:ManagePrincipals` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`AttachManagedPolicyCommandValidator`):**
  - `PrincipalId`, `ManagedPolicyId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePrincipals` permission
  - `404 Not Found` (`ProblemDetails`):
    - `Principal.NotFound` ("Principal not found.")
    - `ManagedPolicy.NotFound` ("Managed policy not found.")

---

### 12. `DELETE /identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Principals/Commands/DetachManagedPolicy/DetachManagedPolicy.cs`
- **Endpoint:** `DetachManagedPolicyEndpoint`
- **Command:** `DetachManagedPolicyCommand(Guid PrincipalId, Guid ManagedPolicyId)`
- **Request DTO:** `DetachManagedPolicyRequest { Guid PrincipalId, Guid ManagedPolicyId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `identity:ManagePrincipals` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`DetachManagedPolicyCommandValidator`):**
  - `PrincipalId`, `ManagedPolicyId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePrincipals` permission
  - `404 Not Found` (`ProblemDetails`): `Principal.NotFound`

---

### 13. `GET /identity/principals/{PrincipalId}/policies`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Principals/Queries/GetPrincipalPolicies/GetPrincipalPolicies.cs`
- **Endpoint:** `GetPrincipalPoliciesEndpoint`
- **Query:** `GetPrincipalPoliciesQuery(Guid PrincipalId)`
- **Request DTO:** `GetPrincipalPoliciesRequest { Guid PrincipalId }`
- **Response DTO:** `PrincipalPoliciesDto(List<InlinePolicyDto> InlinePolicies, List<ManagedPolicySummaryDto> ManagedPolicies)`
- **Success Status:** `200 OK`
- **Authorization:** `identity:ManagePrincipals` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePrincipals` permission
  - `404 Not Found` (`ProblemDetails`): `Principal.NotFound` ("Principal not found.")

---

### 14. `GET /identity/resources/{ResourceType}/{ResourceId}/principals`
- **File:** `src/alphazero-api/Modules/Identity/Presentation/Principals/Queries/GetPrincipalsByResource/GetPrincipalsByResource.cs`
- **Endpoint:** `GetPrincipalsByResourceEndpoint`
- **Query:** `GetPrincipalsByResourceQuery(string ResourceType, Guid ResourceId)`
- **Request DTO:** `GetPrincipalsByResourceRequest { string ResourceType, Guid ResourceId }`
- **Response DTO:** `List<PrincipalAssignmentSummaryDto>`
- **Success Status:** `200 OK`
- **Authorization:** `identity:ManagePrincipals` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `identity:ManagePrincipals` permission
