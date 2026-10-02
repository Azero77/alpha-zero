# 📝 Assessments Module API Audit

This file details all audited endpoints, request/response models, FluentValidation constraints, and ErrorOr error codes for the Assessments module.

---

### 1. `POST /assessments`
- **File:** `src/alphazero-api/Modules/Assessments/Presentation/Endpoints/Assessments/Create/CreateAssessment.cs`
- **Class:** `CreateAssessmentEndpoint`
- **Command:** `CreateAssessmentCommand`
- **Request DTO:** `CreateAssessmentRequest(string Title, string? Description, string Type, decimal PassingScore, AssessmentContent? InitialContent)`
- **Response DTO:** `CreateAssessmentResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `assessments:Create` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`CreateAssessmentCommandValidator`):**
  - `Title`: NotEmpty, MaximumLength(256)
  - `PassingScore`: GreaterThanOrEqualTo(0)
  - `Type`: NotEmpty, IsEnumName(typeof(AssessmentType), caseSensitive: false)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`):
    - FluentValidation errors (invalid Title length, negative PassingScore, invalid Type)
    - `Assessment.Title` ("Title is required.")
    - `Assessment.PassingScore` ("Passing score cannot be negative.")
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`):
    - Missing `assessments:Create` permission for tenant

---

### 2. `GET /assessments/{Id}`
- **File:** `src/alphazero-api/Modules/Assessments/Presentation/Endpoints/Assessments/Get/GetAssessmentEndpoint.cs`
- **Class:** `GetAssessmentEndpoint`
- **Query:** `GetAssessmentQuery(Guid Id, int? Version = null)`
- **Request DTO:** `GetAssessmentRequest { Guid Id, int? Version }`
- **Response DTO:** `AssessmentDetailsDto(Guid Id, string Title, string? Description, string Type, decimal PassingScore, string Status, int VersionNumber, AssessmentContent? Content)`
- **Success Status:** `200 OK`
- **Authorization:** `AllowAnonymous()`
- **FluentValidation Rules:** None
- **Error Codes & Status Mappings:**
  - `404 Not Found` (`ProblemDetails`):
    - `Assessment.NotFound` ("Assessment not found.")
    - `Assessment.VersionNotFound` ("Version {Version} not found for this assessment.")

---

### 3. `GET /assessments`
- **File:** `src/alphazero-api/Modules/Assessments/Presentation/Endpoints/Assessments/List/ListAssessmentsEndpoint.cs`
- **Class:** `ListAssessmentsEndpoint`
- **Query:** `ListAssessmentsQuery(int Page = 1, int PerPage = 10)`
- **Request DTO:** `ListAssessmentsRequest { int Page, int PerPage }`
- **Response DTO:** `PagedResult<AssessmentDto>`
- **Success Status:** `200 OK`
- **Authorization:** `AllowAnonymous()`
- **FluentValidation Rules:** None
- **Error Codes & Status Mappings:**
  - Always returns `200 OK` with paginated list (empty items if no assessments).

---

### 4. `PUT /assessments/{AssessmentId}/content`
- **File:** `src/alphazero-api/Modules/Assessments/Presentation/Endpoints/Assessments/UpdateContent/UpdateAssessmentContent.cs`
- **Class:** `UpdateAssessmentContentEndpoint`
- **Command:** `UpdateAssessmentContentCommand(Guid AssessmentId, AssessmentContent Content)`
- **Request DTO:** `UpdateAssessmentContentRequest { Guid AssessmentId, AssessmentContent Content }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `assessments:Edit` on `ResourceArn.ForAssessment(tenantId, req.AssessmentId)`
- **FluentValidation Rules (`UpdateAssessmentContentCommandValidator`):**
  - `AssessmentId`: NotEmpty
  - `Content`: NotNull
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`):
    - FluentValidation errors (`AssessmentId` empty, `Content` null)
    - Question validation errors from `IAssestmentValidator.Validate(content)`
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`):
    - Missing `assessments:Edit` permission on assessment ARN
  - `404 Not Found` (`ProblemDetails`):
    - `Assessment.NotFound` ("Assessment not found.")
  - `409 Conflict` (`ProblemDetails`):
    - `Assessment.Status` ("Cannot update content of an archived assessment.")

---

### 5. `GET /assessments/submissions`
- **File:** `src/alphazero-api/Modules/Assessments/Presentation/Endpoints/Submissions/List/ListSubmissionsEndpoint.cs`
- **Class:** `ListSubmissionsEndpoint`
- **Query:** `GetSubmissionsQuery(Guid? AssessmentId, string? Status, int Page, int PerPage)`
- **Request DTO:** `ListSubmissionsRequest { Guid? AssessmentId, string? Status, int Page, int PerPage }`
- **Response DTO:** `PagedResult<SubmissionSummaryDto>`
- **Success Status:** `200 OK`
- **Authorization:** `assessments:ViewSubmissions` on `ResourceArn.ForAssessment(...)` or `ResourceArn.ForTenant(...)`
- **FluentValidation Rules:** None
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `assessments:ViewSubmissions` permission

---

### 6. `POST /assessments/submissions/{SubmissionId}/submit`
- **File:** `src/alphazero-api/Modules/Assessments/Presentation/Endpoints/Submissions/Submit/SubmitAssessment.cs`
- **Class:** `SubmitAssessmentEndpoint`
- **Command:** `SubmitAssessmentCommand(Guid SubmissionId, AssessmentSubmissionResponses Responses)`
- **Request DTO:** `SubmitAssessmentRequest { Guid SubmissionId, AssessmentSubmissionResponses Responses }`
- **Response DTO:** `SubmitAssessmentResponse(decimal? Score, string Status)`
- **Success Status:** `200 OK`
- **Authorization:** `assessments:Submit` on `ResourceArn.ForAssessmentSubmission(tenantId, req.SubmissionId)`
- **FluentValidation Rules:** None
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`):
    - `Submission.Empty` ("Cannot submit an empty response.")
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`):
    - Missing `assessments:Submit` permission on submission ARN
  - `404 Not Found` (`ProblemDetails`):
    - `Submission.NotFound` ("Submission not found.")
    - `Assessment.NotFound` ("Assessment not found.")
  - `409 Conflict` (`ProblemDetails`):
    - `Submission.Status` ("Only in-progress submissions can be submitted.")
