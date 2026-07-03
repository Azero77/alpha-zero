# Project: AlphaZero Frontend Buildout and E2E Testing

## Architecture
AlphaZero is a multi-tenant SaaS e-learning platform with a Next.js 16 (App Router) + React 19 frontend, integrating with an ASP.NET Core Clean Architecture backend.
- **Tenant Isolation**: Handled via Next.js middleware extracting the tenant subdomain and rewriting requests to tenant routes `/[tenant]/...`.
- **API Client**: Mapped via `src/api/ApiClient.ts`. Uses TanStack Query for caching and API state management.
- **Progress Tracking**: Uses a bitmask (`VARBIT` in Postgres, mapped to `BitArray` in C#).
- **Offline Code Economy**: Users redeem physical library codes to unlock courses.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Subdomain Routing | Implement `src/middleware.ts` to call `proxy.ts`, fix tenant layout lookup. | None | PLANNED |
| 2 | Auth & Registration | Fix global login, exchange tenant token flow, and JIT student provisioning. | M1 | PLANNED |
| 3 | Student Dashboard & Streaming | Map multi-tenant dashboard, video streaming (Shaka Player), bitmask progress checkmarks. | M2 | PLANNED |
| 4 | Code Redemption | Implement library code redemption to unlock course resources. | M3 | PLANNED |
| 5 | Device Locking | Implement device fingerprinting, main device registration, and fingerprint verification page. | M2 | PLANNED |
| 6 | Teacher Dashboard | Implement teacher interface for batch code generation and library logs. | M2 | PLANNED |
| 7 | Playwright E2E Tests | Write Playwright tests covering all user flows (Tiers 1-4). | M3, M4, M5, M6 | PLANNED |
| 8 | Adversarial Coverage | Run Tier 5 Challenger adversarial hardening to cover gaps. | M7 | PLANNED |

## Interface Contracts
### API Mismatches & Solutions
1. **Tenant Lookup**:
   - Frontend passes `resolvedParams.tenant` directly.
   - Endpoint expects `(query: { subdomain: string })`.
   - Fix: Pass `{ subdomain: resolvedParams.tenant }`.
2. **Progress Bitmask Integration**:
   - C# `EnrollementResponse` / `EnrollmentDto` must include `ProgressBitmask` field mapped from `Progress.Bitmask`.
   - `ApiClient.ts` must expose `progressBitmask?: string` representing bit sequence (e.g. `'10110'`).
3. **Complete Item**:
   - Route is `/courses/enrollements/{EnrollmentId}/complete` instead of `CoursesCompleteItem`.
   - Signature: `(enrollmentId: string, data: { bitIndex: number })`.
4. **Device Lock**:
   - Hook in frontend calls non-existent `SetMainDeviceEndpoint({ deviceFingerprint })`.
   - Correct API endpoint: `alphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint` expecting `{ deviceId?: string }`.
5. **Code Redemption**:
   - Frontend calls with `{ studentId, code }`.
   - API expects `{ rawCode: string }`.

## Code Layout
- Frontend Root: `frontend-web`
  - App Routing: `src/app/`
  - Components: `src/components/`
  - Api: `src/api/`
  - Middleware: `src/middleware.ts`
  - Tests: `tests/`
