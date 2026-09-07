# 📋 LMS Zod Schemas Implementation Status Tracker

> **Location:** `src/alphazero-frontend/packages/lms-types/src/schemas/{modulename}/` & `lms-types/schemas/{modulename}/`  
> **Source Backend:** `src/alphazero-api/Modules/{moduleName}/`  
> **TypeScript Definitions:** `src/alphazero-frontend/packages/lms-types/src/api.d.ts`  
> **Last Updated:** 2026-09-07  

---

## 📊 Executive Summary

| Module | Endpoints / Requests | FluentValidation Rules | Target Schema Directory | Status |
| :--- | :---: | :---: | :--- | :---: |
| **Assessments** | 6 | 2 Command Validators + 1 Service Validator | `schemas/assessments/` | 🟢 `[x] 6/6 Written` |
| **Courses** | 24 | 9 Command Validators | `schemas/courses/` | 🟢 `[x] 24/24 Written` |
| **Identity** | 14 | 11 Command Validators | `schemas/identity/` | 🟢 `[x] 14/14 Written` |
| **Library** | 13 | 8 Command Validators | `schemas/library/` | 🟢 `[x] 13/13 Written` |
| **Tenants** | 6 | 2 Command Validators | `schemas/tenants/` | 🟢 `[x] 6/6 Written` |
| **VideoUploading** | 5 | 1 Command Validator | `schemas/video-uploading/` | 🟢 `[x] 5/5 Written` |
| **TOTAL** | **68 Endpoints/DTOs** | **33 Validators** | | **100% Completed** |

---

## 1. 📝 Assessments Module
**Target Path:** `src/alphazero-frontend/packages/lms-types/src/schemas/assessments/`  
**Backend Application:** `src/alphazero-api/Modules/Assessments/Application/`  
**Backend Presentation:** `src/alphazero-api/Modules/Assessments/Presentation/Endpoints/`  

- [x] **Module Schemas Written: `6/6` (100%)**

| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Create Assessment** | `POST /assessments` | `CreateAssessmentRequest`<br>`CreateAssessmentCommand` | • `Title`: NotEmpty, MaxLength(256)<br>• `PassingScore`: GreaterThanOrEqualTo(0)<br>• `Type`: NotEmpty, IsEnumName(`MCQ`, `Handwritten`, `Hybrid`) | `create-assessment.schema.ts`<br>`CreateAssessmentRequestSchema`<br>`CreateAssessmentResponseSchema` | `[x] Written` |
| **Get Assessment** | `GET /assessments/{Id}` | `GetAssessmentRequest`<br>`GetAssessmentQuery` | None (Route ID Guid, Query `int? Version`) | `get-assessment.schema.ts`<br>`GetAssessmentRequestSchema`<br>`AssessmentDetailsDtoSchema` | `[x] Written` |
| **List Assessments** | `GET /assessments` | `ListAssessmentsRequest`<br>`ListAssessmentsQuery` | None (Query `int Page = 1`, `int PerPage = 10`) | `list-assessments.schema.ts`<br>`ListAssessmentsRequestSchema`<br>`PagedAssessmentsResponseSchema` | `[x] Written` |
| **Update Assessment Content** | `PUT /assessments/{AssessmentId}/content` | `UpdateAssessmentContentRequest`<br>`UpdateAssessmentContentCommand` | • `AssessmentId`: NotEmpty (Guid)<br>• `Content`: NotNull (`AssessmentContent`)<br>• Domain: Validated by `IAssestmentValidator` based on type | `update-assessment-content.schema.ts`<br>`UpdateAssessmentContentRequestSchema` | `[x] Written` |
| **List Submissions** | `GET /assessments/submissions` | `ListSubmissionsRequest`<br>`GetSubmissionsQuery` | None (Query `Guid? AssessmentId`, `string? Status`, `int Page = 1`, `int PerPage = 10`) | `list-submissions.schema.ts`<br>`ListSubmissionsRequestSchema`<br>`PagedSubmissionsResponseSchema` | `[x] Written` |
| **Submit Assessment** | `POST /assessments/submissions/{SubmissionId}/submit` | `SubmitAssessmentRequest`<br>`SubmitAssessmentCommand` | Domain: Not empty answers, submission in `InProgress` state | `submit-assessment.schema.ts`<br>`SubmitAssessmentRequestSchema`<br>`SubmitAssessmentResponseSchema` | `[x] Written` |

---

## 2. 🎓 Courses Module
**Target Path:** `src/alphazero-frontend/packages/lms-types/src/schemas/courses/`  
**Backend Application:** `src/alphazero-api/Modules/Courses/Application/`  
**Backend Presentation:** `src/alphazero-api/Modules/Courses/Presentation/`  

- [x] **Module Schemas Written: `24/24` (100%)**

### Courses & Curriculum
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Create Course** | `POST /courses` | `CreateCourseRequest`<br>`CreateCourseCommand` | • `Title`: NotEmpty, MaxLength(200)<br>• `Description`: NotEmpty, MaxLength(2000)<br>• `SubjectId`: NotEmpty<br>• `Price`: GreaterThanOrEqualTo(0)<br>• `Currency`: NotEmpty, Length(3)<br>• `Difficulty`: IsInEnum (`Beginner`, `Intermediate`, `Advanced`) | `create-course.schema.ts`<br>`CreateCourseRequestSchema`<br>`CreateCourseResponseSchema` | `[x] Written` |
| **Get Course** | `GET /courses/{Id}` | `GetCourseRequest`<br>`GetCourseQuery` | None (Route ID Guid) | `get-course.schema.ts`<br>`GetCourseRequestSchema`<br>`CourseDetailsResponseSchema` | `[x] Written` |
| **List Courses** | `GET /courses` | `ListCoursesRequest`<br>`ListCoursesQuery` | Query: `Guid? SubjectId`, `CourseStatus? Status`, `int Page = 1`, `int PerPage = 10` | `list-courses.schema.ts`<br>`ListCoursesRequestSchema`<br>`PagedCoursesResponseSchema` | `[x] Written` |
| **Add Section** | `POST /courses/{CourseId}/sections` | `AddSectionRequest`<br>`AddSectionCommand` | • `CourseId`: NotEmpty<br>• `Title`: NotEmpty, MaxLength(200) | `curriculum.schema.ts`<br>`AddSectionRequestSchema`<br>`AddSectionResponseSchema` | `[x] Written` |
| **Add Lesson** | `POST /courses/{CourseId}/sections/{SectionId}/lessons` | `AddLessonRequest`<br>`AddLessonCommand` | • `CourseId`: NotEmpty<br>• `SectionId`: NotEmpty<br>• `Title`: NotEmpty, MaxLength(200)<br>• `VideoId`: NotEmpty<br>• `Duration`: GreaterThan(TimeSpan.Zero) | `curriculum.schema.ts`<br>`AddLessonRequestSchema`<br>`AddLessonResponseSchema` | `[x] Written` |
| **Add Quiz** | `POST /courses/{CourseId}/sections/{SectionId}/quizzes` | `AddQuizRequest`<br>`AddAssessmentCommand` | • `CourseId`: NotEmpty<br>• `SectionId`: NotEmpty<br>• `AssessmentId`: NotEmpty<br>• `Title`: NotEmpty, MaxLength(200) | `curriculum.schema.ts`<br>`AddQuizRequestSchema`<br>`AddQuizResponseSchema` | `[x] Written` |
| **Reorder Sections** | `POST /courses/{CourseId}/sections/reorder` | `ReorderSectionsRequest`<br>`ReorderSectionsCommand` | • `SectionIds`: Array of Guid | `curriculum.schema.ts`<br>`ReorderSectionsRequestSchema` | `[x] Written` |
| **Reorder Items** | `POST /courses/{CourseId}/sections/{SectionId}/reorder` | `ReorderItemsRequest`<br>`ReorderItemsCommand` | • `ItemIds`: Array of Guid | `curriculum.schema.ts`<br>`ReorderItemsRequestSchema` | `[x] Written` |

### Course Lifecycle / State
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Submit for Review** | `PATCH /courses/{CourseId}/review` | `SubmitForReviewRequest`<br>`SubmitCourseForReviewCommand` | Route: `CourseId` (Guid) | `lifecycle.schema.ts`<br>`SubmitForReviewRequestSchema` | `[x] Written` |
| **Approve Course** | `PATCH /courses/{CourseId}/approve` | `ApproveCourseRequest`<br>`ApproveCourseCommand` | Route: `CourseId` (Guid) | `lifecycle.schema.ts`<br>`ApproveCourseRequestSchema` | `[x] Written` |
| **Reject Course** | `PATCH /courses/{CourseId}/reject` | `RejectCourseRequest`<br>`RejectCourseCommand` | Body: `Reason` (string) | `lifecycle.schema.ts`<br>`RejectCourseRequestSchema` | `[x] Written` |
| **Publish Course** | `PATCH /courses/{CourseId}/publish` | `PublishCourseRequest`<br>`PublishCourseCommand` | Route: `CourseId` (Guid) | `lifecycle.schema.ts`<br>`PublishCourseRequestSchema` | `[x] Written` |

### Plans
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Add Plan** | `POST /courses/{CourseId}/plans` | `AddPlanRequest`<br>`AddPlanCommand` | • `CourseId`: NotEmpty<br>• `Name`: NotEmpty, MaxLength(100)<br>• `Price`: GreaterThanOrEqualTo(0)<br>• `Currency`: NotEmpty, Length(3)<br>• `Type`: IsInEnum (`OneTime`, `Subscription`, `CustomTimeLimit`)<br>• `DurationDays`: GreaterThan(0) when Subscription/CustomTimeLimit<br>• `LibraryCodeMaxUsage`: GreaterThan(0) when specified | `plan.schema.ts`<br>`AddPlanRequestSchema`<br>`AddPlanResponseSchema` | `[x] Written` |
| **Update Plan** | `PUT /courses/{CourseId}/plans/{PlanId}` | `UpdatePlanRequest`<br>`UpdatePlanCommand` | • `CourseId`: NotEmpty, `PlanId`: NotEmpty<br>• `Name`: NotEmpty, MaxLength(100)<br>• `Price`: GreaterThanOrEqualTo(0)<br>• `Currency`: NotEmpty, Length(3)<br>• `Type`: IsInEnum<br>• `DurationDays`: GreaterThan(0) when Subscription/CustomTimeLimit<br>• `LibraryCodeMaxUsage`: GreaterThan(0) when specified | `plan.schema.ts`<br>`UpdatePlanRequestSchema` | `[x] Written` |
| **Remove Plan** | `DELETE /courses/{CourseId}/plans/{PlanId}` | `RemovePlanRequest`<br>`RemovePlanCommand` | • `CourseId`: NotEmpty<br>• `PlanId`: NotEmpty | `plan.schema.ts`<br>`RemovePlanRequestSchema` | `[x] Written` |

### Enrollments & Progress
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Enroll In Course** | `POST /courses/enroll` | `EnrollInCourseRequest`<br>`EnrollInCourseCommand` | • `CourseId`: NotEmpty<br>• `PlanId`: NotEmpty<br>• `LibraryCode`: Optional string | `enrollment.schema.ts`<br>`EnrollInCourseRequestSchema`<br>`EnrollInCourseResponseSchema` | `[x] Written` |
| **Get Enrollment** | `GET /courses/enrollments/{Id}` | `GetEnrollementRequest`<br>`GetEnrollmentQuery` | Route: `Id` (Guid) | `enrollment.schema.ts`<br>`GetEnrollmentRequestSchema`<br>`EnrollmentDtoSchema` | `[x] Written` |
| **Complete Item** | `POST /courses/enrollements/{EnrollmentId}/complete` | `CompleteItemRequest`<br>`CompleteItemCommand` | Route: `EnrollmentId`, Body: `bitIndex` (integer >= 0) | `enrollment.schema.ts`<br>`CompleteItemRequestSchema` | `[x] Written` |
| **Get Student Dashboard** | `GET /courses/dashboard/{StudentId}` | `GetStudentDashboardRequest`<br>`GetStudentDashboardQuery` | Route: `StudentId` (Guid) | `analytics.schema.ts`<br>`GetStudentDashboardParamsSchema`<br>`StudentDashboardDtoSchema` | `[x] Written` |

### Subjects & Analytics
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Create Subject** | `POST /courses/subjects` | `CreateSubjectRequest`<br>`CreateSubjectCommand` | • `Name`: NotEmpty, MaxLength(100)<br>• `Description`: MaxLength(500) | `subject.schema.ts`<br>`CreateSubjectRequestSchema`<br>`CreateSubjectResponseSchema` | `[x] Written` |
| **Get Subject** | `GET /courses/subjects/{id}` | `GetSubjectRequest`<br>`GetSubjectQuery` | Route: `id` (Guid) | `subject.schema.ts`<br>`GetSubjectRequestSchema`<br>`SubjectDtoSchema` | `[x] Written` |
| **List Subjects** | `GET /courses/subjects` | `ListSubjectsRequest`<br>`ListSubjectsQuery` | Query: `int Page = 1`, `int PerPage = 50` | `subject.schema.ts`<br>`ListSubjectsRequestSchema`<br>`PagedSubjectsResponseSchema` | `[x] Written` |
| **Get Course Analytics** | `GET /courses/{CourseId}/analytics` | `GetCourseAnalyticsRequest`<br>`GetCourseAnalyticsQuery` | Route: `CourseId` (Guid) | `analytics.schema.ts`<br>`GetCourseAnalyticsParamsSchema`<br>`CourseAnalyticsDtoSchema` | `[x] Written` |
| **List Student Progress** | `GET /courses/{CourseId}/progress` | `ListStudentProgressRequest`<br>`ListStudentProgressQuery` | Route: `CourseId`, Query: `int Page = 1`, `int PerPage = 20` | `analytics.schema.ts`<br>`ListStudentProgressParamsSchema`<br>`PagedStudentProgressResponseSchema` | `[x] Written` |

---

## 3. 🔐 Identity Module
**Target Path:** `src/alphazero-frontend/packages/lms-types/src/schemas/identity/`  
**Backend Application:** `src/alphazero-api/Modules/Identity/Application/`  
**Backend Presentation:** `src/alphazero-api/Modules/Identity/Presentation/`  

- [x] **Module Schemas Written: `14/14` (100%)**

### Auth & Registration
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Register Student** | `POST /identity/auth/register-student` | `RegisterStudentRequest`<br>`RegisterStudentCommand` | • `PhoneNumber`: NotEmpty, Regex (phone format)<br>• `Password`: NotEmpty, MinLength(8)<br>• `FullName`: NotEmpty, MaxLength(100)<br>• `DeviceFingerprint`: NotNull, `DeviceId` NotEmpty | `auth.schema.ts`<br>`RegisterStudentRequestSchema`<br>`AuthResultResponseSchema` | `[x] Written` |
| **Login Principal** | `POST /identity/auth/login-principal` | `LoginPrincipalRequest`<br>`LoginPrincipalCommand` | Body: `Email` (EmailAddress), `Password` (string) | `auth.schema.ts`<br>`LoginPrincipalRequestSchema`<br>`PrincipalAuthResultResponseSchema` | `[x] Written` |
| **Exchange Tenant Token** | `POST /identity/auth/exchange-tenant-token` | `LoginAsTenantUserRequest`<br>`LoginAsTenantUserCommand` | • `TenantId`: NotEmpty (Guid)<br>• `DeviceFingerprint`: Optional DTO | `auth.schema.ts`<br>`LoginAsTenantUserRequestSchema`<br>`TenantAuthResultResponseSchema` | `[x] Written` |

### Devices
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Register Device** | `POST /identity/users/devices` | `RegisterDeviceRequest`<br>`RegisterDeviceCommand` | • `DeviceIdentifier`: NotEmpty<br>• `DeviceName`: NotEmpty, MaxLength(100)<br>• `DeviceType`: NotEmpty | `device.schema.ts`<br>`RegisterDeviceRequestSchema`<br>`RegisterDeviceResponseSchema` | `[x] Written` |
| **Set Main Device** | `POST /identity/users/devices/main` | `SetMainDeviceRequest`<br>`SetMainDeviceCommand` | Body: `DeviceId` (Guid NotEmpty) | `device.schema.ts`<br>`SetMainDeviceRequestSchema` | `[x] Written` |

### Policies
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Create Managed Policy** | `POST /identity/policies/managed` | `CreateManagedPolicyRequest`<br>`CreateManagedPolicyCommand` | • `Name`: NotEmpty, MaxLength(100)<br>• `Statements`: NotEmpty list | `policy.schema.ts`<br>`CreateManagedPolicyRequestSchema`<br>`CreatePolicyResponseSchema` | `[x] Written` |
| **Delete Managed Policy** | `DELETE /identity/policies/managed/{PolicyId}` | `DeleteManagedPolicyRequest`<br>`DeleteManagedPolicyCommand` | Route: `PolicyId` (NotEmpty Guid) | `policy.schema.ts`<br>`DeleteManagedPolicyParamsSchema` | `[x] Written` |

### Principals & IAM
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Create Principal** | `POST /identity/principals` | `CreatePrincipalRequest`<br>`CreatePrincipalCommand` | • `UserId`: NotEmpty<br>• `Type`: NotEmpty, IsInEnum (`User`, `Role`, `Group`) | `principal.schema.ts`<br>`CreatePrincipalRequestSchema`<br>`CreatePrincipalResponseSchema` | `[x] Written` |
| **Attach Inline Policy** | `POST /identity/principals/{PrincipalId}/policies/inline` | `AttachInlinePolicyRequest`<br>`AttachInlinePolicyCommand` | • `PrincipalId`: NotEmpty<br>• `Name`: NotEmpty, MaxLength(100)<br>• `Statements`: NotEmpty list | `principal.schema.ts`<br>`AttachInlinePolicyRequestSchema`<br>`AttachPolicyResponseSchema` | `[x] Written` |
| **Detach Inline Policy** | `DELETE /identity/principals/{PrincipalId}/policies/inline/{PolicyId}` | `DetachInlinePolicyRequest`<br>`DetachInlinePolicyCommand` | • `PrincipalId`: NotEmpty<br>• `PolicyId`: NotEmpty | `principal.schema.ts`<br>`DetachInlinePolicyParamsSchema` | `[x] Written` |
| **Attach Managed Policy** | `POST /identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}` | `AttachManagedPolicyRequest`<br>`AttachManagedPolicyCommand` | • `PrincipalId`: NotEmpty<br>• `ManagedPolicyId`: NotEmpty | `principal.schema.ts`<br>`AttachManagedPolicyParamsSchema` | `[x] Written` |
| **Detach Managed Policy** | `DELETE /identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}` | `DetachManagedPolicyRequest`<br>`DetachManagedPolicyCommand` | • `PrincipalId`: NotEmpty<br>• `ManagedPolicyId`: NotEmpty | `principal.schema.ts`<br>`DetachManagedPolicyParamsSchema` | `[x] Written` |
| **Get Principal Policies** | `GET /identity/principals/{PrincipalId}/policies` | `GetPrincipalPoliciesRequest`<br>`GetPrincipalPoliciesQuery` | Route: `PrincipalId` (Guid) | `principal.schema.ts`<br>`GetPrincipalPoliciesParamsSchema` | `[x] Written` |
| **Get Principals By Resource** | `GET /identity/resources/{ResourceType}/{ResourceId}/principals` | `GetPrincipalsByResourceRequest`<br>`GetPrincipalsByResourceQuery` | Route: `ResourceType` (string), `ResourceId` (string) | `principal.schema.ts`<br>`GetPrincipalsByResourceParamsSchema` | `[x] Written` |

---

## 4. 📚 Library Module
**Target Path:** `src/alphazero-frontend/packages/lms-types/src/schemas/library/`  
**Backend Application:** `src/alphazero-api/Modules/Library/Application/`  
**Backend Presentation:** `src/alphazero-api/Modules/Library/Presentation/Endpoints/`  

- [x] **Module Schemas Written: `13/13` (100%)**

### Access Codes
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Generate Batch** | `POST /library/libraries/{LibraryId}/access-codes/generate` | `GenerateBatchRequest`<br>`GenerateBatchCommand` | • `LibraryId`: NotEmpty<br>• `Quantity`: GreaterThan(0), LessThanOrEqualTo(1000)<br>• `ResourceId`: NotEmpty<br>• `ResourceType`: NotEmpty<br>• `UnitPrice`: GreaterThanOrEqualTo(0)<br>• `RevenueShareRate`: InclusiveBetween(0, 1) | `access-codes.schema.ts`<br>`GenerateBatchRequestSchema`<br>`GenerateBatchResponseSchema` | `[x] Written` |
| **Generate Admin Code** | `POST /library/admin/access-codes/generate-single` | `GenerateAdminCodeRequest`<br>`GenerateAdminCodeCommand` | • `ResourceId`: NotEmpty<br>• `ResourceType`: NotEmpty<br>• `Note`: MaxLength(500) when present | `access-codes.schema.ts`<br>`GenerateAdminCodeRequestSchema`<br>`GenerateAdminCodeResponseSchema` | `[x] Written` |
| **Distribute Batch** | `POST /library/access-codes/batches/{BatchId}/distribute` | `DistributeBatchRequest`<br>`DistributeBatchCommand` | Route: `BatchId` (NotEmpty Guid) | `access-codes.schema.ts`<br>`DistributeBatchParamsSchema` | `[x] Written` |
| **Void Code** | `POST /library/access-codes/void` | `VoidCodeRequest`<br>`VoidCodeCommand` | • `Code`: NotEmpty<br>• `Reason`: NotEmpty, MaxLength(500) | `access-codes.schema.ts`<br>`VoidCodeRequestSchema` | `[x] Written` |
| **Redeem Code** | `POST /library/redeem` | `RedeemCodeRequest`<br>`RedeemCodeCommand` | • `Code`: NotEmpty string | `access-codes.schema.ts`<br>`RedeemCodeRequestSchema`<br>`RedeemCodeResponseSchema` | `[x] Written` |
| **Get Redemption Logs** | `GET /library/libraries/{LibraryId}/audit-logs` | `GetRedemptionLogsRequest`<br>`GetRedemptionLogsQuery` | Route: `LibraryId`, Query: `int Page = 1`, `int PerPage = 20` | `access-codes.schema.ts`<br>`GetRedemptionLogsParamsSchema`<br>`PagedRedemptionLogsResponseSchema` | `[x] Written` |

### Libraries & Resources
| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Create Library** | `POST /library/libraries` | `CreateLibraryRequest`<br>`CreateLibraryCommand` | • `Name`: NotEmpty, MaxLength(200)<br>• `PhysicalAddress`: NotEmpty, MaxLength(500)<br>• `ContactPhone`: NotEmpty, MaxLength(30)<br>• `DefaultRevenueShareRate`: InclusiveBetween(0, 1) | `library.schema.ts`<br>`CreateLibraryRequestSchema`<br>`CreateLibraryResponseSchema` | `[x] Written` |
| **Get Library** | `GET /library/libraries/{Id}` | `GetLibraryRequest`<br>`GetLibraryQuery` | Route: `Id` (Guid) | `library.schema.ts`<br>`GetLibraryParamsSchema`<br>`LibraryDtoSchema` | `[x] Written` |
| **List Libraries** | `GET /library/libraries` | `ListLibrariesRequest`<br>`ListLibrariesQuery` | Query: `int Page = 1`, `int PerPage = 20` | `library.schema.ts`<br>`ListLibrariesQuerySchema`<br>`PagedLibrariesResponseSchema` | `[x] Written` |
| **Update Library** | `PATCH /library/libraries/{Id}` | `UpdateLibraryRequest`<br>`UpdateLibraryCommand` | • `Id`: NotEmpty<br>• `Name`: NotEmpty, MaxLength(200)<br>• `PhysicalAddress`: NotEmpty, MaxLength(500)<br>• `ContactPhone`: NotEmpty, MaxLength(30)<br>• `DefaultRevenueShareRate`: InclusiveBetween(0, 1) | `library.schema.ts`<br>`UpdateLibraryRequestSchema` | `[x] Written` |
| **Delete Library** | `DELETE /library/libraries/{Id}` | `DeleteLibraryRequest`<br>`DeleteLibraryCommand` | Route: `Id` (Guid) | `library.schema.ts`<br>`DeleteLibraryParamsSchema` | `[x] Written` |
| **Authorize Resource** | `POST /library/libraries/{Id}/resources` | `AuthorizeResourceRequest`<br>`AuthorizeResourceCommand` | • `LibraryId`: NotEmpty<br>• `ResourceId`: NotEmpty<br>• `ResourceType`: NotEmpty | `library-resource.schema.ts`<br>`AuthorizeResourceRequestSchema` | `[x] Written` |
| **Deauthorize Resource** | `DELETE /library/libraries/{Id}/resources` | `DeauthorizeResourceRequest`<br>`DeauthorizeResourceCommand` | • `LibraryId`: NotEmpty<br>• `ResourceId`: NotEmpty<br>• `ResourceType`: NotEmpty | `library-resource.schema.ts`<br>`DeauthorizeResourceRequestSchema` | `[x] Written` |

---

## 5. 🏢 Tenants Module
**Target Path:** `src/alphazero-frontend/packages/lms-types/src/schemas/tenants/`  
**Backend Application:** `src/alphazero-api/Modules/Tenants/Application/`  
**Backend Presentation:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/`  

- [x] **Module Schemas Written: `6/6` (100%)**

| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Create Tenant** | `POST /tenants` | `CreateTenantRequest`<br>`CreateTenantCommand` | • `Identifier`: NotEmpty, Regex(`^[a-z0-9-]+$`), MaxLength(50)<br>• `Name`: NotEmpty, MaxLength(100)<br>• `Domain`: NotEmpty, MaxLength(100)<br>• `AdminEmail`: NotEmpty, EmailAddress<br>• `AdminPassword`: NotEmpty, MinLength(8)<br>• `AdminFullName`: NotEmpty, MaxLength(100)<br>• `AdminPhone`: NotEmpty | `create-tenant.schema.ts`<br>`CreateTenantRequestSchema`<br>`CreateTenantResponseSchema` | `[x] Written` |
| **Get Tenant** | `GET /tenants/{Id}` | `GetTenantRequest`<br>`GetTenantQuery` | Route: `Id` (Guid) | `tenant.schema.ts`<br>`GetTenantParamsSchema`<br>`TenantDtoSchema` | `[x] Written` |
| **List Tenants** | `GET /tenants` | `ListTenantsRequest`<br>`ListTenantsQuery` | Query: `int Page = 1`, `int PerPage = 10` | `tenant.schema.ts`<br>`ListTenantsQuerySchema`<br>`PagedTenantsResponseSchema` | `[x] Written` |
| **Lookup Tenant** | `GET /tenants/lookup` | `LookupTenantRequest`<br>`LookupTenantQuery` | Query: `string? Identifier`, `string? Domain` | `tenant.schema.ts`<br>`LookupTenantQuerySchema`<br>`TenantLookupDtoSchema` | `[x] Written` |
| **Update Tenant** | `PUT /tenants/{Id}` | `UpdateTenantRequest`<br>`UpdateTenantCommand` | • `Id`: NotEmpty<br>• `Name`: NotEmpty, MaxLength(100)<br>• `Domain`: NotEmpty, MaxLength(100)<br>• `Status`: IsInEnum (`Active`, `Suspended`, etc.) | `tenant.schema.ts`<br>`UpdateTenantRequestSchema` | `[x] Written` |
| **Delete Tenant** | `DELETE /tenants/{Id}` | `DeleteTenantRequest`<br>`DeleteTenantCommand` | Route: `Id` (Guid) | `tenant.schema.ts`<br>`DeleteTenantParamsSchema` | `[x] Written` |

---

## 6. 🎥 Video Uploading Module
**Target Path:** `src/alphazero-frontend/packages/lms-types/src/schemas/video-uploading/`  
**Backend Application:** `src/alphazero-api/Modules/VideoUploading/Application/`  
**Backend Presentation:** `src/alphazero-api/Modules/VideoUploading/Presentation/Features/`  

- [x] **Module Schemas Written: `5/5` (100%)**

| Endpoint / Operation | HTTP & Route | Request DTO & Command | FluentValidation Rules | Target Schema File & Exports | Status |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Request Video Upload** | `POST api/video-uploading/upload` | `UploadRequest`<br>`UploadCommand` | • `FileName`: NotEmpty, MaxLength(255)<br>• `ContentType`: NotEmpty, Valid video MIME type (`video/mp4`, `video/webm`, etc.)<br>• `FileSizeBytes`: GreaterThan(0) | `upload.schema.ts`<br>`UploadRequestSchema`<br>`UploadResponseSchema` | `[x] Written` |
| **Get Streaming Info** | `GET api/video/{videoId:guid}` | `GetStreamingInfoRequest`<br>`GetStreamingInfoQuery` | Route: `VideoId` (Guid) | `streaming.schema.ts`<br>`GetStreamingInfoParamsSchema`<br>`StreamingInfoDtoSchema` | `[x] Written` |
| **Get Video DRM Key** | `GET api/video/keys/{VideoId:guid}` | `GetVideoKeyRequest`<br>`GetVideoKeyQuery` | Route: `VideoId` (Guid) | `streaming.schema.ts`<br>`GetVideoKeyParamsSchema` | `[x] Written` |
| **Update Video Info** | `PATCH api/video-uploading/debug/videos/{id:guid}` | `UpdateVideoInfoRequest`<br>`UpdateVideoInfoCommand` | Route: `id` (Guid), Body: Optional `Title`, `Description` | `streaming.schema.ts`<br>`UpdateVideoInfoRequestSchema` | `[x] Written` |
| **List Debug Videos** | `GET api/video-uploading/debug/videos` | None / Debug | None | `streaming.schema.ts`<br>`DebugVideoDtoSchema` | `[x] Written` |

---

## 🚀 Exports Architecture
All schemas are available via two import paths:
1. Root package import:
   ```typescript
   import { CreateCourseRequestSchema, RegisterStudentRequestSchema } from "@repo/lms-types";
   ```
2. Direct module import:
   ```typescript
   import { CreateCourseRequestSchema } from "@repo/lms-types/schemas/courses";
   ```
