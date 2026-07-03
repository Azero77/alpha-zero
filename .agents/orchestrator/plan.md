# Plan: Frontend Buildout and E2E Testing

## Objective
Build out the AlphaZero frontend from top to bottom, implementing every feature and endpoint mapped in the API client, and write comprehensive Playwright E2E tests for all flows.

## Milestone Decomposition
1. **Milestone 1: Codebase Exploration & Architecture Analysis**
   - Explore frontend-web structure, proxy, API client, existing pages.
   - Review backend api contract.
   - Identify missing UI features and components.
2. **Milestone 2: Define PROJECT.md and TEST_INFRA.md**
   - Document architectural patterns, module boundaries, and interfaces.
   - Design E2E test plan (Tiers 1-4).
3. **Milestone 3: E2E Test Suite Creation (E2E Track)**
   - Implement E2E test infrastructure.
   - Build out comprehensive test cases.
4. **Milestone 4: Core Pages & Layouts (Implementation Track)**
   - Authentication (Login, Register).
   - Tenant Provisioning / Management.
   - Course Management & Streaming Player.
   - Physical Library Code Redemption.
5. **Milestone 5: Integration & Verification (Phase 1 & Phase 2)**
   - Phase 1: Pass 100% of E2E tests (Tiers 1-4).
   - Phase 2: Adversarial Coverage Hardening (Tier 5).
