# 📝 Library Module API Audit

This file details all audited endpoints, request/response models, FluentValidation constraints, and ErrorOr error codes for the Library module.

---

## Access Codes

### 1. `POST /library/libraries/{LibraryId}/access-codes/generate`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/AccessCodes/GenerateBatch/GenerateBatchEndpoint.cs`
- **Endpoint:** `GenerateBatchEndpoint`
- **Command:** `GenerateBatchCommand(Guid LibraryId, int Quantity, string StrategyId, string TargetResourceArn, decimal FaceValue, JsonDocument Metadata)`
- **Request DTO:** `GenerateBatchRequest { Guid LibraryId, int Quantity, string StrategyId, string TargetResourceArn, decimal FaceValue, JsonDocument Metadata }`
- **Response DTO:** `GenerateBatchResponse(Guid BatchId, int GeneratedCount)`
- **Success Status:** `201 Created`
- **Authorization:** `library:GenerateCodes` on `ResourceArn.ForLibrary(tenantId, req.LibraryId)`
- **FluentValidation Rules (`GenerateBatchCommandValidator`):**
  - `LibraryId`: NotEmpty
  - `Quantity`: GreaterThan(0), LessThanOrEqualTo(1000)
  - `StrategyId`, `TargetResourceArn`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`):
    - Missing `library:GenerateCodes` permission
    - `Library.Batch.Forbidden` ("Library is not authorized to sell this resource.")
  - `404 Not Found` (`ProblemDetails`): `Library.NotFound` ("Library not found.")

---

### 2. `POST /library/admin/access-codes/generate-single`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/AccessCodes/GenerateAdminCode/GenerateAdminCodeEndpoint.cs`
- **Endpoint:** `GenerateAdminCodeEndpoint`
- **Command:** `GenerateAdminCodeCommand(string TargetResourceArn, string StrategyId, JsonDocument? Metadata)`
- **Request DTO:** `GenerateAdminCodeRequest { string TargetResourceArn, string StrategyId, JsonDocument? Metadata }`
- **Response DTO:** `GenerateAdminCodeResponse(Guid CodeId, string PlainTextCode)`
- **Success Status:** `201 Created`
- **Authorization:** `library:Audit` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`GenerateAdminCodeCommandValidator`):**
  - `TargetResourceArn`, `StrategyId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission

---

### 3. `POST /library/access-codes/batches/{BatchId}/distribute`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/AccessCodes/DistributeBatch/DistributeBatchEndpoint.cs`
- **Endpoint:** `DistributeBatchEndpoint`
- **Command:** `DistributeBatchCommand(Guid BatchId)`
- **Request DTO:** `DistributeBatchRequest { Guid BatchId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `library:SellCodes` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`DistributeBatchCommandValidator`):**
  - `BatchId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:SellCodes` permission
  - `409 Conflict` (`ProblemDetails`): `AccessCode.InvalidStatus` ("Only Minted codes can be distributed.")

---

### 4. `POST /library/access-codes/void`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/AccessCodes/VoidCode/VoidCodeEndpoint.cs`
- **Endpoint:** `VoidCodeEndpoint`
- **Command:** `VoidCodeCommand(string RawCode, string Reason)`
- **Request DTO:** `VoidCodeRequest { string RawCode, string Reason }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `library:Audit` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`VoidCodeCommandValidator`):**
  - `RawCode`: NotEmpty
  - `Reason`: NotEmpty, MaximumLength(512)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission
  - `404 Not Found` (`ProblemDetails`): `AccessCode.NotFound` ("The provided code is invalid.")
  - `409 Conflict` (`ProblemDetails`): `AccessCode.AlreadyVoided` ("Code is already voided.")

---

### 5. `POST /library/redeem`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/RedeemCode/RedeemCodeEndpoint.cs`
- **Endpoint:** `RedeemCodeEndpoint`
- **Command:** `RedeemCodeCommand(string RawCode, string? DeviceFingerprint = null, string? IpAddress = null)`
- **Request DTO:** `RedeemCodeRequest { string RawCode }`
- **Response DTO:** None (`Send.OkAsync`)
- **Success Status:** `200 OK`
- **Authorization:** `courses:Enroll` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `User.Unauthenticated` ("User must be logged in to redeem codes.")
  - `403 Forbidden` (`ProblemDetails`):
    - Missing `courses:Enroll` permission
    - `AccessCode.TenantMismatch` ("This code belongs to another academy.")
  - `404 Not Found` (`ProblemDetails`):
    - `AccessCode.NotFound` ("The provided code is invalid.")
  - `409 Conflict` (`ProblemDetails`):
    - `AccessCode.InvalidStatus` ("Code cannot be redeemed. Current status: ...")

---

### 6. `GET /library/libraries/{LibraryId}/audit-logs`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/RedemptionAuditLogs/GetRedemptionLogsEndpoint.cs`
- **Endpoint:** `GetRedemptionLogsEndpoint`
- **Query:** `GetRedemptionLogsQuery(Guid LibraryId, int Page, int PerPage)`
- **Request DTO:** `GetRedemptionLogsRequest { Guid LibraryId, int Page, int PerPage }`
- **Response DTO:** `PagedResult<RedemptionAuditLogDto>`
- **Success Status:** `200 OK`
- **Authorization:** `library:Audit` on `ResourceArn.ForLibrary(tenantId, req.LibraryId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission

---

## Libraries & Resources

### 7. `POST /library/libraries`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/Libraries/CreateLibrary/CreateLibraryEndpoint.cs`
- **Endpoint:** `CreateLibraryEndpoint`
- **Command:** `CreateLibraryCommand(string Name, string Address, string ContactNumber)`
- **Request DTO:** `CreateLibraryRequest { string Name, string Address, string ContactNumber }`
- **Response DTO:** `CreateLibraryResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `library:Audit` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`CreateLibraryCommandValidator`):**
  - `Name`: NotEmpty, MaximumLength(256)
  - `Address`: NotEmpty, MaximumLength(512)
  - `ContactNumber`: NotEmpty, MaximumLength(32)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission

---

### 8. `GET /library/libraries/{Id}`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/Libraries/GetLibrary/GetLibraryEndpoint.cs`
- **Endpoint:** `GetLibraryEndpoint`
- **Query:** `GetLibraryQuery(Guid Id)`
- **Request DTO:** `GetLibraryRequest { Guid Id }`
- **Response DTO:** `LibraryDto`
- **Success Status:** `200 OK`
- **Authorization:** `library:Audit` on `ResourceArn.ForLibrary(tenantId, req.Id)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission
  - `404 Not Found` (`ProblemDetails`): `Library.NotFound` ("Library not found.")

---

### 9. `GET /library/libraries`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/Libraries/ListLibraries/ListLibrariesEndpoint.cs`
- **Endpoint:** `ListLibrariesEndpoint`
- **Query:** `ListLibrariesQuery(int Page = 1, int PerPage = 10)`
- **Request DTO:** `ListLibrariesRequest { int Page, int PerPage }`
- **Response DTO:** `PagedResult<LibraryDto>`
- **Success Status:** `200 OK`
- **Authorization:** `library:Audit` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission

---

### 10. `PATCH /library/libraries/{Id}`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/Libraries/UpdateLibrary/UpdateLibraryEndpoint.cs`
- **Endpoint:** `UpdateLibraryEndpoint`
- **Command:** `UpdateLibraryCommand(Guid Id, string Name, string Address, string ContactNumber)`
- **Request DTO:** `UpdateLibraryRequest { Guid Id, string Name, string Address, string ContactNumber }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `library:Audit` on `ResourceArn.ForLibrary(tenantId, req.Id)`
- **FluentValidation Rules (`UpdateLibraryCommandValidator`):**
  - `Id`: NotEmpty
  - `Name`: NotEmpty, MaximumLength(256)
  - `Address`: NotEmpty, MaximumLength(512)
  - `ContactNumber`: NotEmpty, MaximumLength(32)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission
  - `404 Not Found` (`ProblemDetails`): `Library.NotFound` ("Library not found.")

---

### 11. `DELETE /library/libraries/{Id}`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/Libraries/DeleteLibrary/DeleteLibraryEndpoint.cs`
- **Endpoint:** `DeleteLibraryEndpoint`
- **Command:** `DeleteLibraryCommand(Guid Id)`
- **Request DTO:** `DeleteLibraryRequest { Guid Id }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `library:Audit` on `ResourceArn.ForLibrary(tenantId, req.Id)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:Audit` permission
  - `404 Not Found` (`ProblemDetails`): `Library.NotFound` ("Library not found.")

---

### 12. `POST /library/libraries/{Id}/resources`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/Libraries/AuthorizeResource/AuthorizeResourceEndpoint.cs`
- **Endpoint:** `AuthorizeResourceEndpoint`
- **Command:** `AuthorizeResourceCommand(Guid LibraryId, string ResourceArn)`
- **Request DTO:** `AuthorizeResourceRequest { Guid Id, string ResourceArn }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `library:AttachCourses` on `ResourceArn.ForLibrary(tenantId, req.Id)`
- **FluentValidation Rules (`AuthorizeResourceCommandValidator`):**
  - `LibraryId`, `ResourceArn`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:AttachCourses` permission
  - `404 Not Found` (`ProblemDetails`): `Library.NotFound` ("Library not found.")
  - `409 Conflict` (`ProblemDetails`): `Library.ResourceAlreadyAuthorized` ("Library is already authorized for this resource.")

---

### 13. `DELETE /library/libraries/{Id}/resources`
- **File:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/Libraries/DeauthorizeResource/DeauthorizeResourceEndpoint.cs`
- **Endpoint:** `DeauthorizeResourceEndpoint`
- **Command:** `DeauthorizeResourceCommand(Guid LibraryId, string ResourceArn)`
- **Request DTO:** `DeauthorizeResourceRequest { Guid Id, string ResourceArn }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `library:AttachCourses` on `ResourceArn.ForLibrary(tenantId, req.Id)`
- **FluentValidation Rules (`DeauthorizeResourceCommandValidator`):**
  - `LibraryId`, `ResourceArn`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `library:AttachCourses` permission
  - `404 Not Found` (`ProblemDetails`):
    - `Library.NotFound` ("Library not found.")
    - `Library.ResourceNotAuthorized` ("Library is not authorized for this resource.")
