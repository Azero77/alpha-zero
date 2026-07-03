# Scope: AlphaZero E2E Testing Track

## Architecture
- **Framework**: Playwright (TypeScript) configured in `frontend-web/playwright.config.ts`.
- **Target Environments**: Local Next.js frontend (subdomains mapped dynamically) and Clean Architecture ASP.NET Core backend.
- **Execution Target**: Verify UI rendering, local storage, API routing, and state transitions without mocking backend endpoints.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Test Infra & Auth Tests | Set up Playwright config, helpers, database seed command/API, and write `tests/auth.spec.ts` and `tests/tenant.spec.ts` (10 Tier 1/2 tests). | None | PLANNED |
| 2 | Dashboard & Playback Tests | Write `tests/dashboard.spec.ts` and `tests/playback.spec.ts` (20 Tier 1/2 tests). | M1 | PLANNED |
| 3 | Redemption & Device Lockdown | Write `tests/redemption.spec.ts` and `tests/device-lock.spec.ts` (20 Tier 1/2 tests). | M1 | PLANNED |
| 4 | Teacher Code Management | Write `tests/teacher.spec.ts` (10 Tier 1/2 tests). | M1 | PLANNED |
| 5 | Cross-Feature & Real-World | Write `tests/cross-feature.spec.ts` (7 Tier 3 tests) and `tests/real-world.spec.ts` (5 Tier 4 tests). Verify full pass and publish `TEST_READY.md`. | M2, M3, M4 | PLANNED |

## Interface Contracts
- **Base URLs**:
  - Main App: `http://localhost:3000`
  - Tenant Subdomains: `http://[tenant].localhost:3000` (e.g. `http://damascus.localhost:3000`)
- **Backend API**:
  - Seed endpoint or CLI scripts to set up clean tenants, users, and codes before runs.
