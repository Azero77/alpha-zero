# 📋 API Endpoint Audit & Documentation Master TODO

This document tracks the progress of auditing each endpoint (tracing `FluentValidation`, `ErrorOr`, and Domain error codes) and subsequently updating the endpoint's OpenAPI documentation.

Status Legend:
- [ ] Not Started
- [x] Audited (Error codes and flow documented in `docs/api-audit/`)
- [x] Documented (OpenAPI `Summary` / `Produces` implemented in code)

---

## 1. Assessments Module (6 Endpoints)
- **Audit File:** `docs/api-audit/01-assessments.md`
- [x] `POST /assessments` (`CreateAssessmentEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /assessments/{Id}` (`GetAssessmentEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /assessments` (`ListAssessmentsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PUT /assessments/{Id}/content` (`UpdateAssessmentContentEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /assessments/{AssessmentId}/submissions` (`ListSubmissionsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /assessments/{AssessmentId}/submissions` (`SubmitAssessmentEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

---

## 2. Courses Module (24 Endpoints)
- **Audit File:** `docs/api-audit/02-courses.md`
### Courses & Curriculum
- [x] `POST /courses` (`CreateCourseEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /courses/{Id}` (`GetCourseEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /courses` (`ListCoursesEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /courses/{CourseId}/sections` (`AddSectionEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /courses/{CourseId}/sections/{SectionId}/lessons` (`AddLessonEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /courses/{CourseId}/sections/{SectionId}/quizzes` (`AddQuizEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /courses/{CourseId}/sections/reorder` (`ReorderSectionsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /courses/{CourseId}/sections/{SectionId}/reorder` (`ReorderItemsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Course Lifecycle / State
- [x] `PATCH /courses/{CourseId}/review` (`SubmitForReviewEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PATCH /courses/{CourseId}/approve` (`ApproveEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PATCH /courses/{CourseId}/reject` (`RejectEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PATCH /courses/{CourseId}/publish` (`PublishEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Plans
- [x] `POST /courses/{CourseId}/plans` (`AddPlanEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PUT /courses/{CourseId}/plans/{PlanId}` (`UpdatePlanEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `DELETE /courses/{CourseId}/plans/{PlanId}` (`RemovePlanEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Enrollments & Progress
- [x] `POST /courses/enroll` (`EnrollInCourseEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /courses/enrollments/{Id}` (`GetEnrollementEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /courses/enrollements/{EnrollmentId}/complete` (`CompleteItemEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /courses/dashboard/{StudentId}` (`GetStudentDashboardEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Subjects
- [x] `POST /courses/subjects` (`CreateSubjectEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /courses/subjects/{id}` (`GetSubjectEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /courses/subjects` (`ListSubjectsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Analytics
- [x] `GET /courses/{CourseId}/analytics` (`GetCourseAnalyticsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /courses/{CourseId}/progress` (`ListStudentProgressEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

---

## 3. Identity Module (14 Endpoints)
- **Audit File:** `docs/api-audit/03-identity.md`
### Auth & Registration
- [x] `POST /identity/auth/register-student` (`RegisterStudentEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /identity/auth/login-principal` (`LoginPrincipalEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /identity/auth/exchange-tenant-token` (`LoginAsTenantUserEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Devices
- [x] `POST /identity/users/devices` (`RegisterDeviceEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /identity/users/devices/main` (`SetMainDeviceEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Policies
- [x] `POST /identity/policies/managed` (`CreateManagedPolicyEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `DELETE /identity/policies/managed/{PolicyId}` (`DeleteManagedPolicyEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Principals & Authorization
- [x] `POST /identity/principals` (`CreatePrincipalEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /identity/principals/{PrincipalId}/policies/inline` (`AttachInlinePolicyEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `DELETE /identity/principals/{PrincipalId}/policies/inline/{PolicyId}` (`DetachInlinePolicyEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}` (`AttachManagedPolicyEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `DELETE /identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}` (`DetachManagedPolicyEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /identity/principals/{PrincipalId}/policies` (`GetPrincipalPoliciesEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /identity/resources/{ResourceType}/{ResourceId}/principals` (`GetPrincipalsByResourceEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

---

## 4. Library Module (13 Endpoints)
- **Audit File:** `docs/api-audit/04-library.md`
### Access Codes
- [x] `POST /library/libraries/{LibraryId}/access-codes/generate` (`GenerateBatchEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /library/admin/access-codes/generate-single` (`GenerateAdminCodeEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /library/access-codes/batches/{BatchId}/distribute` (`DistributeBatchEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /library/access-codes/void` (`VoidCodeEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /library/redeem` (`RedeemCodeEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /library/libraries/{LibraryId}/audit-logs` (`GetRedemptionLogsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

### Libraries & Resources
- [x] `POST /library/libraries` (`CreateLibraryEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /library/libraries/{Id}` (`GetLibraryEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /library/libraries` (`ListLibrariesEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PATCH /library/libraries/{Id}` (`UpdateLibraryEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `DELETE /library/libraries/{Id}` (`DeleteLibraryEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `POST /library/libraries/{Id}/resources` (`AuthorizeResourceEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `DELETE /library/libraries/{Id}/resources` (`DeauthorizeResourceEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

---

## 5. Tenants Module (6 Endpoints)
- **Audit File:** `docs/api-audit/05-tenants.md`
- [x] `POST /tenants` (`CreateTenantEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /tenants/{Id}` (`GetTenantEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /tenants` (`ListTenantsEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET /tenants/lookup` (`LookupTenantEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PUT /tenants/{Id}` (`UpdateTenantEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `DELETE /tenants/{Id}` (`DeleteTenantEndpoint`)
  - [x] Audited
  - [x] Documented in Presentation

---

## 6. VideoUploading Module (5 Endpoints)
- **Audit File:** `docs/api-audit/06-video-uploading.md`
- [x] `POST api/video-uploading/upload` (`Upload.Endpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET api/video/{videoId:guid}` (`GetStreamingInfo.Endpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `GET api/video/keys/{VideoId:guid}` (`GetVideoKey.Endpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `PATCH api/video-uploading/debug/videos/{id:guid}` (`UpdateVideoInfo.Endpoint`)
  - [x] Audited
  - [x] Documented in Presentation
- [x] `api/video-uploading/debug/videos` (`Debug.Endpoint`)
  - [x] Audited
  - [x] Documented in Presentation
