# AlphaZero LMS — Multi-Tenant White-Label Branding System: Implementation Plan & Tasks

> **Architecture Context:** Modular Monolith (Enterprise Clean Architecture, .NET 9) + Next.js 15 (Turborepo, App Router, Tailwind v4, shadcn/ui).
> **Mandates:** Tenant-awareness, low-bandwidth optimization (Syria/MENA), zero FOUC, automatic WCAG contrast safety, zero component-level styling overhead.

---

## 🏛️ System Architecture Overview

```
[Tenant Admin Portal]
        │  PUT /api/tenants/{id} (or branding action)
        ▼
[AlphaZero.Modules.Tenants]
  ├── Value Object: TenantBranding (PrimaryColor, SecondaryColor, LogoUrl, DarkModeLogoUrl, FaviconUrl)
  ├── Domain Validation: Regex ^#[0-9A-Fa-f]{6}$, Contrast safety checks
  ├── Domain Event: TenantBrandingUpdatedDomainEvent
  └── Lookup Endpoint: GET /api/tenants/lookup?subdomain=... (Edge/CDN Cached)
        │
        ▼ (Webhook / Cache Invalidation: revalidateTag("tenant-{subdomain}"))
[Next.js App Router (SSR)]
  ├── Edge Middleware: Resolves host/subdomain -> Injects x-tenant-subdomain header
  ├── Root Layout (SSR): Calls GET /api/tenants/lookup with tag cache
  ├── color-utils.ts: Derives luminance, foreground text contrast, and active tints
  └── HTML <head>: Injects inline <style id="tenant-theme"> with CSS variables
        │
        ▼
[Browser DOM Hydration]
  └── ZERO FOUC: Tailwind v4 @theme inline binds --primary & --secondary instantly
```

---

## 📋 Master Task Breakdown

### Phase 1: Backend Domain & Persistence (AlphaZero.Modules.Tenants)

- [x] **Task 1.1: Create `TenantBranding` Value Object**
  - **Path:** `src/alphazero-api/Modules/Tenants/Domain/TenantBranding.cs`
  - **Details:**
    - Immutable record containing:
      - `string PrimaryColor` (Default: `#2563eb`)
      - `string? SecondaryColor`
      - `string? LogoUrl`
      - `string? DarkModeLogoUrl`
      - `string? FaviconUrl`
    - Domain factory method `Create(...)` enforcing hex format (`^#([0-9A-Fa-f]{6})$`).
    - Returns `ErrorOr<TenantBranding>` with domain error `Branding.InvalidHexFormat`.
  - **Acceptance Criteria:** Unit tests prove invalid hexes are rejected and defaults are populated.

- [x] **Task 1.2: Refactor `Tenant.cs` Aggregate Root**
  - **Path:** `src/alphazero-api/Modules/Tenants/Domain/Tenant.cs`
  - **Details:**
    - Replace loose properties with `public TenantBranding Branding { get; private set; }`.
    - Add method: `public ErrorOr<Success> UpdateBranding(TenantBranding branding)`.
    - Emit `TenantBrandingUpdatedDomainEvent(Guid TenantId, string Subdomain, TenantBranding Branding)`.
  - **Acceptance Criteria:** Entity encapsulates branding changes and records domain events.

- [x] **Task 1.3: EF Core Entity Configuration & Migration**
  - **Path:** `src/alphazero-api/Modules/Tenants/Infrastructure/Persistence/Configurations/TenantConfiguration.cs`
  - **Details:**
    - Map `TenantBranding` as an owned entity (`builder.OwnsOne(x => x.Branding)`) with dedicated columns (`PrimaryColor`, `SecondaryColor`, `LogoUrl`, `DarkModeLogoUrl`, `FaviconUrl`).
    - Generate EF Core database migration for PostgreSQL: `20260908095736_AddTenantBranding`.
  - **Acceptance Criteria:** Migration applies cleanly; round-trip repository tests pass.

---

### Phase 2: Application Commands & Public Queries

- [x] **Task 2.1: Update `UpdateTenantCommand` & Handler**
  - **Path:** `src/alphazero-api/Modules/Tenants/Application/Tenants/Commands/UpdateTenant/`
  - **Details:**
    - Update `UpdateTenantCommand` parameters:
      `Guid Id, string Name, string? PrimaryColor, string? SecondaryColor, string? LogoUrl, string? DarkModeLogoUrl, string? FaviconUrl`.
    - Add FluentValidation rules in `UpdateTenantCommandValidator` verifying hex regex and URL limits.
    - Handler loads tenant, calls `UpdateBranding`, persists changes, and logs event.
  - **Acceptance Criteria:** Integration tests verify updating branding successfully persists to DB.

- [x] **Task 2.2: Upgrade `LookupTenantEndpoint` & Response DTO**
  - **Path:** `src/alphazero-api/Modules/Tenants/Presentation/Endpoints/LookupTenant/`
  - **Details:**
    - Update `LookupTenantBranding` record:
      `public record LookupTenantBranding(string PrimaryColor, string? SecondaryColor, string? LogoUrl, string? DarkModeLogoUrl, string? FaviconUrl);`
    - Ensure endpoint returns HTTP 200 with appropriate `Cache-Control` header:
      `public, max-age=300, s-maxage=86400, stale-while-revalidate=86400`.
  - **Acceptance Criteria:** Swagger docs updated via FastEndpoints summary; cached endpoint returns full branding payload.

- [x] **Task 2.3: In-Memory Domain Event Consumer for Cache Invalidation**
  - **Path:** `src/alphazero-api/Modules/Tenants/Infrastructure/Consumers/`
  - **Details:**
    - Register MassTransit In-Memory consumer for `TenantBrandingUpdatedDomainEvent`.
    - Invalidate Redis/HybridCache tenant cache entry `tenant:subdomain:{subdomain}`.
    - Fire HTTP webhook to Next.js API route `/api/revalidate?tag=tenant-{subdomain}`.
  - **Acceptance Criteria:** Branding update triggers immediate purge of edge and Next.js ISR caches.

---

### Phase 3: Frontend Theming Engine (@repo/design-system)

- [x] **Task 3.1: Create Algorithmic Color & Contrast Utility**
  - **Path:** `src/alphazero-frontend/packages/design-system/lib/color-utils.ts`
  - **Details:**
    - Implement WCAG 2.1 relative luminance calculation: `getLuminance(hex: string): number`.
    - Calculate contrast ratio: `getContrastRatio(fgHex: string, bgHex: string): number`.
    - Implement `generateTenantCssVariables(primaryHex: string, secondaryHex?: string | null): string`:
      - Primary text: if `luminance(primary) > 0.4` -> `#09090b` else `#ffffff`.
      - Secondary text: if `luminance(secondary) > 0.4` -> `#09090b` else `#ffffff`.
      - Injects `--primary`, `--primary-foreground`, `--ring`, `--secondary`, `--secondary-foreground`, `--color-precision-blue`, `--color-blue-subtle`.
  - **Acceptance Criteria:** Unit tests in vitest verifying contrast ratio decisions for yellow, navy, crimson, and black.

- [x] **Task 3.2: Verify Tailwind v4 Theme Tokens**
  - **Path:** `src/alphazero-frontend/packages/design-system/styles/globals.css`
  - **Details:**
    - Ensure `@theme inline` cleanly references `var(--primary)`, `var(--primary-foreground)`, `var(--secondary)`, `var(--secondary-foreground)`, and `var(--ring)`.
    - Verify dark mode (`.dark`) surfaces keep pure neutral Zinc foundations as specified in `DESIGN.md`.
  - **Acceptance Criteria:** Changing `:root` CSS variables dynamically updates all button, badge, and card styles without FOUC.

---

### Phase 4: Next.js SSR Integration (apps/app & apps/web)

- [x] **Task 4.1: Edge Subdomain Resolution Middleware**
  - **Path:** `src/alphazero-frontend/apps/app/proxy.ts`
  - **Details:**
    - Read `host` and `x-forwarded-host` header from `NextRequest`.
    - Extract tenant subdomain (e.g. `harvard.alphazero.academy` -> `harvard`).
    - Rewrite or pass forward custom request header: `x-tenant-subdomain: harvard`.
  - **Acceptance Criteria:** Request headers carry parsed tenant subdomain into App Router Server Components.

- [x] **Task 4.2: Dynamic Theme Injection in Root Layout**
  - **Path:** `src/alphazero-frontend/apps/app/app/layout.tsx`
  - **Details:**
    - Read `x-tenant-subdomain` from `headers()`.
    - Fetch tenant branding via cached `fetch(.../tenants/lookup?subdomain=...)` with `next: { tags: ['tenant-' + subdomain] }`.
    - Compute dynamic CSS string using `generateTenantCssVariables`.
    - Render critical inline style tag in HTML head: `<style id="tenant-theme">{dynamicCss}</style>`.
  - **Acceptance Criteria:** Browser network audit proves zero FOUC and zero external CSS download overhead.

- [x] **Task 4.3: Add Revalidation API Route**
  - **Path:** `src/alphazero-frontend/apps/app/app/api/revalidate/route.ts`
  - **Details:**
    - Authenticate shared webhook secret.
    - Extract `tag` query param (e.g. `tenant-harvard`).
    - Call `revalidateTag(tag)` to purge Next.js server cache instantly upon backend branding save.
  - **Acceptance Criteria:** Calling revalidation webhook immediately reflects new colors on next page refresh.

---

### Phase 5: Tenant Admin Branding Management UI

- [x] **Task 5.1: Build Admin Theme Configuration Page**
  - **Path:** `src/alphazero-frontend/apps/app/app/(authenticated)/settings/branding/page.tsx`
  - **Details:**
    - Form fields:
      - Academy Name
      - Primary Brand Color (Color picker + Hex input + Presets)
      - Secondary Accent Color (Color picker + Hex input + Presets)
      - Logo Upload / Dark Mode Logo Upload / Favicon URL
    - Real-time Interactive Sandbox Component:
      - Course Card with Syrian/MENA Bitmask progress bar
      - Primary CTA Button ("Enroll in Course")
      - Secondary Action Button ("Preview Syllabus")
      - Physical Library Voucher Redemption Card (`AZ-9482-K92X`)
      - Dark Mode / Light Mode preview toggle
    - Live WCAG Contrast Indicator:
      - Computes real-time score against white and dark backgrounds.
      - Displays badge: `[WCAG AA Pass: 5.4:1]` or `[Low Contrast: Text automatically set to Dark]`.
    - Reset to Default Button (restores `#2563eb` AlphaZero Precision Blue).
  - **Acceptance Criteria:** Tenant admins can interactively visualize their brand before committing changes.

---

## 🧪 Testing & Verification Strategy

1. **Unit Tests (.NET):**
   - `TenantBrandingTests.cs`: Validates hex regex parsing, defaults, and immutability. [Passed - 8 tests]
   - `TenantAggregateTests.cs`: Validates `UpdateBranding` raises domain events. [Passed - 3 tests]
   - `CreateTenantCommandHandlerTests.cs`: Validates creation invariants and branding. [Passed - 3 tests]
   - `UpdateTenantCommandHandlerTests.cs`: Validates updating branding persists to DB. [Passed - 3 tests]
   - `GetTenantBySubdomainQueryHandlerTests.cs`: Validates subdomain branding resolution. [Passed - 2 tests]
   - `TenantBrandingUpdatedConsumerTests.cs`: Validates cache invalidation and webhook dispatch. [Passed - 2 tests]
2. **Integration & API Verification (.NET):**
   - `LookupTenantEndpoint`: Public lookup returns both primary and secondary colors with HTTP 200 and edge `Cache-Control` header `public, max-age=300, s-maxage=86400, stale-while-revalidate=86400`.
   - `UpdateTenantEndpoint`: Validates and saves dynamic branding.
3. **Frontend Tests (Vitest):**
   - `color-utils.test.ts`: Tests relative luminance and contrast ratio calculation across edge-case colors:
     - Pure White (`#FFFFFF`) -> Primary Foreground must be `#09090b` [Passed]
     - Neon Yellow (`#FFFF00`) -> Primary Foreground must be `#09090b` [Passed]
     - Deep Navy (`#0F172A`) -> Primary Foreground must be `#FFFFFF` [Passed]
     - Crimson (`#A51C30`) -> Primary Foreground must be `#FFFFFF` [Passed]
     - Precision Blue (`#2563eb`) -> Primary Foreground must be `#FFFFFF` [Passed]
     - Inline CSS size validation: ~200-400 bytes [Passed]
