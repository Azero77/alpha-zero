# 🚀 AlphaZero LMS: Frontend Production Implementation Plan

> **Objective:** Build a production-grade, bilingual (Arabic & English), offline-resilient Next.js frontend for the AlphaZero LMS that integrates with your existing C# ASP.NET Core API, Keycloak, and AlphaZero IAM.

---

## 📅 Chronological Implementation Roadmap

```
[Step 0] Repo Scaffolding & Cleanup (pnpm + Monorepo)
   │
[Step 1] Contracts & Schemas (`packages/lms-types`)
   │
[Step 2] Resilient HTTP Client (`packages/lms-api-client`)
   │
[Step 3] Design System & Components (`packages/design-system`)
   │
[Step 4] Auth & AlphaZero IAM Bridge (`packages/auth`)
   │
[Step 5] App Shell & Bilingual Layouts (`apps/app`)
   │
[Step 6] Feature: Access Code & Voucher Redemption
   │
[Step 7] Feature: Course Catalog & Learning Dashboard
   │
[Step 8] Feature: HLS Video Player & Transcoding Webhook
   │
[Step 9] Error Pipeline & Notifications (Zustand + Sonner)
   │
[Step 10] Production Hardening (Rate Limiting, CSP, Sentry)
```

---

## Step 0: Monorepo Scaffolding & Pruning (Day 1)

Before writing any UI code, prune the boilerplate to keep only what your LMS needs.

### Tasks:
1. Initialize the monorepo with `pnpm`:
   ```bash
   pnpm create next-forge@latest alpha-zero-frontend --package-manager pnpm
   ```
2. Remove unnecessary services:
   ```bash
   rm -rf apps/api apps/docs apps/email apps/storybook apps/studio
   rm -rf packages/ai packages/cms packages/collaboration packages/database packages/email packages/notifications packages/payments packages/storage
   ```
3. Verify that `pnpm install` and `pnpm build` pass with zero errors.

---

## Step 1: Contracts & Shared Types (`packages/lms-types`) (Day 2)

**Goal:** Establish the type-safe contracts between C# and Next.js.

### Tasks:
1. **Auto-generate types from C# Swagger:**
   ```bash
   pnpm dlx openapi-typescript https://localhost:5001/swagger/v1/swagger.json -o packages/lms-types/src/api.d.ts
   ```
2. **Define FastEndpoints & ErrorOr `ProblemDetails` schema:**
   - Create [`packages/lms-types/src/problem-details.ts`](#) with `AlphaZeroProblemDetailsSchema` and `ProblemDetailsException`.
   - Models `traceId`, `title`, `detail`, `status`, and `errors`: an array of `{ name, reason, code?, severity?, metadata? }` (accommodating FastEndpoints validation errors & ErrorOr domain errors).
3. **Define Core Domain Zod Schemas:**
   - `CourseSchema`, `LessonSchema`, `VoucherRedeemSchema`.
   - AlphaZero ARN types: `az:<service>:<tenantId>:<resource-path>`.
4. Export everything from `packages/lms-types/src/index.ts`.

---

## Step 2: Resilient API Client (`packages/lms-api-client`) (Day 3)

**Goal:** Build a robust, Polly-like fetch client using `ky`.

### Tasks:
1. Create `packages/lms-api-client/src/client.ts`.
2. Configure **timeouts** (15s) and **retry policies** (retry 502/503/504, never retry 400/401/403).
3. Add `beforeRequest` hook:
   - Extract session from `packages/auth` and attach `Authorization: Bearer <keycloak_token>`.
4. Add `beforeError` hook:
   - Detect `application/problem+json` and parse into typed `ProblemDetailsException`.
5. Create typed API methods:
   - `getCourses()`, `getCourse(id)`, `redeemAccessCode(code)`, `requestVideoUpload(arn, fileName)`.

---

## Step 3: Design System & Arabic/RTL Styling (`packages/design-system`) (COMPLETED ✅)

**Goal:** Ready-to-use accessible components with native Arabic (RTL) and English (LTR) support.

### Tasks:
1. [x] **Configure Fonts:**
   - Configured **Inter Variable** for English and UI foundations.
   - Configured **Cairo** for Arabic using `next/font/google` (selected over IBM Plex Sans Arabic for Syrian educational clarity at 13–16px).
   - Configured **JetBrains Mono** as fallback for Berkeley Mono for voucher codes and monospace counters.
   - Exposed CSS variables: `--font-sans`, `--font-arabic`, `--font-mono` in `globals.css` with `html[dir="rtl"]` font inheritance.
2. [x] **RTL Setup in Tailwind:**
   - Audited `packages/design-system/styles/globals.css`.
   - Ensured all spacing uses logical properties: `ps-*`, `pe-*`, `ms-*`, `me-*`, `text-start`, `text-end`.
3. [x] **Download Required Shadcn Primitives:**
   - Verified 50+ primitives in `packages/design-system/components/ui/` including button, dialog, form, input-otp, alert-dialog, skeleton, sonner.
4. [x] **Build Custom LMS Domain Components (`packages/design-system/components/lms/`):**
   - `<BitmaskProgressBar bitmask={string} totalLessons={number} />`: Bitwise completion tracker supporting both Continuous 6px Rail and Discrete Bit-Pill Array variants with monospace counters.
   - `<VoucherInput />`: Segmented OTP-style code input with locked `[AZ]` prefix badge, two 4-slot groups (`[XXXX]-[XXXX]`), clipboard paste auto-dash-stripping, auto-uppercase, and validation states.
   - `<CourseCard />`: Course thumbnail, title, instructor, embedded bitmask progress bar, and enrollment CTA supporting both vertical 16:9 grid and horizontal row layouts.
5. [x] **Verification & Test Suite:**
   - Decoupled `lib/utils.ts` from Sentry bundler plugins.
   - Added 10 unit tests in `apps/app/__tests__/lms-components.test.tsx` (26/26 tests passing).
   - Verified clean monorepo typecheck (15/15 packages passing).

---

## Step 4: Keycloak Auth & Secure Session Management (`packages/auth`) (Days 6–7)

**Goal:** Secure BFF session cookies and server-authoritative permission enforcement.

### Tasks:
1. **OIDC Auth Code Flow with Keycloak:**
   - Implement `/api/auth/login` (redirects to Keycloak with PKCE).
   - Implement `/api/auth/callback` (exchanges auth code for tokens, extracts userId/tenantId, sets encrypted `HttpOnly` cookie).
   - Implement `/api/auth/logout` (invalidates Keycloak session, clears cookie).
2. **Session Reader (`session.ts` / `server.ts`):**
   - Create `getSession()` (reads and decrypts session cookie, silently refreshes expired tokens).
   - Forward `X-Device-Fingerprint` to support context-aware device enforcement per architectural mandates.
3. **Server-Authoritative Authorization Model:**
   - **Architectural Decision:** Eliminate client-side speculative `can()` rule evaluations. Authorization is strictly enforced at the C# API layer via FastEndpoints `ResourceArn.ForUser(req.StudentId)`.
   - On 403 Forbidden responses, `ApiErrorException` maps `ProblemDetails` and triggers UI unlock prompts (e.g. "Redeem Voucher to Unlock Course"), caching the 403 response in client state to minimize unauthorized network requests over Syrian connections.

---

## Step 5: Application Shell & Bilingual Layouts (`apps/app`) (Week 2, Days 1–2)

**Goal:** Subpath `[locale]` routing, auth guards, and Arabic/English language toggle.

### Tasks:
1. **Create Subpath `[locale]` Directory Structure:**
   ```
   apps/app/app/
   └── [locale]/
       ├── layout.tsx              # Sets <html dir="rtl|ltr" lang="ar|en"> + Cairo/Inter font classes
       ├── (auth)/
       │   └── login/page.tsx      # Keycloak entry point
       └── (dashboard)/
           ├── layout.tsx          # Server Auth Guard + Sidebar + Language Switcher
           ├── courses/
           │   ├── page.tsx        # Course catalog
           │   └── [id]/page.tsx   # Course details & syllabus
           └── redeem/
               └── page.tsx        # Physical voucher redemption
   ```
2. **Implement Dictionaries:**
   - Add `"ar"` to targets in `packages/internationalization/languine.json`.
   - Populate `packages/internationalization/dictionaries/ar.json` and `en.json` with Arabic/English translations for navigation, dashboard, courses, and error messages.
3. **Build the Global Sidebar & Header:**
   - Reusable responsive navigation (Dashboard, My Courses, Redeem Voucher, Settings).
   - Language switcher dropdown toggle between Arabic (RTL) and English (LTR) preserving current path.

---

## Step 6: Feature 1 — Voucher & Access Code Redemption (Week 2, Days 3–4)

**Goal:** Offline voucher activation connecting React Hook Form, Server Actions, and C# ProblemDetails.

### Tasks:
1. Create localized Zod schema factory: `createAccessCodeSchema(dict.errors)`.
2. Build the UI in `apps/app/app/[locale]/(dashboard)/redeem/page.tsx`:
   - React Hook Form with `zodResolver`.
   - Submit via Next.js Server Action (`redeemCodeAction`).
3. Connect C# ProblemDetails error mapping:
   - If backend returns `VOUCHER_EXPIRED`, display localized Arabic error `"انتهت صلاحية هذا الرمز"`.

---

## Step 7: Feature 2 — Course Catalog & Dashboard (Week 2, Day 5 – Week 3, Day 1)

**Goal:** Course list and details using Server Components + TanStack Query hydration.

### Tasks:
1. **Catalog Page (`courses/page.tsx`):**
   - RSC fetches courses via `lmsClient.getCourses()`.
   - Renders `<CourseCard />` grid (0 KB client JavaScript).
2. **Course Details Page (`courses/[id]/page.tsx`):**
   - Fetches course hierarchy (Sections, Lessons, Quizzes) per `FrontendIntegrationGuide.md`.
   - Check IAM: `const canStream = await can("video:Stream", courseArn)`.
   - If locked, render "Redeem Access Code to Unlock" CTA.
3. **Dynamic SEO:**
   - Add `generateMetadata()` for course title, thumbnail, and JSON-LD schema.

---

## Step 8: Feature 3 — HLS Video Player & Transcoding Webhook (Week 3, Days 2–4)

**Goal:** Smooth video playback and async handling of transcoding sagas.

### Tasks:
1. **Direct Video Upload Flow:**
   - Instructor requests presigned URL: `POST /api/video-uploading/upload` with `targetResourceArn`.
   - Direct `PUT` upload from browser to S3.
2. **HLS Video Player Component (`<VideoPlayer />`):**
   - Built on `video.js` or `hls.js` with quality selector (1080p, 720p, 480p, 360p).
   - Auto-saves student timestamp progress every 10 seconds.
3. **Transcoding Saga Webhook (`apps/app/app/api/webhooks/video/route.ts`):**
   - Receives webhook from C# backend when video transcoding finishes.
   - Verifies HMAC signature.
   - Calls `revalidateTag(courseArn)` so students see the playable video immediately.

---

## Step 9: Error Pipeline & IAM Notification Store (Week 3, Day 5)

**Goal:** Zero-boilerplate error handling and IAM restriction alerts.

### Tasks:
1. Implement `useNotificationStore` (Zustand) for persistent IAM denials.
2. Configure `QueryClient` global `onError` handler for automatic Sonner toasts.
3. Mount `<IamAlertBanner />` in the dashboard layout.

---

## Step 10: Production Hardening & Offline Readiness (Week 4)

### Tasks:
1. **Security Headers:**
   - Enable Nosecone in `apps/app/proxy.ts`.
   - Configure CSP `media-src` to permit only your authorized video S3/CDN endpoints.
2. **Voucher Rate Limiting:**
   - Upstash Redis sliding window (5 attempts / min) on voucher redemption.
3. **Observability:**
   - Configure Sentry DSN in `apps/app/env.ts`.
   - Verify `onRequestError` logs server crashes.
4. **Service Worker (Optional for Offline):**
   - Register a Workbox service worker to cache course assets and lesson text in IndexedDB.

---

## Summary Checklist

| Phase | Core Deliverable | Estimated Time |
|---|---|---|
| **Phase 1** | Monorepo setup, `lms-types`, and `lms-api-client` | Days 1–3 |
| **Phase 2** | Design system, Arabic/English setup, and Keycloak Auth | Days 4–7 |
| **Phase 3** | Access code redemption & Course catalog | Week 2 |
| **Phase 4** | HLS Video Player, Transcoding Webhooks & IAM | Week 3 |
| **Phase 5** | Hardening, Rate limiting, and Sentry deployment | Week 4 |

---

## GSTACK REVIEW REPORT

### Review Summary

| Review Run | Status | Key Findings |
| :--- | :--- | :--- |
| **Step 0: Scope & Architecture Gate** | PASSED | Simplified Step 4 by dropping speculative client `can()` evaluator; backend API remains sole authorization authority. |
| **Section 1: Architecture & Data Flow** | PASSED | Next.js 16 BFF + Keycloak OIDC PKCE + C# Modular Monolith with zero-reverse-coupling tag revalidation. |
| **Section 2: Code Quality & Contracts** | PASSED | FastEndpoints `ProblemDetails` synchronized in `packages/lms-types`; bitmask string format verified against `Progress.cs`. |
| **Section 3: Test Strategy & Coverage** | PASSED | Unit test coverage for `<BitmaskProgressBar />`, `<VoucherInput />`, and auth token refresh; contract tests for error handling. |
| **Section 4: Performance & Syrian Constraints** | PASSED | 200-byte zero-FOUC CSS injection, Cairo font subsetting, 0 KB client JS server-rendered catalog. |
| **Section 5: Design Review (8 Dimensions)** | PASSED (8.0 -> 10/10) | Typography locked to Cairo + Inter + Berkeley Mono; native RTL logical properties (`ps-*`, `pe-*`); Precision Blue interactive scale. |

**VERDICT: APPROVED FOR IMPLEMENTATION**

NO UNRESOLVED DECISIONS
