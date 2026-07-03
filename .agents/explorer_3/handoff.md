# Handoff Report — Subdomain Routing & Tenant Layout Lookup Analysis

This report outlines the analysis of subdomain routing and tenant layout lookup in `/mnt/e/AlphaZeroLearningAcademy/frontend-web/`.

## 1. Observation

### Observation A: Inactive Middleware File
Next.js expects middleware to be defined in `src/middleware.ts` or `middleware.ts` (in the root directory).
- Direct check of `frontend-web/src/` shows no `middleware.ts` is present.
- A routing file `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/proxy.ts` exists containing:
  ```typescript
  export function proxy(request: NextRequest) {
    ...
  }
  ```
- Because Next.js does not recognize `src/proxy.ts` as a middleware entrypoint, the proxy rewrite logic is currently inactive.

### Observation B: Missing Infinite Rewrite Guard in `src/proxy.ts`
Inside `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/proxy.ts`, lines 25–31 read:
```typescript
  if (subdomain && subdomain !== 'www') {
    // Rewrite the URL to a dynamic route for the specific tenant
    url.pathname = `/${subdomain}${url.pathname}`;
    const response = NextResponse.rewrite(url);
    response.headers.set('x-tenant-subdomain', subdomain);
    return response;
  }
```
- There is no check to see if `url.pathname` is already prefixed with the tenant subdomain (e.g. `/${subdomain}/` or equals `/${subdomain}`).
- Since Next.js routes rewritten URLs back through middleware, this triggers recursive rewrites (e.g. `/tenant1/login` -> `/tenant1/tenant1/login` -> `/tenant1/tenant1/tenant1/login`).

### Observation C: API Parameter Type Mismatch in `src/app/[tenant]/layout.tsx`
In `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/app/[tenant]/layout.tsx`, lines 14–16 call:
```typescript
    const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint(
      resolvedParams.tenant
    );
```
- However, checking `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/api/ApiClient.ts` at line 2558 reveals the following definition:
  ```typescript
  alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint:
    (
      query: {
        subdomain: string;
      },
      params: RequestParams = {},
    ) =>
  ```
- The endpoint method expects an object containing `{ subdomain: string }` as the first argument, not a raw `string`. Passing `resolvedParams.tenant` directly leads to compilation and runtime failures.

---

## 2. Logic Chain

1. **Routing Activation**: Next.js requires the entrypoint `src/middleware.ts` to register middleware. Since it is missing, we must add `src/middleware.ts` and export the `proxy` function from `./proxy` as the `middleware` function.
2. **Rewrite Loop Prevention**: Once the middleware is active, Next.js will execute it again for internally rewritten URLs. Without checking if the pathname has already been rewritten to start with the subdomain, it will repeatedly prefix the subdomain to the path. Therefore, we must modify `src/proxy.ts` to return `NextResponse.next()` if the pathname already starts with `/${subdomain}/` or matches `/${subdomain}`.
3. **TypeScript/Compilation Correction**: Passing a raw string to the generated API endpoint causes a type mismatch error during compilation. Since the endpoint expects a query object of shape `{ subdomain: string }`, modifying the argument to `{ subdomain: resolvedParams.tenant }` resolves this error.

---

## 3. Caveats

- **Network Mode**: Investigated in `CODE_ONLY` network mode; external API availability could not be verified online.
- **Localhost Testing**: The middleware extracts subdomains assuming `parts.length >= 2` on localhost. For testing, this assumes a custom host header is sent or dynamic domain routing is configured locally (e.g., `tenant.localhost`).
- **Client Components**: Pages inside `src/app/[tenant]/` use synchronous `params` access (e.g. `params.tenant`). Under React 19 and Next.js 15+, client pages may need `React.use(params)` to handle the dynamic parameter correctly if strict typings are enforced on client pages.

---

## 4. Conclusion

The tenant subdomain routing and layout lookup system is currently non-functional due to:
1. A missing `src/middleware.ts` file.
2. A missing rewrite recursion guard in `src/proxy.ts`.
3. An API client invocation mismatch in `src/app/[tenant]/layout.tsx`.

### Exact Recommendations

#### Recommendation 1: Add `src/middleware.ts`
Create a new file `/mnt/e/AlphaZeroLearningAcademy/frontend-web/src/middleware.ts` containing:
```typescript
import { proxy } from './proxy';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  return proxy(request);
}

export { config } from './proxy';
```

#### Recommendation 2: Modify `src/proxy.ts`
Update `src/proxy.ts` around line 25 to add the rewrite loop guard:
```typescript
  if (subdomain && subdomain !== 'www') {
    // Prevent infinite rewrite loop if already rewritten to the tenant path
    if (url.pathname.startsWith(`/${subdomain}/`) || url.pathname === `/${subdomain}`) {
      return NextResponse.next();
    }
    // Rewrite the URL to a dynamic route for the specific tenant
    url.pathname = `/${subdomain}${url.pathname}`;
    const response = NextResponse.rewrite(url);
    response.headers.set('x-tenant-subdomain', subdomain);
    return response;
  }
```

#### Recommendation 3: Modify `src/app/[tenant]/layout.tsx`
Update the API call on lines 14–16 of `src/app/[tenant]/layout.tsx`:
```typescript
    // Lookup tenant with correct object parameter shape matching the API client SDK definition
    const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint({
      subdomain: resolvedParams.tenant
    });
```

---

## 5. Verification Method

To verify these changes:
1. **Compilation**: Run `npm run build` inside `/mnt/e/AlphaZeroLearningAcademy/frontend-web` to verify that there are no TypeScript compiler errors.
2. **Subdomain Routing & Middleware Execution**:
   - Run the development server using `npm run dev`.
   - Send requests using `curl` with a custom Host header to check rewrite responses:
     ```bash
     curl -I -H "Host: tenant1.localhost:3000" http://localhost:3000/
     ```
   - Verify that the response headers contain `x-tenant-subdomain: tenant1` and do not end in infinite redirects/rewrites.
