# AlphaZero Frontend Codebase Analysis

This analysis covers the frontend architecture, API client integration, routing, components, and test configuration for the AlphaZero Web frontend (`/mnt/e/AlphaZeroLearningAcademy/frontend-web/`).

---

## 1. API Client and Endpoints Summary
The API client is located in `src/api/ApiClient.ts` (generated via `swagger-typescript-api`) and instantiated in `src/api/client.ts`.

### Endpoint Categories & Represented Features:
1. **`assessments`** (Lines 1161-1324)
   - `alphaZeroModulesAssessmentsPresentationEndpointsSubmissionsSubmitSubmitAssessmentEndpoint` (`POST /assessments/submissions/{submissionId}/submit`) — MCQ and handwritten quiz submissions.
   - `alphaZeroModulesAssessmentsPresentationEndpointsSubmissionsListListSubmissionsEndpoint` (`GET /assessments/submissions`) — List student submission summaries.
   - `alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsUpdateContentUpdateAssessmentContentEndpoint` (`PUT /assessments/{assessmentId}/content`) — Update quiz items, options, correct answers, rubrics.
   - `alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsListListAssessmentsEndpoint` (`GET /assessments`) — Paginated list of assessments.
   - `alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsCreateCreateAssessmentEndpoint` (`POST /assessments`) — Create assessments (MCQ, handwritten, hybrid).
   - `alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsGetGetAssessmentEndpoint` (`GET /assessments/{id}`) — Fetch assessment details (and version snapshot history).

2. **`courses`** (Lines 1325-1883)
   - `alphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemEndpoint` (`POST /courses/enrollements/{enrollmentId}/complete`) — **Bitmask progress tracking** completion action.
   - `alphaZeroModulesCoursesPresentationSubjectsListListSubjectsEndpoint` (`GET /courses/subjects`) — List curriculum subjects.
   - `alphaZeroModulesCoursesPresentationSubjectsCreateCreateSubjectEndpoint` (`POST /courses/subjects`) — Create curriculum subject category.
   - `alphaZeroModulesCoursesPresentationSubjectsGetGetSubjectEndpoint` (`GET /courses/subjects/{id}`) — Get subject details.
   - `alphaZeroModulesCoursesPresentationEnrollementsGetGetEnrollementEndpoint` (`GET /courses/enrollments/{id}`) — Get enrollment details.
   - `alphaZeroModulesCoursesPresentationEnrollementsEnrollEnrollInCourseEndpoint` (`POST /courses/enroll`) — Enroll a student.
   - `alphaZeroModulesCoursesPresentationEnrollementsDashboardGetStudentDashboardEndpoint` (`GET /courses/dashboard/{studentId}`) — Load student's multi-tenant dashboard.
   - `alphaZeroModulesCoursesPresentationCoursesStateApproveCourseEndpoint` (`PATCH /courses/{courseId}/approve`) — State transition: Approve course.
   - `alphaZeroModulesCoursesPresentationCoursesStatePublishCourseEndpoint` (`PATCH /courses/{courseId}/publish`) — State transition: Publish course.
   - `alphaZeroModulesCoursesPresentationCoursesStateRejectCourseEndpoint` (`PATCH /courses/{courseId}/reject`) — State transition: Reject course with comments.
   - `alphaZeroModulesCoursesPresentationCoursesStateSubmitForReviewEndpoint` (`PATCH /courses/{courseId}/review`) — Submit course for QA review.
   - `alphaZeroModulesCoursesPresentationCoursesReorderSectionsReorderSectionsEndpoint` (`POST /courses/{courseId}/sections/reorder`) — Reorder curriculum sections.
   - `alphaZeroModulesCoursesPresentationCoursesReorderItemsReorderItemsEndpoint` (`POST /courses/{courseId}/sections/{sectionId}/reorder`) — Reorder lessons/quizzes within sections.
   - `alphaZeroModulesCoursesPresentationCoursesPlansUpdatePlanUpdatePlanEndpoint` (`PUT /courses/{courseId}/plans/{planId}`) / `AddPlan` / `RemovePlan` — Study plans management.
   - `alphaZeroModulesCoursesPresentationCoursesListListCoursesEndpoint` (`GET /courses`) — Paginated list of courses.
   - `alphaZeroModulesCoursesPresentationCoursesCreateCreateCourseEndpoint` (`POST /courses`) — Create new course.
   - `alphaZeroModulesCoursesPresentationCoursesGetGetCourseEndpoint` (`GET /courses/{id}`) — Fetch course syllabus, modules, and items.
   - `alphaZeroModulesCoursesPresentationCoursesAddSectionAddSectionEndpoint` (`POST /courses/{courseId}/sections`) — Create new course syllabus section.
   - `alphaZeroModulesCoursesPresentationCoursesAddItemAddLessonEndpoint` (`POST /courses/{courseId}/sections/{sectionId}/lessons`) — Add video lesson item to section.
   - `alphaZeroModulesCoursesPresentationCoursesAddItemAddAssessmentEndpoint` (`POST /courses/{courseId}/sections/{sectionId}/assessments`) — Add quiz assessment item to section.
   - `alphaZeroModulesCoursesPresentationAnalyticsGetCourseAnalyticsEndpoint` (`GET /courses/{courseId}/analytics`) — Get course aggregated enrollments and completion rates.
   - `alphaZeroModulesCoursesPresentationAnalyticsListStudentProgressEndpoint` (`GET /courses/{courseId}/students`) — Lists all enrolled students with completion percentages.

3. **`identity`** (Lines 1884-2173)
   - `alphaZeroModulesIdentityPresentationUsersDevicesRegisterDeviceEndpoint` (`POST /identity/users/devices`) — Register browser/device and upload public key.
   - `alphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint` (`POST /identity/users/devices/main`) — Change main locked device.
   - `alphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalsByResourceGetPrincipalsByResourceEndpoint` (`GET /identity/resources/{resourceType}/{resourceId}/principals`) — Fetch authorized roles or users.
   - `alphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalPoliciesGetPrincipalPoliciesEndpoint` (`GET /identity/principals/{principalId}/policies`) — Policy lookup.
   - Policy attachments endpoints (`POST`/`DELETE` inline and managed policies).
   - `alphaZeroModulesIdentityPresentationPrincipalsCommandsCreatePrincipalCreatePrincipalEndpoint` (`POST /identity/principals`) — Add user/role principals.
   - `alphaZeroModulesIdentityPresentationAuthCommandsLoginPrincipalLoginPrincipalEndpoint` (`POST /identity/auth/login-principal`) — Authenticate against root or tenant.
   - `alphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint` (`POST /identity/auth/exchange-tenant-token`) — Exchange principal token for a specific tenant token (X-TenantId authorization).

4. **`library`** (Lines 2174-2486)
   - `alphaZeroModulesLibraryPresentationEndpointsRedemptionAuditLogsGetRedemptionLogsEndpoint` (`GET /library/libraries/{libraryId}/audit-logs`) — Code redemption audit trial.
   - `alphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeEndpoint` (`POST /library/redeem`) — **Physical Library Code redemption** (Unlocks target course resource).
   - CRUD and resource authorization endpoints for Physical Libraries (`/library/libraries`).
   - Access codes generation (`GenerateBatch`, `GenerateAdminCode`) and distribution.

5. **`tenants`** (Lines 2487-2629)
   - `alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint` (`GET /tenants/lookup`) — Resolve subdomain/slug to tenant instance data.
   - `alphaZeroModulesTenantsPresentationEndpointsListTenantsListTenantsEndpoint` (`GET /tenants`) — List academies.
   - `alphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantEndpoint` (`POST /tenants`) — Provision new academy.

6. **`api`** (Video uploading and encryption) (Lines 2630-2776)
   - `alphaZeroModulesVideoUploadingPresentationFeaturesGetVideoKeyEndpoint` (`GET /api/video/keys/{videoId}`) — Retrieve AES-128 HLS decryption keys.
   - S3-to-MediaConvert video upload and transcode debug endpoints.
   - `getApiVideo` (`GET /api/video/{videoId}`) — Video details.

7. **`users`** (Lines 2777-2793)
   - `getUsersMe` (`GET /users/me`) — Context principal details.

---

## 2. Frontend Project Structure

### Page Structure (`src/app/`):
- `layout.tsx` — Global root layout.
- `page.tsx` — Global Infrastructure Admin Dashboard ("Tenant Command") for provisioning new tenant nodes and monitoring active subdomains.
- `login/page.tsx` — Global principal credential authentication page.
- `[tenant]/` (Dynamic tenant folder based on subdomain rewrite):
  - `layout.tsx` — Dynamic Layout lookup via Subdomain Lookup endpoint. Sets colors dynamically on CSS variables (`--color-primary`, `--color-secondary`) for white-label styling.
  - `page.tsx` — Student Dashboard featuring code redemption and course grid.
  - `login/page.tsx` — Tenant user login/exchange screen.
  - `register/page.tsx` — Student account registration.
  - `device-lock/page.tsx` — Device fingerprint mismatch locking block page.
  - `teacher/page.tsx` — Hardcoded mock dashboard layout for teachers.
  - `courses/[courseId]/page.tsx` — Course interactive details page. Integrates video streaming and quiz modules.

### Shared Components (`src/components/`):
- `Providers.tsx` — React-Query configuration provider (`staleTime: 60000`, `retry: 1`).
- `Quiz.tsx` — Quiz MCQ handler (Client component).
- `VideoPlayer.tsx` — Shaka Player overlay wrapper (Uses standard polyfills and UI overlays for dash/hls playback).

---

## 3. Tenant Routing and Proxy Setup
- **`src/proxy.ts`** matches paths using next `NextResponse.rewrite` matching.
  - Subdomains are parsed from the host header (e.g. `damascus.localhost:3000` -> `damascus`).
  - Rewrites target URL pathnames into `/[subdomain]/[path]` transparently.
  - Static paths like `_next/`, `api/` or filenames are excluded from rewrite.
  - Adds `x-tenant-subdomain` header.
- **Critical Configuration Mismatch:**
  - **There is no `middleware.ts` registered** in the root of the project to call `proxy.ts`. Thus, next middleware is currently not routing subdomain layouts.

---

## 4. Feature Implementation & UI-to-API Mismatch Status

### Fully Implemented Endpoints in UI:
- Provisioning and monitoring of subdomains (Global root page `/` -> `ListTenants` & `CreateTenant`).
- App User Authenticator (Global `/login` -> `LoginPrincipal`).
- Tenant User Authentication Exchange (Tenant exchange `/login` -> `LoginAsTenantUser`).

### Placeholders / Mock Implementations:
- **Teacher Dashboard (`[tenant]/teacher/page.tsx`):**
  - Fully mock. Displays placeholder metrics and a simulated batch generation form. Has no actual API connections.

### Discrepancies and Compile/Runtime Errors:
1. **Tenant Lookup Param Mismatch:**
   - **File:** `src/app/[tenant]/layout.tsx` (Line 14)
   - **Issue:** Passes dynamic route string directly: `apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint(resolvedParams.tenant)`
   - **Correct API Signature:** Expects query object: `(query: { subdomain: string })`. Should be passed as `{ subdomain: resolvedParams.tenant }`.

2. **Code Redemption Param Mismatch:**
   - **File:** `src/app/[tenant]/page.tsx` (Line 43)
   - **Issue:** Passes `{ studentId: studentId, code: redeemCode }`.
   - **Correct API Signature:** `alphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeEndpoint` expects `{ rawCode?: string }`. Mismatched payload causes runtime request issues.

3. **Device Lock Hook Mismatch:**
   - **File:** `src/app/[tenant]/device-lock/page.tsx` (Line 19)
   - **Issue:** Calls `apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsSetMainDeviceSetMainDeviceEndpoint({ deviceFingerprint: fingerprint })`.
   - **Correct API Signature:** This method does not exist. The correct generated path is `apiClient.identity.alphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint`. It expects `{ deviceId?: string }` payload, not `deviceFingerprint`.

4. **Complete Item Endpoint Mismatch:**
   - **Files:** `src/app/[tenant]/courses/[courseId]/page.tsx` (Line 51) and `src/components/Quiz.tsx` (Line 23).
   - **Issue:** Calls `apiClient.courses.alphaZeroModulesCoursesPresentationCoursesCompleteItemCompleteItemEndpoint({ studentId, courseId, itemId })`.
   - **Correct API Signature:** This path does not exist. The correct endpoint is `apiClient.courses.alphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemEndpoint` (Route: `/courses/enrollements/{enrollmentId}/complete`). It expects `enrollmentId` as path parameter, and `{ bitIndex?: number }` in the request body.

5. **Student Registration Endpoint Mismatch:**
   - **File:** `src/app/[tenant]/register/page.tsx` (Line 25)
   - **Issue:** Calls `apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsRegisterStudentRegisterStudentEndpoint(...)`.
   - **Correct API Signature:** This endpoint is completely missing from `ApiClient.ts`. Student registration cannot compile or run.

6. **Student Dashboard Enrollment Mapping:**
   - **File:** `src/app/[tenant]/page.tsx` (Line 122)
   - **Issue:** Attempts to map `dashboard.enrollments` directly.
   - **Correct API Signature:** The `DashboardResponse` returns `academies?: Record<string, EnrollmentDto[]>`. It has no `enrollments` array at the root.

---

## 5. Playwright Testing Setup
- **Config file:** `playwright.config.ts`
  - Targets tests in `./tests`.
  - Configured to run tests in parallel using the `chromium` browser project (Desktop Chrome).
  - WebServer execution block is currently commented out.
- **Spec files:**
  - `tests/example.spec.ts` — Contains only two default Playwright boilerplate tests hitting `https://playwright.dev/`.
  - **No tests** are currently configured to test the actual application or tenants.
