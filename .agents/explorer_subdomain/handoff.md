# Handoff Report: Subdomain Routing & Tenant Layout Lookup

This report analyzes the subdomain routing and tenant layout lookup mechanisms in the AlphaZero frontend (`frontend-web`) and recommends the exact additions and modifications required to achieve type-safety, correct runtime routing, and compilation.

---

## 1. Observation

### Middleware / Proxy Routing
- **File Checked**: `frontend-web/src/proxy.ts` (lines 4-38)
- **Observation**: The proxy function `proxy(request: NextRequest)` and matching config are defined, but there is no `middleware.ts` or `middleware.js` in the `src/` root. Next.js does not run custom routing middleware unless it is named `middleware.ts` inside `src/` (or the root directory).
- **Result**: Subdomain rewriting logic is completely bypassed at runtime.

### Tenant Layout Lookup
- **File Checked**: `frontend-web/src/app/[tenant]/layout.tsx` (lines 14-16, 23)
- **Observation**:
  - The API call to look up the tenant passes the subdomain string directly:
    ```typescript
    const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint(
      resolvedParams.tenant
    );
    ```
  - Running `npx tsc --noEmit` returns the following compiler error:
    ```
    src/app/[tenant]/layout.tsx:15:7 - error TS2345: Argument of type 'string' is not assignable to parameter of type '{ subdomain: string; }'.
    ```
  - The branding styles lookup references `branding?.secondaryColor`:
    ```typescript
    '--color-secondary': branding?.secondaryColor || '#F4B400',
    ```
  - Running `npx tsc --noEmit` returns the following compiler error:
    ```
    src/app/[tenant]/layout.tsx:23:38 - error TS2339: Property 'secondaryColor' does not exist on type 'AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantBranding'.
    ```

### Backend Lookup Endpoint Mapping
- **File Checked**: `src/Modules/Tenants/Presentation/Endpoints/LookupTenant/LookupTenantEndpoint.cs` (lines 11-12, 38-43)
- **Observation**:
  - The backend defines:
    ```csharp
    public record LookupTenantBranding(string? PrimaryColor, string? LogoUrl);
    public record LookupTenantResponse(Guid Id, string Subdomain, string Name, LookupTenantBranding Branding);
    ```
  - The query `GetTenantBySubdomainQuery` handler returns a `TenantDto` containing `SecondaryColor`, but `LookupTenantBranding` does not include it, meaning it is not mapped and is missing from the Swagger contract.

### Tenant Login and Registration Runtime Mismatches
- **File Checked**: `frontend-web/src/app/[tenant]/login/page.tsx` (lines 38-43) and `frontend-web/src/app/[tenant]/register/page.tsx` (lines 25-32)
- **Observation**:
  - In `login/page.tsx`, the exchange token call passes the subdomain slug `params.tenant` (e.g. `"damascus"`) directly to `tenantId`:
    ```typescript
    const res = await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint({
      tenantId: params.tenant,
      ...
    ```
    Because the backend expects `Guid TenantId` (as a UUID string in TypeScript), passing a slug will result in a runtime parsing error (400 Bad Request/500 Server Error).
  - In `register/page.tsx`, the client calls `RegisterStudentEndpoint` which does not exist in the API or backend:
    ```
    src/app/[tenant]/register/page.tsx:25:44 - error TS2551: Property 'alphaZeroModulesIdentityPresentationAuthCommandsRegisterStudentRegisterStudentEndpoint' does not exist...
    ```

---

## 2. Logic Chain

1. **Routing Inactivity**: Next.js requires a root-level `middleware.ts` to execute proxy middleware. Because this file is missing, any subdomains visited do not undergo path rewriting, meaning they cannot resolve to `src/app/[tenant]`. Adding `src/middleware.ts` that delegates to `src/proxy.ts` resolves this.
2. **Lookup Payload Type Mismatch**: The swagger-generated `ApiClient.ts` defines `LookupTenantEndpoint` as taking a query object `{ subdomain: string }`. `layout.tsx` passes a raw string, failing type-safety. Passing the slug inside `{ subdomain: resolvedParams.tenant }` resolves the compile error.
3. **Secondary Color Exclusion**: The backend's `LookupTenantEndpoint` omits `SecondaryColor` from `LookupTenantBranding`. Extending `LookupTenantBranding` to include `SecondaryColor` and mapping it from `tenant.SecondaryColor` ensures that regenerating the client resolves the type-safety error in `layout.tsx`.
4. **Tenant ID Runtime Error**: In `login/page.tsx`, the exchange endpoint accepts a tenant's database UUID, not a subdomain slug. Resolving the subdomain slug to a tenant Guid via `LookupTenantEndpoint` on the client-side before token exchange fixes the runtime exception.
5. **Missing Registration API**: The `RegisterStudentEndpoint` is completely absent on the backend because students are JIT-provisioned inside `LoginAsTenantUser` on token exchange. The registration page must either be updated to create a principal first and then exchange it, or a separate backend signup endpoint must be added.

---

## 3. Caveats

- We assume that the subdomains will always be mapped to the tenant's `subdomain` column.
- For local testing of subdomains, developers must configure host aliases (e.g. `/etc/hosts` mapping `damascus.localhost` to `127.0.0.1`) or test using tools like `local.alphazero.com`.
- Public/student registration remains non-functional on the frontend until a decision is made to either allow public `CreatePrincipal` or introduce a dedicated endpoint.

---

## 4. Conclusion & Recommendations

To correct subdomain routing and layout lookup, the following additions and modifications must be implemented.

### 4.1. File Additions

#### 1. Add `frontend-web/src/middleware.ts`
Create a new file to wire up Next.js middleware with `proxy.ts`.

```typescript
// Path: /mnt/e/AlphaZeroLearningAcademy/frontend-web/src/middleware.ts
import { proxy } from './proxy';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  return proxy(request);
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
};
```

---

### 4.2. File Modifications

#### 1. Modify `frontend-web/src/app/[tenant]/layout.tsx`
Fix the query parameter mapping and unwrap `params` correctly (Next.js 15+ convention).

**Before:**
```typescript
    const resolvedParams = await params;
    // Lookup tenant
    const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint(
      resolvedParams.tenant
    );
    const tenant = res.data;
```

**After:**
```typescript
    const resolvedParams = await params;
    // Lookup tenant using query object
    const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint({
      subdomain: resolvedParams.tenant
    });
    const tenant = res.data;
```

---

#### 2. Modify `src/Modules/Tenants/Presentation/Endpoints/LookupTenant/LookupTenantEndpoint.cs`
Update the backend endpoint to include `SecondaryColor` in the branding payload.

**Before (Lines 11-12 & 42):**
```csharp
public record LookupTenantBranding(string? PrimaryColor, string? LogoUrl);
...
new LookupTenantBranding(tenant.PrimaryColor, tenant.LogoUrl)
```

**After:**
```csharp
public record LookupTenantBranding(string? PrimaryColor, string? SecondaryColor, string? LogoUrl);
...
new LookupTenantBranding(tenant.PrimaryColor, tenant.SecondaryColor, tenant.LogoUrl)
```

---

#### 3. Modify `frontend-web/src/app/[tenant]/login/page.tsx`
Resolve the tenant subdomain slug into its database UUID (Guid) before invoking `LoginAsTenantUser`.

**Before (Lines 38-43):**
```typescript
      const res = await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint({
        tenantId: params.tenant,
        publicKey: "none",
        deviceName: "Web Browser",
        platform: 0 // Web
      });
```

**After:**
```typescript
      // 1. Resolve subdomain slug to Guid first
      const tenantRes = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint({
        subdomain: params.tenant
      });
      const realTenantId = tenantRes.data.id;
      if (!realTenantId) {
        throw new Error("Tenant not found.");
      }

      // 2. Perform key exchange using resolved Guid
      const res = await apiClient.identity.alphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint({
        tenantId: realTenantId,
        publicKey: "none",
        deviceName: "Web Browser",
        platform: 0 // Web
      });
```

---

## 5. Verification Method

To verify these changes:
1. **Regenerate Client**: Rebuild the backend and run client code generation to update `frontend-web/src/api/ApiClient.ts` containing the updated `LookupTenantBranding` property structure.
2. **Type Check**: Execute `npx tsc --noEmit` inside `frontend-web/`. The compilation errors in `layout.tsx` and `login/page.tsx` must be resolved.
3. **Run Dev Server**: Launch the frontend (`npm run dev`) and navigate to `http://<subdomain>.localhost:3000/login`. Ensure the middleware rewrites the host header properly and requests are redirected to `/[tenant]/login`.
