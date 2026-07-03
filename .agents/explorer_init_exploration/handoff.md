# Handoff Report: Initial Codebase Exploration (frontend-web)

This handoff report summarizes the key observations, logic chain, caveats, and conclusions derived from the read-only investigation of the AlphaZero frontend project located in `/mnt/e/AlphaZeroLearningAcademy/frontend-web/`.

---

## 1. Observation

### API Client Structures & Features
The API client definition is located in `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/api/ApiClient.ts`. It has structured endpoints grouped by domain fields:
- **`assessments`** (Lines 1161-1324): CRUD and submissions for MCQ and Handwritten tests.
- **`courses`** (Lines 1325-1883): Syllabus structure, subjects, course CRUD, lesson/assessment item addition, course reordering, study plans, course analytics, student progress list, and in-memory/DB progress completion.
- **`identity`** (Lines 1884-2173): Credential login, device registration, device lock override, inline/managed policies, and multi-tenant token exchange.
- **`library`** (Lines 2174-2486): Physical code redemption (the offline library code economy), libraries CRUD, access code distribution, batch generation.
- **`tenants`** (Lines 2487-2629): Subdomain-based tenant lookup, provisioning, listing, and deletion.
- **`api` & `users`** (Lines 2630-2793): Video uploading debug, streaming, and current user info `/users/me`.

### Code Discrepancies and Incompatibilities Observed:
1. **Tenant Subdomain Lookup Parameter Type Mismatch:**
   - **Path:** `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/app/[tenant]/layout.tsx` (Line 14)
   - **Code:**
     ```typescript
     const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint(
       resolvedParams.tenant
     );
     ```
   - **Signature in `ApiClient.ts` (Line 2558):**
     ```typescript
     alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint: (
       query: { subdomain: string; },
       params: RequestParams = {},
     ) => ...
     ```

2. **Code Redemption Parameter Mismatch:**
   - **Path:** `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/app/[tenant]/page.tsx` (Line 43)
   - **Code:**
     ```typescript
     await apiClient.library.alphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeEndpoint({
       studentId: studentId,
       code: redeemCode,
     });
     ```
   - **Signature in `ApiClient.ts` (Line 717):**
     ```typescript
     export interface AlphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeRequest {
       rawCode?: string;
     }
     ```

3. **Device Lock Endpoint Path and Payload Mismatch:**
   - **Path:** `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/app/[tenant]/device-lock/page.tsx` (Line 19)
   - **Code:**
     ```typescript
     await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsSetMainDeviceSetMainDeviceEndpoint({
       deviceFingerprint: fingerprint
     });
     ```
   - **Signature in `ApiClient.ts` (Line 1918 & Line 553):**
     - Endpoint `alphaZeroModulesIdentityPresentationAuthCommandsSetMainDeviceSetMainDeviceEndpoint` does not exist.
     - The generated endpoint is `alphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint`.
     - The payload type is:
       ```typescript
       export interface AlphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceRequest {
         deviceId?: string;
       }
       ```

4. **Complete Item Endpoint Path and Parameter Mismatch:**
   - **Paths:** `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/app/[tenant]/courses/[courseId]/page.tsx` (Line 51) and `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/components/Quiz.tsx` (Line 23)
   - **Code:**
     ```typescript
     await apiClient.courses.alphaZeroModulesCoursesPresentationCoursesCompleteItemCompleteItemEndpoint({
       studentId,
       courseId,
       itemId: activeVideoItem.id // or quizId
     });
     ```
   - **Signature in `ApiClient.ts` (Line 1334 & Line 238):**
     - Method name `alphaZeroModulesCoursesPresentationCoursesCompleteItemCompleteItemEndpoint` does not exist on `apiClient.courses`.
     - The actual method is `alphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemEndpoint` (POST `/courses/enrollements/{enrollmentId}/complete`).
     - It expects two parameters: `enrollmentId: string` and a body payload `{ bitIndex?: number }`.

5. **Student Registration Endpoint Missing:**
   - **Path:** `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/app/[tenant]/register/page.tsx` (Line 25)
   - **Code:**
     ```typescript
     const res = await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsRegisterStudentRegisterStudentEndpoint({
       tenantId: params.tenant,
       username,
       email,
       password,
       deviceFingerprint: fingerprint,
       platform: 0
     });
     ```
   - **Signature in `ApiClient.ts`:**
     - There is no registration endpoint matching `alphaZeroModulesIdentityPresentationAuthCommandsRegisterStudentRegisterStudentEndpoint` (or any user registration endpoint) defined in `ApiClient.ts`.

6. **Student Dashboard Mapping Mismatch:**
   - **Path:** `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/app/[tenant]/page.tsx` (Line 122, 129, 131)
   - **Code:**
     ```typescript
     {dashboard?.enrollments && dashboard.enrollments.length > 0 && (
       <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
         {dashboard.enrollments.map((course: any) => (
     ```
   - **Signature in `ApiClient.ts` (Line 321):**
     ```typescript
     export interface AlphaZeroModulesCoursesPresentationEnrollementsDashboardDashboardResponse {
       academies?: Record<
         string,
         AlphaZeroModulesCoursesPresentationEnrollementsDashboardEnrollmentDto[]
       >;
     }
     ```
     The returned response structure contains `academies?: Record<string, EnrollmentDto[]>` rather than an `enrollments` field directly.

### Tenant Routing & Proxy:
- `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/proxy.ts` is fully implemented to extract subdomains from headers (e.g. `damascus.localhost:3000` -> `damascus`) and perform rewrites to `/[subdomain]/path`.
- **However, `src/middleware.ts` is missing in the workspace.** Without a Next.js middleware file calling this function, the routing proxy does not execute, making subdomain layouts unreachable under normal conditions.

### Playwright Spec Mocks:
- `/mnt/e/AlphaZeroLearningAcademy/frontend-web/tests/example.spec.ts` consists of the default mock tests hitting `https://playwright.dev/`. No actual functional tests targeting the local dev environment or the user/teacher/admin dashboard pages are defined.

---

## 2. Logic Chain

1. **Routing Inoperability:**
   - *Observation:* We observed `src/proxy.ts` exists but there is no `middleware.ts` in `/src` or the project root.
   - *Reasoning:* Next.js uses the root file `middleware.ts` (or `src/middleware.ts`) to execute middleware logic on incoming requests.
   - *Conclusion:* Because this entry point file does not exist, the proxy is completely disabled.

2. **Frontend UI Compile/Runtime Failures:**
   - *Observation:* We observed multiple instances where frontend components (e.g., dashboard, register, device lock, quiz, course completion) call endpoints that are either named differently, expect completely different parameters, or are not generated at all in `ApiClient.ts`.
   - *Reasoning:* The generated TypeScript types from Swagger (`ApiClient.ts`) represent the strict schema of the backend API. Calling functions with mismatched names/payload formats (e.g. passing a string to an endpoint expecting an object, or passing `deviceFingerprint` instead of `deviceId`) will throw TypeScript compilation errors and fail at runtime.
   - *Conclusion:* The UI is currently out of sync with the generated backend client schemas, requiring significant realignment of handlers (especially for progress bitmasks, code redemption, and device locking).

3. **Incomplete Feature Support:**
   - *Observation:* `[tenant]/teacher/page.tsx` contains static grids and dummy forms with no API calls, while `example.spec.ts` hits `https://playwright.dev`.
   - *Reasoning:* There are no network requests, integration states, or local assertions matching the actual app components.
   - *Conclusion:* The teacher interface is a pure placeholder UI, and Playwright tests are currently completely unconfigured for testing the application.

---

## 3. Caveats

- We did not verify the actual running status of the backend API at `http://localhost:5053`, but we evaluated the generated `ApiClient.ts` file which serves as the static contract of that API.
- We assumed the `ApiClient.ts` represents the latest backend Swagger schema. If the backend changes its OpenAPI generator settings or changes its exposed endpoints, the API client would need to be re-generated.

---

## 4. Conclusion

The AlphaZero frontend is built on Next.js 16.2 (App Router) and React 19.2. It uses TanStack Query and a brutalist styling theme. 

However, **it cannot currently be successfully built or run without compilation errors** due to critical discrepancies between the frontend page implementations and the generated `ApiClient.ts`. Most features (Student dashboard course lists, physical code redemption, device locking, course bitmask progress tracking, and registration) are broken due to parameter mismatches or missing endpoints in the generated API client contract. The teacher dashboard is mock-only. Tenant-level subdomain routing is defined in `proxy.ts` but deactivated because the Next.js `middleware.ts` entry point is missing. Playwright tests are unconfigured and boilerplate-only.

---

## 5. Verification Method

To independently verify these findings:
1. **Compilation Check:** Run `npm run build` or `npx tsc --noEmit` inside `/mnt/e/AlphaZeroLearningAcademy/frontend-web/` to see the exact TS compilation errors generated by the mismatched endpoints listed above.
2. **Inspect Files:**
   - Check `src/app/[tenant]/layout.tsx` at line 14 vs `src/api/ApiClient.ts` at line 2558.
   - Check `src/app/[tenant]/page.tsx` at line 43 vs `src/api/ApiClient.ts` at line 2222 (Redeem code signature).
   - Check `src/app/[tenant]/device-lock/page.tsx` at line 19 vs `src/api/ApiClient.ts` at line 1918.
   - Check `src/app/[tenant]/courses/[courseId]/page.tsx` at line 51 vs `src/api/ApiClient.ts` at line 1334.
3. **Verify Middleware Absence:** Confirm that running `ls -l src/middleware.ts` or `ls -l middleware.ts` in `/mnt/e/AlphaZeroLearningAcademy/frontend-web/` fails to find the files.
