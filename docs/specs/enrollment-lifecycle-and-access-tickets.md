# Enrollment Lifecycle, Audit Entries & Access Tickets Design

## 1. Overview & Context

This document describes the as-built architecture for course enrollment lifecycle management, transition auditing, capability-based access-window evaluation, and set-based worker processes implemented for issue #34 (plan: `spec-34/plans/enrollment-lifecycle-rework.plan.md`).

---

## 2. Enrollment Audit Table Convention

### Principles
- Transition who/when metadata lives in an append-only audit table (`"Courses"."EnrollmentAuditEntries"`), keeping the `Enrollment` aggregate free of read-boundary audit plumbing.
- Generic columns: `Id, TenantId, SubjectId, EventKind, ActorId, OccurredOn, Metadata (jsonb)`.
- **In-process writer**: MediatR domain event handlers stage `EnrollmentAuditEntry` rows on the shared scoped `AppDbContext`. The UnitOfWork publishes notification handlers before its single `SaveChangesAsync`, ensuring audit entries commit atomically with aggregate state changes in the same transaction (no bus or outbox dependency on write).
- **Projections**: Read queries (such as `GetEnrollment` and enrollment listings) query the latest audit entry per enrollment via correlated subqueries (`(SubjectId, OccurredOn DESC)` seek) to surface honest `LastStatusChangedOn` and `LastStatusChangedBy` fields without polluting aggregate domain state.

---

## 3. Access Tickets & Policy Operator Architecture

### Principal-per-(Course, Plan)
- When a course with a windowed plan (`AccessWindowDays != null`) is created or updated, Identity creates or ensures a dedicated **role principal** for that `(courseId, planId)` with a **managed policy**.
- The managed policy statement grants `courses:View` and `courses:Read` scoped to the course ARN, gated by the custom `AccessTicketValid` condition operator.
- Enrollments in windowed plans are assigned this per-plan principal; unlimited-window enrollments retain the shared student template (`...100000000002`).

### Access Ticket Minting (The Stateful Gate)
- Endpoint: `POST /courses/{courseId}/access-tickets`
- Protected by FastEndpoints IAM check: `AccessControl("courses:View", ...)`
- Stateful checks against the database:
  - If the student's enrollment is not found: `404 NotFound`
  - If the access window has already elapsed: marks the enrollment `Expired` (lazy expiry path b) and returns `409 Conflict` (`Enrollment.AccessWindowElapsed`)
  - If the enrollment is `Suspended` or `Expired`: returns `409 Conflict`
  - If the enrollment is `Active`: mints an HMAC-SHA256 capability ticket with dedicated secret (`AccessTicket:Secret`) and configurable TTL (~2 hours), containing `(tid, uid, eid, exp, iat, windowStart, windowEnd)`.

### Stateless Edge Evaluation (`AccessTicketValid` Operator)
- Traverses the `X-Access-Ticket` HTTP header on content requests.
- Evaluator runs in Identity without database queries:
  - Validates HMAC signature and ticket expiration (`exp`).
  - Verifies actor binding (`uid == context.Id`, `tid == context.TenantId`).
  - Evaluates whether current time falls within `[windowStart, windowEnd]`.
- If the ticket window is elapsed, raises `PrincipalExpiredIntegrationEvent` (Identity's first cross-module event), consumed idempotently by Courses to transition the enrollment to `Expired`.

---

## 4. Triple Expiry Architecture

1. **Path A (Condition-driven):** The `AccessTicketValid` operator detects an expired window during request authorization and publishes `PrincipalExpiredIntegrationEvent`. Courses consumes the event idempotently and transitions the enrollment via `MarkExpired()`.
2. **Path B (Stateful mint gate):** When a student requests a new ticket at `POST /courses/{courseId}/access-tickets`, Courses checks `AccessExpiresOn <= UtcNow`. If elapsed, it transitions the enrollment to `Expired` and rejects the mint request with `409 Conflict`.
3. **Path C (Set-based sweep backstop):** A scheduled background worker (`EnrollmentExpiryWorker`) runs a single atomic PostgreSQL UPDATE with an SQL-encoded guard (`Status = 'Active' AND "AccessExpiresOn" <= @now RETURNING ...`) that updates enrollments, inserts audit rows via CTE, and bulk-publishes `EnrollmentExpiredIntegrationEvent`s without aggregate loads or N+1 queries.

---

## 5. Derived-from-Source Course Analytics

- Running counters are refreshed by a scheduled daily worker (`CourseAnalyticsRefreshWorker`) via a single set-based SQL UPSERT.
- Query filters `Status != 'Expired'` (Active, Suspended, Inactive students are included; Expired students are excluded).
- Per-row analytics decrements (`DecrementExpiredEnrollmentAsync`) are completely deleted, eliminating hundreds of round trips per expiry batch and preventing counter drift.
