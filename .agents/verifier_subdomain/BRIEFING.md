# BRIEFING — 2026-07-02T00:19:15Z

## Mission
Adversarially verify the correctness of subdomain routing and tenant layout lookup in the `frontend-web` project.

## 🔒 My Identity
- Archetype: critic_specialist
- Roles: critic, specialist
- Working directory: /mnt/e/AlphaZeroLearningAcademy/.agents/verifier_subdomain
- Original parent: 03f42b31-109a-495b-9126-bdbef33d3259
- Milestone: Milestone 1: Subdomain Routing & Tenant Layout
- Instance: 1 of 1

## 🔒 Key Constraints
- Review-only — do NOT modify implementation code (our task is to verify and report bugs)
- Network restrictions: CODE_ONLY network mode. No external HTTP/wget/curl targeting external URLs.
- Integrity Mandate: No cheating, no fake implementations.

## Current Parent
- Conversation ID: 03f42b31-109a-495b-9126-bdbef33d3259
- Updated: not yet

## Review Scope
- **Files to review**: `frontend-web/src/middleware.ts`, `frontend-web/src/proxy.ts`, `frontend-web/src/app/[tenant]/layout.tsx`
- **Interface contracts**: `/mnt/e/AlphaZeroLearningAcademy/.agents/implementation_orchestrator/SCOPE.md`
- **Review criteria**: Correctness of middleware routing, static file exclusions, and infinite redirect/rewrite recursion guard.

## Attack Surface
- **Hypotheses tested**:
  - Dynamic path dot bypass: Hypothesized that paths containing dots (e.g. `/courses/next.js-basics`) incorrectly bypass tenant routing. Confirmed.
  - ccTLD parsing error: Hypothesized that country-code top-level domains (e.g. `alphazero.com.sy`) are treated as having tenant subdomains. Confirmed.
  - IPv4 domain parsing error: Hypothesized that visiting via IP address (e.g. `127.0.0.1`) incorrectly extracts the first octet as tenant. Confirmed.
  - Unsafe layout null dereference: Hypothesized that if the tenant API returns null/undefined data without throwing, the layout will crash on `tenant.name`. Confirmed.
  - Exclusions & loop guard: Tested normal routing, `_next`, `api`, files (e.g., `.png`), and route recursion guards. Confirmed they work.
- **Vulnerabilities found**:
  - BUG 1: Any dynamic URL containing a dot (e.g., `/courses/next.js-basics` or `/news/release-1.0`) is excluded from tenant rewrites.
  - BUG 2: ccTLD domains with 3 parts (e.g., `alphazero.com.sy`) extract `'alphazero'` as the tenant subdomain.
  - BUG 3: IP Address hosts (e.g., `127.0.0.1`) extract `'127'` as the tenant subdomain.
  - BUG 4: `[tenant]/layout.tsx` has an unsafe reference `tenant.name` which causes server-side render crash if the lookup resolves to null.
- **Untested angles**:
  - DNS wildcards and IDN (Unicode) subdomains.
  - Complex nested paths and trailing slash next.js configurations.

## Loaded Skills
- **Source**: /mnt/e/AlphaZeroLearningAcademy/.agents/skills/critique/SKILL.md
  - **Local copy**: /mnt/e/AlphaZeroLearningAcademy/.agents/verifier_subdomain/skills/critique/SKILL.md
  - **Core methodology**: UX design evaluation, cognitive load checklist, heuristics-based scoring.
- **Source**: /mnt/e/AlphaZeroLearningAcademy/.agents/skills/harden/SKILL.md
  - **Local copy**: /mnt/e/AlphaZeroLearningAcademy/.agents/verifier_subdomain/skills/harden/SKILL.md
  - **Core methodology**: Boundary conditions, error scenarios, logical properties, and robustness checking.

## Key Decisions Made
- Wrote and verified a Playwright unit test suite `frontend-web/tests/middleware.spec.ts` evaluating routing, exclusions, recursion, port formats, and parsing limits.
- Confirmed test execution and verified the bug behavior programmatically using `@playwright/test`.

## Artifact Index
- /mnt/e/AlphaZeroLearningAcademy/.agents/verifier_subdomain/ORIGINAL_REQUEST.md — Verbatim user request record
- /mnt/e/AlphaZeroLearningAcademy/.agents/verifier_subdomain/BRIEFING.md — Verifier persistent briefing memory
- /mnt/e/AlphaZeroLearningAcademy/frontend-web/tests/middleware.spec.ts — Spec file verifying the middleware logic
- /mnt/e/AlphaZeroLearningAcademy/.agents/verifier_subdomain/progress.md — Liveness progress log
- /mnt/e/AlphaZeroLearningAcademy/frontend-web/tests/proxy.spec.ts — Playwright test suite verifying the proxy routing
- /mnt/e/AlphaZeroLearningAcademy/.agents/verifier_subdomain/handoff.md — Final verification report (this is where our verification is written)
