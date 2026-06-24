# Backend MVP Gaps & TODOs

Based on the `MVP-Score.md`, `PRD.md`, and the upcoming Web (Next.js) & Mobile (React Native) implementation, here are the architectural gaps identified in the current backend endpoints. These must be addressed to unblock the frontend and mobile development.

---

## 1. Video Progress Synchronization
- **Gap:** We currently have a `CompleteItemEndpoint`, which marks an item as "Done" (fulfilling the Bitmasking/`VARBIT` requirement for storage efficiency). However, we lack granular tracking for "Video Last Watched Position".
- **Why it is needed:** 
  - *Frontend/Mobile UI:* When a student goes to their dashboard, they need to see a "Resume Lesson" button. When they click it, the video player should automatically jump to `05:23` instead of restarting.
  - *Network Stability:* In Syrian/MENA internet conditions, connections drop frequently. The player needs to sync the timestamp every ~10-15 seconds in the background so progress isn't lost if the internet cuts out.
- **Action:** 
  - Create `UpdateVideoProgressEndpoint` (POST `/api/courses/enrollments/progress`): Receives `{ "lessonId": "uuid", "lastWatchedSecond": 323 }`.
  - Create `GetVideoProgressEndpoint` (GET `/api/courses/enrollments/progress/{lessonId}`): Returns the last synced second.

## 2. Teacher Analytics & Cohort Tracking (MVP Requirement)
- **Gap:** MVP Phase 1 & 2 states: *"the ability for teachers to track their students progress and see his courses statistics."* Currently, we only have student-facing `GetStudentDashboard` and general `ListCourses` endpoints.
- **Why it is needed:** 
  - *Frontend UI:* Teachers need a dedicated dashboard to see which students are falling behind or where drop-offs are happening in a video.
- **Action:** 
  - Create `GetCourseAnalyticsEndpoint` (GET `/api/courses/{courseId}/analytics`): Should return aggregated data like total enrollments, average completion rate, and completion bitmasks decoded into percentages.
  - Create `ListStudentProgressEndpoint` (GET `/api/courses/{courseId}/students`): Lists students in the course and their individual progress percentage.

## 3. Student Registration / Onboarding (Self-Serve)
- **Gap:** The system has `JoinTenant` (assuming they are already a user) and `RegisterTenantUser` (a command, but potentially missing a public endpoint).
- **Why it is needed:** 
  - *Auth Flow:* A new student buys a physical library code. They go to `oxford.alphazero.com`. Before they can redeem the code, they need to create an account. They must be able to hit a public registration endpoint.
  - *Device Locking:* At the exact moment of registration, the frontend/mobile app will send their unique `DeviceFingerprint`.
- **Action:** 
  - Expose a public `RegisterStudentEndpoint` (POST `/api/identity/register`): Accepts `{ "tenantId": "uuid", "email": "...", "password": "...", "deviceFingerprint": "..." }`.

## 4. Tenant Context & Unauthenticated Branding
- **Gap:** The `LookupTenantEndpoint` exists, but we must ensure it serves data *without* requiring an Authorization token.
- **Why it is needed:** 
  - *Frontend Web (Next.js Middleware):* When a user visits `school.alphazero.com`, the Next.js server needs to fetch the school's colors, logo, and name *before* the page renders, and *before* the user logs in.
- **Action:** 
  - Ensure `LookupTenantEndpoint` is marked with `[AllowAnonymous]`.
  - Ensure the response payload explicitly includes the branding object:
    ```json
    {
      "id": "uuid",
      "subdomain": "school",
      "name": "Oxford Academy",
      "branding": {
        "primaryColor": "#1A73E8",
        "logoUrl": "https://s3.../logo.png"
      }
    }
    ```
