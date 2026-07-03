# Scope: AlphaZero Frontend Buildout

## Architecture
Next.js 16 (App Router) + React 19 frontend integrated with ASP.NET Core Clean Monolith backend.
- Tenant-awareness is resolved via `proxy.ts` rewrites to dynamic routes `/[tenant]/...`.
- State managed via TanStack Query and standard React hooks.
- Device locking verifies the active browser's fingerprint before authorizing access.
- Code redemption unlocks courses via library codes.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Subdomain Routing & Tenant Layout | Implement `src/middleware.ts` to call `proxy.ts`, fix tenant layout lookup. | None | DONE |
| 2 | Auth Exchange & Registration | Fix global and tenant login, JIT student provisioning, implement missing register endpoint. | M1 | PLANNED |
| 3 | Student Dashboard, Syllabus & Streaming | Map course list in `[tenant]/page.tsx`, dynamic styles, and Shaka Player integration in `VideoPlayer.tsx`. | M2 | PLANNED |
| 4 | Code Redemption & Progress Bitmask | Implement library code redemption, update C# `EnrollementResponse` and `EnrollmentDto` to expose `ProgressBitmask` string, update `ApiClient.ts`, and render completion checkmarks. | M3 | PLANNED |
| 5 | Device Locking | Implement fingerprinting, set main device, and validation page in `[tenant]/device-lock/page.tsx`. | M2 | PLANNED |
| 6 | Teacher Dashboard | Connect batch generation, libraries CRUD, and redemption audit logs in `[tenant]/teacher/page.tsx`. | M2 | PLANNED |
| 7 | E2E Test Suite verification | Poll for `TEST_READY.md`, run tests, fix any bugs, and verify all tests pass. | M1-M6 | PLANNED |

## Interface Contracts
### 1. Tenant Subdomain Lookup
- Frontend: `apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint({ subdomain: resolvedParams.tenant })`

### 2. Student Registration Endpoint
- C# Backend exposes registration at `POST /identity/auth/register-student` (or similar).
- Need to check if there is an existing endpoint in identity presentation, and regenerate `ApiClient.ts` or add it manually.

### 3. Student Dashboard
- Response: `academies: Record<string, EnrollmentDto[]>`
- Dashboard must iterate over record values rather than accessing a flat array.

### 4. Progress Bitmask & Completion
- C# `EnrollementResponse` and `EnrollmentDto` must expose `ProgressBitmask` string (representing `BitArray` as '1010' string).
- API clients must be updated to contain `progressBitmask: string`.
- Completion endpoint: `apiClient.courses.alphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemEndpoint(enrollmentId, { bitIndex })` (Route: `POST /courses/enrollements/{enrollmentId}/complete`).

### 5. Device Lock
- API endpoint: `apiClient.identity.alphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint({ deviceId })`.
- Handled via fingerprint on device block page.

### 6. Code Redemption
- API endpoint: `apiClient.library.alphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeEndpoint({ rawCode })`.
