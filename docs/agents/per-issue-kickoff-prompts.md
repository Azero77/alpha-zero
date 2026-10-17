# Spec-chain kickoff prompts (#29, #32–#35)

Per-issue prompts for the Courses spec chain. Master workflow: `docs/agents/per-issue-runbook.md`.
Chain state lives in the issues' native blocked-by edges: start an issue only when its
`blocked_by: 0` (verified before claiming). All PRs target **`CourseContentManagement`**; `main`
is frozen until the chain completes. `#30`, `#31` prompts already issued in conversation; they are
also derivable from the runbook §§1–3.

Worktree recipe (all issues, user-run):

```bash
git worktree add ../<name> -b spec/<n>-<slug> origin/CourseContentManagement
cd ../<name> && opencode     # fresh conversation, paste the issue's prompt
```

---

## #29 — P0 instructor/admin CRUD

```text
You are the implementation owner for exactly one issue in this repo:

  ISSUE: 29
  BRANCH: spec/29-crud-p0  (create it yourself: git switch -c spec/29-crud-p0 origin/CourseContentManagement)
  PR BASE: CourseContentManagement  (never main — main is frozen until the whole chain lands)

First global read: docs/agents/per-issue-runbook.md — follow its §1 kickoff steps, §2 workflow and
§3 addendum row for #29, adapted as below. Verify the issue's gate before claiming:
gh api repos/Azero77/alpha-zero/issues/29 --jq .issue_dependencies_summary.blocked_by   (must be 0)

1. CLAIM: gh issue edit 29 --add-assignee @me · then gh issue view 29 --comments.

2. Read the issue body + comments, docs/agents/issue-tracker.md, docs/agents/domain.md,
   docs/reports/courses-gap-report-v2-2026-10-16.md (§3 P0 table = your six work items), then
   the cited sources: Course aggregate (UpdateInformation is ORPHANED, not missing — reuse it),
   CourseSection (Update/Delete/Restore are internal — reach them via new public aggregate-root
   methods), Subject (has only Create — you add small Update + soft-delete methods with an
   in-use guard), Enrollement/Enrollment deactivate logic, and the existing tenant-scoped
   student-enrollments query (dead code today — expose it, don't rewrite it).

3. BASELINE: build + full test suite in the worktree; report any pre-existing red before coding.

4. Plan comment on the issue (per runbook §1 step 4): the 8 routes, reuse decisions, validators
   to add, and any spec/code drift found. Then implement immediately.

5. IMPLEMENT, per the spec's Implementation Decisions: PUT /courses/{id} (UpdateInformation, no
   status guard); PUT + DELETE section routes (delete = soft delete via new aggregate method;
   item bits stay stable, Progress active-item recalculation drives the new percentage — the
   existing BitmaskStabilityTests guarantee this, extend one behavioral test to prove it end to
   end); PATCH /courses/{id}/archive (existing ArchiveCourseCommand finally gets an endpoint);
   DELETE /courses/{id} via new explicit Course Delete/Restore (the persistence interceptor +
   global query filters already exist and are integration-tested); PUT/DELETE subjects with
   409 Subject.InUse when a live course references the subject; POST
   /courses/enrollments/{id}/deactivate — NEW id-keyed command wrapping the existing deactivate
   logic, IDEMPOTENT (already-inactive returns success), latency: the saga's own studentId/
   courseId command stays untouched; GET /courses/enrollments?StudentId=...&Page&PerPage exposing
   the existing tenant-scoped query. Route vocabulary: "enrollments" (correct spelling), never
   the legacy one. Every new command gets an application-boundary validator per the module's
   established validator pattern. Every endpoint uses the module's AccessControl resource-ARN
   permission pattern (courses:Edit for writes, view-scope for the listing).

6. TESTS per the spec's Testing Decisions: integration tests per resource action over the
   Courses.Tests.Integration host (success, validation failure, permission denial, not-found,
   cross-tenant isolation; subject-delete in-use refusal; deactivate idempotency + progress
   retention; enrollments listing paging) modeled on the existing SoftDeleteTests /
   EnrollmentTests prior art; NSubstitute+FluentAssertions unit tests for new command handlers;
   domain unit tests for Course Delete / section delete via root / Subject Update+guard,
   following the aggregate test families.

7. REVIEW: full suite green; enumerate every issue user story as implemented-with-test or
   explicitly-deferred; diff hygiene pass.

8. SHIP: push, open PR against CourseContentManagement (never main), body starts "Closes #29",
   then summary + test-evidence bullets. Compact final report per runbook.
```

---

## #32 — Student catalog + course-detail view + completion event

```text
You are the implementation owner for exactly one issue in this repo:

  ISSUE: 32
  BRANCH: spec/32-student-ux  (git switch -c spec/32-student-ux origin/CourseContentManagement)
  PR BASE: CourseContentManagement  (never main)

First global read: docs/agents/per-issue-runbook.md — §1 kickoff, §2 workflow, §3 addendum row
for #32. Verify the gate first: blocked_by on #32 must be 0, then CLAIM the issue.

1. Context list: issue body + comments; docs/agents/*; docs/reports/courses-gap-report-v2-2026-10-16.md
   (P1 items 7/8 + row 11); the code to study FIRST: the GetCourseOverview pre-request visibility
   family and its tests (draft/published/anonymous masking is the pattern you extend),
   GetStudentDashboard, the CoursePlan aggregate, and the existing item-completed integration
   event path (your completion event follows it exactly).

2. BASELINE: build + full suite; report pre-existing red.

3. Plan comment on the issue: the two new queries, DTO field lists, the event contract, and
   spec/code drift.

4. IMPLEMENT per spec:
   - GET /courses/catalog — NEW student query: keyword search (title/description), subject
     filter, published-status constraint (students only ever see Published), sort (newest/title),
     paging; rich summary DTO (cover image, subject NAME — join, not raw id, total duration,
     item counts, minimal plan/price, timestamps). This is a new command/query type, NOT an
     extension of the admin listing.
   - GET /courses/{id}/view — NEW student-shaped detail query with the overview pre-request
     visibility pattern; for enrolled students of that course, overlay enrollmentId, status,
     completion percentage on the curriculum tree; admins see the same shape with no enrollment.
     Resource-identity cleanup rides here: real resource ARN/id fields, DROP the always-empty
     legacy metadata bucket from the new shape entirely.
   - CourseCompletedDomainEvent raised inside the enrollment aggregate exactly when a
     completion attempt flips the course to "all active items complete" (use the existing
     IsAllComplete check); verified idempotent — re-completions and progress recomputations
     after a section deletion NEVER re-raise. A domain-event handler publishes the
     enrollment-course-completed integration event through the module's existing outbox
     conventions (mirror the ItemCompleted path).
   - DO NOT change shape or auth on existing list/detail/dashboard endpoints and never expose
     readiness internals or pool data in student shapes.

5. TESTS per the spec: integration — catalog happy path + filters + sort + tenant isolation +
   deleted-content exclusion + summary enrichment correctness; view × three families
   (anonymous prospect, enrolled student overlay, admin stallion overlay-behavior — see spec);
   completion-event domain unit tests (raise exactly once on transition; idempotency; the
   section-deletion interaction: deleting a section keeps an incomplete course incomplete).
   Only external behavior asserted.

6. REVIEW + SHIP: full suite green; user-story enumeration; PR against
   CourseContentManagement (never main), body starts "Closes #32" + evidence bullets.
```

---

## #33 — HybridCache on the read paths

```text
You are the implementation owner for exactly one issue in this repo:

  ISSUE: 33
  BRANCH: spec/33-caching  (git switch -c spec/33-caching origin/CourseContentManagement)
  PR BASE: CourseContentManagement  (never main)

First global read: docs/agents/per-issue-runbook.md — §1 kickoff, §2 workflow, §3 addendum row
for #33. Verify the gate first: blocked_by on #33 must be 0, then CLAIM the issue.

1. Context list: issue body + comments; docs/agents/*; the shared cache infrastructure
   (AddHybridCache + the caching repository decorator) and its EXISTING usage precedent in the
   Identity / VideoUploading / Tenants modules — imitate, do not invent. No new caching
   framework is in scope.

2. BASELINE: build + full suite (with the sibling-module precedent visible to you).

3. Plan comment on the issue BEFORE coding: your per-surface table (cache or not, TTL, key
   shape, eviction triggers) per the spec's Implementation Decisions.

4. IMPLEMENT per spec: cache ONLY — catalog queries (per tenant + filter version), the student
   course-view public shape (per course + visibility class, never sharing keys across
   visibility variants), readiness companion, tenant subject listing. Tenant id is part of
   every key (hard isolation requirement). Wired eviction (tag/include) on the mutation
   commands that invalidate those surfaces — course update/delete/archive, section/item
   mutations, overview upsert, state transitions — in the SAME PR as the cached reads
   (never reads without evictions). Explicitly NOT cached: enrollment checks in write paths,
   complete-item, enroll/redeem, state-transition commands, and the dashboard unless cheap
   tag invalidation by student id exists (default: don't). No schema changes.

5. TESTS per the spec: integration-first behavioral tests — first call fills, second call
   served from cache, and FRESHNESS: after each mutation, the next read within the TTL window
   returns the updated data (this is the acceptance bar). Assert tenant isolation
   (cross-tenant key reuse returns nothing of the other tenant's). Stampede behavior rides on
   HybridCache's own guarantee — do not write custom code for it. Behavior-only assertions.

6. REVIEW + SHIP: full suite green; user-story enumeration; PR against
   CourseContentManagement (never main), body starts "Closes #33" + evidence bullets.
```

---

## #34 — Enrollment lifecycle (guarded state machine)

```text
You are the implementation owner for exactly one issue in this repo:

  ISSUE: 34
  BRANCH: spec/34-lifecycle  (git switch -c spec/34-lifecycle origin/CourseContentManagement)
  PR BASE: CourseContentManagement  (never main)

First global read: docs/agents/per-issue-runbook.md — §1 kickoff, §2 workflow, §3 addendum row
for #34. Verify the gate first: blocked_by on #34 must be 0 (it lives on #29's deactivate
endpoint existing), then CLAIM the issue.

1. Context list: issue body + comments; docs/agents/*; Enrollment aggregate + EnrollmentStatus
   enum guards (spec/saga deactivate today, bare Activate with zero callers today); spec #29's
   id-keyed deactivate endpoint; the Payments revocation saga consumer path (untouched
   semantically — its calls just gain aggregate guards); the module's integration-event /
   outbox conventions INCLUDING whatever wire-identity pinning strategy #31 chose (read #31's
   final comments to copy it, don't invent a new one).

2. BASELINE: build + full suite; report pre-existing red.

3. Plan comment on the issue BEFORE coding: the transition matrix (single source of truth —
   Active⇄Suspended, Inactive/Expired/Suspended→Active via Reactivate, illegal moves rejected),
   who/when transition metadata approach, expiry mechanism choice, and the event contract.

4. IMPLEMENT per spec:
   - Aggregate transition methods with guards returning ErrorOr, each recording who/when
     transition metadata (same convention as the existing OnDeleted-style fields).
   - PATCH /courses/enrollments/{id}/suspend + /reactivate (admin/support enum AccessControl
     ARN permissioning, same pattern as #29's deactivate). Both IDEMPOTENT (re-suspending a
     Suspended, or reactivating an Active, returns success rather than a domain error).
   - Suspension/Resumption/Expiry integration events, wire-pinned per #31's strategy.
   - Expiry: aligned to the course plan's access window; choose the module's existing
     scheduler/background precedent (investigate; no new cron/lambda deploy artifact).
     Non-HTTP trigger only.
   - The Payments saga's deactivate continues to work — now through the guards (a blocked move
     raises a domain error the saga already surfaces as its failure event).

5. TESTS per the spec: full state-machine unit matrix (every legal transition + every illegal
   one), integration roundtrips (reactivate keeps progress; suspend/unsuspend preserves all
   state; invalid transition → its domain error code at the HTTP boundary; cross-tenant
   denial; idempotency), one expiry integration test (short window plan → Expired + event),
   and a dedicated revocation-saga consumer test (today's module has none for this consumer —
   you add regression protection for the money path).

6. REVIEW + SHIP: full suite green; user-story enumeration; PR against
   CourseContentManagement (never main), body starts "Closes #34" + evidence bullets.
```

---

## #35 — Hardening (validators + missing tests)

```text
You are the implementation owner for exactly one issue in this repo:

  ISSUE: 35
  BRANCH: spec/35-hardening  (git switch -c spec/35-hardening origin/CourseContentManagement)
  PR BASE: CourseContentManagement  (never main)

First global read: docs/agents/per-issue-runbook.md — §1 kickoff, §2 workflow, §3 addendum row
for #35. Verify the gate first: blocked_by on #35 must be 0, then CLAIM the issue.

1. Context list: issue body + comments; docs/agents/*; docs/reports/courses-gap-report-v2-2026-10-16.md
   (P1 item 12 = the validator gap list; §10 = the test gap list). Validator auto-registration
   + ValidationBehavior is already live — you follow the established per-command validator
   pattern, you do not touch pipeline config.

2. BASELINE: build + full suite; report pre-existing red.

3. Plan comment on the issue BEFORE coding: the complete message-type × validator matrix
   (excluding any message type newer specs #29/#32/#33/#34 already validated), plus the test
   target list. Confirm with a grep that you haven't double-validated anything a sibling spec
   created.

4. IMPLEMENT per spec:
   - Validators for every REMAINING command and query: CompleteItemCommand (BitIndex >= 0),
     DeactivateEnrollmentCommand (only if #29/#34 left the legacy studentId/courseId one
     unvalidated), the 4 asset-mutation commands (Assign/Unassign/Dismiss, MarkAvailable/
     MarkFailed), all 5 state commands, both Reorder commands (uniqueness + containment of the
     id set), UpsertCourseOverviewCommand, IssuePlaybackTicketCommand, SyncResourceMetadataCommand,
     and the unvalidated queries. Boundary-only rules; domain semantics stay in the aggregate.
   - Bug fix only where validation exposes one real defect: Reorder commands accepting
     duplicate/garbage ids — fix + its own focused test; nothing else.
   - TESTS (the larger half of this spec): EnrollInCourseCommand unit test (duplicate → conflict;
     unknown course; not-published rule); DeactivateEnrollmentCommand + RevokeStudentEnrollment-
     FromSagaConsumer tests (deactivation through guards, expected event, already-inactive
     tolerance); the CourseRedemptionSaga and CourseRevocationSaga state machines (transition/
     persistence/final-events — the money paths; imitate the module's existing MassTransit
     consumer/saga test style from EnrollStudentFromSagaConsumerTests / MetadataConsumerTests);
     plan command handlers (add/update/remove, error-path parity with the curriculum suites);
     ONE end-to-end playback journey integration test (enroll → playback ticket → playback
     session command path), asserting the session DTO shape; happy + one error path each.
   - No snapshot/golden files; no new feature endpoints; tests follow module naming/style.

5. TEST EXECUTION: full suite green; user-story enumeration (every named validator and test
   target ticked off); PR against CourseContentManagement (never main), body starts
   "Closes #35" + evidence bullets.
```

---

## Reminder (the loop)

1. Wait for the issue's gate: `blocked_by: 0`
2. New worktree from `origin/CourseContentManagement`, fresh conversation, paste the prompt
3. Agent claims, posts plan comment, implements (TDD), ships PR **to CourseContentManagement**
4. Merge only when satisfied → issue auto-closes → its dependents' gates clear to 0
5. `main` stays untouched until all chain PRs have landed; then one release PR
   `CourseContentManagement → main` closes out the whole chain.
