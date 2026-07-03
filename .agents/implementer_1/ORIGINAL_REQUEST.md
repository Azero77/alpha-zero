## 2026-07-01T23:45:25Z

Implement the changes for Milestone 1: Subdomain Routing & Tenant Layout in the `frontend-web` project.

Here are the Explorer's findings:
1. Create `src/middleware.ts` to export the `proxy` function from `./proxy` as Next.js middleware.
2. Modify `src/proxy.ts` to include a rewrite loop guard so that it doesn't repeatedly rewrite URLs that already contain the subdomain prefix.
3. Modify `src/app/[tenant]/layout.tsx` to fix the API parameter mismatch in `apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint` by passing the object `{ subdomain: resolvedParams.tenant }` instead of the raw string.

After making these changes, run a TypeScript compiler check (`npx tsc --noEmit`) and/or a build check (`npm run build`) in `frontend-web/` to verify that there are no compilation errors related to this layout page. Report your findings and results in handoff.md.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## 2026-07-01T21:05:58Z

Please run `npx tsc --noEmit` inside `frontend-web` to check the current compilation errors and output them verbatim in your handoff.md.

## 2026-07-02T00:34:20Z

Please run `git status` and `git diff` at the repository root to see what modifications have been made to the frontend and backend, and write a summary in your handoff.md.

## 2026-07-03T17:49:07Z

Please implement the code fixes for Milestone 1 in the `frontend-web` and `src/Modules/Tenants/Presentation` codebase:

1. Backend: Update `src/Modules/Tenants/Presentation/Endpoints/LookupTenant/LookupTenantEndpoint.cs`.
   - Update `LookupTenantBranding` record to:
     `public record LookupTenantBranding(string? PrimaryColor, string? SecondaryColor, string? LogoUrl);`
   - Map it:
     `new LookupTenantBranding(tenant.PrimaryColor, tenant.SecondaryColor, tenant.LogoUrl)`

2. Frontend Client: Update `frontend-web/src/api/ApiClient.ts` around line 822 to manually add `secondaryColor` to the type:
   ```typescript
   export interface AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantBranding {
     primaryColor?: string | null;
     secondaryColor?: string | null;
     logoUrl?: string | null;
   }
   ```

3. Subdomain Proxy: Update `frontend-web/src/proxy.ts` to implement robust subdomain extraction:
   - Extract the hostname and strip any port segment.
   - Add a regex asset filter instead of naive `.includes('.')`. Whitelist common static extensions:
     `const staticExtensionRegex = /\.(png|jpg|jpeg|gif|svg|ico|css|js|json|woff2?|ttf|map|txt|mp3|mp4|webm)$/i;`
   - Check if the host is a valid IPv4 address (if so, return `NextResponse.next()`).
   - Extract the subdomain by checking against base domains: `['alpha-zero.com', 'alphazero.co.uk', 'alphazero.com.sy']` or `localhost`.
   - Avoid treating `www` as a tenant subdomain.

4. Layout Null Guard: In `frontend-web/src/app/[tenant]/layout.tsx`, add a null check for `tenant` right after resolution:
   ```typescript
   if (!tenant) {
     notFound();
   }
   ```
   Remove any unsafe type casts on `branding` since `secondaryColor` is now natively typed.

5. Test Assertions: Update the Playwright tests in `frontend-web/tests/middleware.spec.ts` to assert correct, bug-free routing (i.e. `/courses/math.101` SHOULD rewrite to `tenant1` and `alphazero.co.uk` SHOULD be `null` subdomain).

After implementing, run a TypeScript compiler check (`npx tsc --noEmit`) and execute the Playwright test suite `npx playwright test tests/middleware.spec.ts` to verify everything compiles and passes. Write your changes and verification outcomes to handoff.md.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.
