# Original User Request

## 2026-07-01T19:53:15Z

# Teamwork Project Prompt — Draft

> Status: Launched.
> Goal: Build out the frontend and write E2E tests

Build out the entire AlphaZero frontend from top to bottom, implementing every feature and endpoint mapped in the API client, and write comprehensive Playwright E2E tests for all flows. 

Working directory: /mnt/e/AlphaZeroLearningAcademy/frontend-web
Integrity mode: development

## Requirements

### R1. Complete Production-Grade Frontend Implementation
Implement all missing React components, pages, and routing to fully support every endpoint mapped in `ApiClient.ts`. Ensure tenant-awareness is maintained via `proxy.ts` and `X-TenantId`. Every UI component must be production-ready, featuring proper error handling, empty states, and loading skeletons.

### R2. Playwright E2E Test Suite
Create a Playwright end-to-end test suite (`npx playwright test`) that comprehensively covers all user flows (Login, Tenant Provisioning, Course Management, etc.).

## Acceptance Criteria

### Frontend Completeness
- [ ] Every endpoint in `ApiClient.ts` has a corresponding functional UI component.
- [ ] No hardcoded data; everything must interact with the live backend API.
- [ ] All network edge cases (404, 500, timeouts) are explicitly handled with user-friendly error states.

### E2E Testing Verification
- [ ] `npx playwright test` executes and passes successfully for all implemented features against the local backend.
- [ ] Tests verify actual UI rendering and DOM updates, not just network mocks.
