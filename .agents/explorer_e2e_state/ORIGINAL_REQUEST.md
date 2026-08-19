## 2026-07-04T19:38:33Z

Explore the workspace to recover the exact state of the E2E Testing Track.
Specifically:
1. Verify if any test files exist in `frontend-web/tests/` and read them.
2. Check git status and git diff to see if there are any uncommitted test helpers, seed scripts, or configuration changes.
3. Check the compilation status of the `frontend-web` folder using `npx tsc --noEmit` to verify type errors.
4. Search the backend codebase to identify how we can trigger database seeding or reset for E2E tests (e.g. check if there is an endpoint, a CLI command, or if we need to write one).
5. Document all findings in `handoff.md` in your directory.

CRITICAL DIRECTIVE: Do NOT create or modify `src/middleware.ts` under any circumstances, as it causes Next.js to crash. All routing and proxy logic must live ONLY in `src/proxy.ts`. Ensure this constraint is respected.
