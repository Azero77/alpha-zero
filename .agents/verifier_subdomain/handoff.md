# Handoff - Subdomain Routing & Tenant Layout Verification Report

## 1. Observation
Direct observations and quotes from the codebase and test execution:
- **File Path**: `frontend-web/src/proxy.ts`
  - **Exclusion Logic** (lines 11-17):
    ```typescript
    if (
      url.pathname.startsWith('/_next') ||
      url.pathname.startsWith('/api') ||
      url.pathname.includes('.')
    ) {
      return NextResponse.next();
    }
    ```
  - **Subdomain Extraction** (lines 19-23):
    ```typescript
    const isLocalhost = hostname.includes('localhost');
    const parts = hostname.split('.');
    const subdomain = (parts.length >= 3 || (isLocalhost && parts.length >= 2)) ? parts[0] : null;
    ```
  - **Recursion Guard** (lines 25-35):
    ```typescript
    if (subdomain && subdomain !== 'www') {
      if (url.pathname.startsWith(`/${subdomain}/`) || url.pathname === `/${subdomain}`) {
        return NextResponse.next();
      }
      url.pathname = `/${subdomain}${url.pathname}`;
      const response = NextResponse.rewrite(url);
      response.headers.set('x-tenant-subdomain', subdomain);
      return response;
    }
    ```
- **File Path**: `frontend-web/src/app/[tenant]/layout.tsx`
  - **Tenant Resolution** (lines 11-22):
    ```typescript
    let tenant;
    try {
      const resolvedParams = await params;
      const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint({
        subdomain: resolvedParams.tenant
      });
      tenant = res.data;
    } catch {
      notFound();
    }
    ```
  - **Header Rendering** (line 37):
    ```typescript
    <h1 className="text-xl font-bold">{tenant.name || 'AlphaZero Academy'}</h1>
    ```
- **Test Command**: `npx playwright test tests/middleware.spec.ts --project=chromium` run from `/mnt/e/AlphaZeroLearningAcademy/frontend-web`
- **Test Output**:
  ```
  Running 7 tests using 4 workers
  ...
    7 passed (3.6s)
  ```
  This includes:
  - `BUG: Valid path containing a dot gets excluded from subdomain routing` (passed)
  - `BUG/LIMITATION: Nested/complex subdomains are parsed incorrectly` (passed)

## 2. Logic Chain
1. **Dot Exclusion Bug**: In `proxy.ts`, `url.pathname.includes('.')` is used to check if the path is a static file asset. However, if a dynamic route path contains a dot character (such as `/courses/math.101` or `/course/1.0-intro`), `includes('.')` will evaluate to `true`. This causes the middleware to return `NextResponse.next()` immediately, bypassing the rewrite loop. As a result, the request is never rewritten under the tenant context (e.g. `/tenant1/courses/math.101`), causing a 404 error when routed at the root path.
2. **Base Domain Parsing Bug**: In `proxy.ts`, `subdomain` is extracted by splitting the hostname by dots (`parts = hostname.split('.')`). If the hostname contains 3 or more parts, `parts[0]` is extracted as the subdomain. However, if the project is deployed under a base domain with multiple parts (e.g. `alphazero.co.uk`), a direct request to `http://alphazero.co.uk/courses` has 3 parts in the host header (`['alphazero', 'co', 'uk']`). The length is 3, causing the middleware to falsely extract `'alphazero'` as the tenant subdomain and rewrite the request to `/alphazero/courses`.
3. **Null Dereference Risk in Tenant Layout**: In `layout.tsx`, the `catch` block only triggers if the API promise rejects. If the backend returns a 200 OK status but the body is empty/null, `tenant` will be assigned `res.data` (which is `null` or `undefined`). When Next.js attempts to render line 37 (`tenant.name`), it will throw a `TypeError: Cannot read properties of null (reading 'name')`, causing the SSR render to crash rather than gracefully executing the `notFound()` page.

## 3. Caveats
- The layout lookup crash was analyzed statically since the mock backend was not fully integrated into the test environment.
- Subdomain tests were run against a direct mock wrapper of the Next.js `proxy.ts` middleware under Playwright's Node.js test runner to ensure fast and repeatable execution without database dependencies.

## 4. Conclusion
The routing middleware successfully handles simple subdomains (e.g. `tenant1.localhost:3000` -> `/tenant1/...`), correctly filters standard static files (`/_next`, `/api`, `/logo.png`), and prevents infinite rewrite recursion via prefix-matching.
However, it contains three bugs that make it unsuitable for production:
1. **Dot Path Exclusion**: Causes dynamic paths containing dots to bypass subdomain routing.
2. **Base Domain Parsing**: Falsely extracts subdomains on multi-part base domains (like `.co.uk`).
3. **Layout Rendering Crash**: Vulnerable to crashing if the API endpoint returns successful but empty (`null`) tenant data.

*Actionable Recommendations:*
- Replace `url.pathname.includes('.')` with a regex targeting actual file extensions or utilize Next.js config `matcher`.
- Extract base domain suffix safely by comparing against a configured base domain or utilizing a public suffix lookup.
- Add a null check in the layout component before accessing `tenant` properties:
  ```typescript
  if (!tenant) {
    notFound();
  }
  ```

## 5. Verification Method
To verify these findings, run the following command from the `frontend-web` folder:
```bash
npx playwright test tests/middleware.spec.ts --project=chromium
```
Inspect the spec file:
`frontend-web/tests/middleware.spec.ts`
*Invalidation Conditions:* If the routing logic in `proxy.ts` is changed or the tests are altered/removed, this verification report is invalidated.
