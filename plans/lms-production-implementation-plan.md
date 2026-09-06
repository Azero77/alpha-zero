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

## Step 3: Design System & Arabic/RTL Styling (`packages/design-system`) (Days 4–5)

**Goal:** Ready-to-use accessible components with native Arabic (RTL) and English (LTR) support.

### Tasks:
1. **Configure Fonts:**
   - Configure **Geist / Inter** for English.
   - Configure **IBM Plex Sans Arabic** or **Cairo** for Arabic using `next/font/google`.
2. **RTL Setup in Tailwind:**
   - Audit `packages/design-system/styles/globals.css`.
   - Ensure all spacing uses logical properties: `ps-*` (padding-inline-start), `pe-*` (padding-inline-end), `text-start`.
3. **Download Required Shadcn Primitives:**
   ```bash
   pnpm dlx shadcn@latest add button dialog form input alert-dialog dropdown-menu skeleton sonner -c packages/design-system
   ```
4. **Build Custom LMS Domain Components:**
   - `<CourseCard />`: Thumbnail, progress bar, lesson count.
   - `<VoucherInput />`: OTP-style 4x4 segmented code input.
   - `<ProgressBar />`: Percentage completion tracker.

---

## Step 4: Keycloak Auth & AlphaZero IAM Bridge (`packages/auth`) (Days 6–7)

**Goal:** Secure BFF session cookies and server-side policy authorization.

### Tasks:
1. **OIDC Auth Code Flow with Keycloak:**
   - Implement `/api/auth/login` (redirects to Keycloak).
   - Implement `/api/auth/callback` (exchanges auth code for tokens, sets encrypted `HttpOnly` cookie).
   - Implement `/api/auth/logout` (invalidates Keycloak session, clears cookie).
2. **Session Reader (`session.ts`):**
   - Create `getSession()` (reads and decrypts session cookie, silently refreshes expired tokens).
3. **AlphaZero IAM Evaluator (`authorize.ts`):**
   - Create `can(action, resourceArn)` helper:
     ```ts
     const allowed = await can("video:Stream", "az:courses:tenant1:course/math-101");
     ```
   - Caches decision on the server for 30s.

---

## Step 5: Application Shell & Bilingual Layouts (`apps/app`) (Week 2, Days 1–2)

**Goal:** Route groups, auth guards, and Arabic/English language toggle.

### Tasks:
1. Create directory structure:
   ```
   apps/app/app/
   └── [locale]/
       ├── layout.tsx              # Sets <html dir="rtl|ltr" lang="ar|en">
       ├── (auth)/
       │   └── login/page.tsx      # Keycloak entry point
       └── (dashboard)/
           ├── layout.tsx          # Server Auth Guard + Sidebar + IamAlertBanner
           ├── courses/
           └── redeem/
   ```
2. **Implement Dictionaries:**
   - Populate `packages/internationalization/dictionaries/ar.json` and `en.json`.
3. **Build the Global Sidebar:**
   - Reusable responsive navigation (Dashboard, My Courses, Redeem Voucher, Settings).
   - Dropdown to switch language between Arabic and English.

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
