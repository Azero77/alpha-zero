# Issue #32 — code review evidence pack (grilling input)

Generated 2026-10-20 from a read-only review. Use this file to run the follow-up grilling
session and decide what to fix, defer, or re-scope. No code was modified to produce it.

## Review context

- Worktree: `/home/azero/.local/share/opencode/worktree/adfa55/spec-32-student-ux`
- Branch: `spec/32-student-ux`
- HEAD: `eedf0871` — merge of `CourseContentManagement` (#29) into `spec/32-student-ux`
- Spec: GitHub issue #32 + all comments (fetch with `gh api repos/:owner/:repo/issues/32 --jq '.title,.body'`
  and `gh api repos/:owner/:repo/issues/32/comments --jq '.[] | "---\n" + .body'`).
  Local `/tmp/issue-32-full.md` may exist; GitHub wins.
- Conventions: `docs/agents/issue-tracker.md`, `docs/agents/domain.md`,
  `docs/agents/per-issue-runbook.md` (§3 row #32).
- Diff maps:
  - `#32`-only delta: `git diff 5947b942 bb51b4fb`
  - merge resolution: `git show eedf0871`
  - current authority: files at HEAD (merge altered behavior)
- #29's progress model is authoritative: `Course.ActiveItemsBitmask` +
  `Enrollment.Progress.Bitmask`; counts derive only through `ProgressCalculation`; new
  enrollments start with a zero-length mask.

## Verification performed

Commands and outcomes:

- `dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj --nologo -v q`
  → **203 passed, 0 failed**.
- `dotnet test tests/Modules/Courses/Courses.Tests.Integration/Courses.Tests.Integration.csproj --nologo -v q`
  → first attempt failed with `MSB4018` (file lock) because it ran concurrently with the unit
  build; rerun alone → **90 passed, 0 failed**.

No code, commits, pushes, resets, or destructive DB commands were run. `.qoder/` is untracked
and must stay untouched.

## Findings

Severity is the reviewer's judgement. Each finding has the evidence needed to falsify it in the
grilling session.

### Blocker

#### B1 — Anonymous catalog and view are non-functional
- **Axis:** [Spec][Correctness]
- **Evidence:**
  - `src/alphazero-api/Modules/Courses/Presentation/Courses/Catalog/GetCourseCatalog.cs:43`
    and `.../View/GetCourseView.cs:87` use `AllowAnonymous()`.
  - `src/alphazero-api/AlphaZero.Shared/Infrastructure/Tenats/ITenantProvider.cs:33-35`:
    `HttpTenantProvider.GetTenant()` returns `null` unless a `tid` claim exists. An anonymous
    request has no claims, even if `X-TenantId` is sent (the claim check returns first).
  - `src/alphazero-api/AlphaZero.Shared/Infrastructure/Database/ModelBuilderExtensions.cs:76-78`:
    global filter is `e.TenantId == context.TenantId`.
  - Therefore `CourseQueryService.ListCourseCatalogAsync` (`CourseQueryService.cs:95-96`)
    returns zero rows, and `GetCourseStatusAsync` (`:152-158`) returns null, so
    `GetCourseViewPreRequestHandler’s` published branch never runs and the view returns 404.
- **Why it matters:** story 2 (“without making an account”), story 6 (“before any account
  exists”), and the decision that catalog/view are “permission-light … for anonymous usages”
  are not met for unauthenticated callers.
- **Why tests miss it:** `tests/.../Abstractions/ApiFactory.cs:59-61` replaces `ITenantProvider`
  with `TestTenantProvider` (static `SetTenant`), and `TestAuthHandler.cs:19-42` always
  authenticates. `CourseViewTests.cs:116-120` explicitly notes the “prospect” test is
  authenticated.
- **Caveat:** if an external gateway injects a `tid` claim even for anonymous traffic, this is
  neutralized. Nothing in this repo does that; verify the deployment path.
- **Grilling question:** what is the intended public-tenant resolution (host mapping, public
  tenant id, header, gateway claim)? What stops a caller from selecting another tenant?
- **Possible fixes:** explicit public-tenant argument to these two queries; host/header
  resolution before the claim check; an anonymous auth scheme that issues a tenant-only
  principal. Add a real anonymous integration test either way.

### High

#### H1 — Completion “exactly once” is not concurrency-safe
- **Axis:** [Correctness]
- **Evidence:**
  - `src/.../Domain/Aggregates/Enrollments/Enrollment.cs:87-91` guards with
    `ProgressCalculation.IsAllComplete(...) && CompletedOn is null` after a read-modify-write.
  - `src/.../Infrastructure/Persistance/Configurations/EnrollmentConfiguration.cs:17` has a
    unique `(StudentId, CourseId)` index but no rowversion/concurrency token.
  - `src/alphazero-api/AlphaZero.Shared/Infrastructure/UnitOfWork.cs:21-45` publishes domain
    events before the single `SaveChangesAsync`.
- **Failure modes:** two simultaneous completions of the final item both see
  `CompletedOn == null` and both raise `CourseCompleted`; completing the final two bits
  simultaneously can lose one bitmask update (last writer wins), so the course never reaches
  complete and no event fires.
- **Why it matters:** the implementation decision says the transition “fires exactly once per
  enrollment”. Sequential retries are safe; concurrent requests are not.
- **Grilling question:** is concurrency in #32’s acceptance, or does it belong to #29/#35?
- **Possible fixes:** `xmin`/rowversion + retry; an atomic conditional `UPDATE ... WHERE
  CompletedOn IS NULL` and raise only when one row is affected; an outbox dedupe key on
  `EnrollmentId`.

### Medium

#### M1 — Test harness hides the anonymous and authorization paths
- **Axis:** [Tests]
- **Evidence:** `TestAuthHandler.cs:19-42` always authenticates and does not add `tid`;
  `ApiFactory.cs:64-68` bypasses IAM; the draft-404 path is unit-tested only with a mock that
  returns success (`GetCourseViewPreRequestHandlerTests.cs:91`).
- **Why it matters:** B1 is invisible, and tenant tests exercise the static provider rather
  than production claim/header logic.
- **Grilling question:** add an anonymous opt-out (e.g. `X-Test-Anonymous`) and a non-bypass IAM
  host, or accept the gap?

#### M2 — Plan/pricing has no price
- **Axis:** [Spec]
- **Evidence:** `ListCourseCatalog.cs:21` and `GetCourseView.cs:9` expose only `Id, Name`;
  `CoursePlan` has no price fields.
- **Why it matters:** story 2 asks “what it costs”; the decision says “minimal plan info summary
  (price display)”. The plan comment documents the deviation because pricing lives behind the
  Principal in another module.
- **Grilling question:** defer with an explicit spec amendment, or add a cross-module price
  summary now?

#### M3 — “Overview” is only `Course.Description`
- **Axis:** [Spec]
- **Evidence:** `CourseQueryService.cs:174-209` never reads `CourseOverview.DescriptionContent`,
  `TargetAudienceContent`, or `LearningObjectivesContent`.
- **Why it matters:** the Solution says the view shows “curriculum structure, overview, plan
  info”; the implementation decision’s field list narrows this to “description”.
- **Grilling question:** is `Course.Description` the intended overview, or should the rich
  overview fields be included?
- **Note:** if included, do not project `.Value` in LINQ (see L7).

#### M4 — Non-published visibility is gated by `courses:View`, which students hold
- **Axis:** [Security][Spec]
- **Evidence:** `GetCourseViewPreRequestHandler.cs:60-66` authorizes non-Published statuses with
  `courses:View`; `Modules/Identity/Domain/SeedData/ManagedPolicies.json:9` grants it to
  `StudentAccess`; enrollment creates a course-scoped assignment
  (`EnrollmentCreatedIntegrationEventConsumer.cs:34-39`).
- **What is proven:** anonymous callers are masked; an authenticated caller with an assignment on
  that course (enrolled student, teacher) can read a draft/archived course. The plan comment says
  this is deliberate (“same permission the admin detail endpoint requires”).
- **Why it matters:** story 15 says unpublished content gets 404/403 “as the access rules already
  dictate”; whether enrolled students should retain access to non-published courses is a product
  decision.
- **Grilling question:** is `courses:View` the right gate for non-Published, or should it be
  `courses.overview:Manage` / `courses:Edit`?

#### M5 — `CompletedOn` exists only in the squashed Initial migration
- **Axis:** [Correctness, operational]
- **Evidence:** `20261018134147_Initial.cs:130`, `AppDbContextModelSnapshot.cs:308`, and the
  Designer agree; the merge message documents folding it in and dropping
  `20261018134147_AddEnrollmentCompletedOn`.
- **Why it matters:** any database that already applied `CourseContentManagement`’s Initial
  before this merge will never get the column (`MigrateAsync` will not re-run it).
- **Grilling question:** are dev/staging DBs disposable (reset), or do we need a new migration
  before release?

### Low

#### L1 — Overlay uses `CompletedItemIds`, not per-item `IsCompleted`
- **Axis:** [Spec]
- **Evidence:** `GetCourseView.cs:35-39`; the decision says “per-item `IsCompleted` from the
  bitmask”.
- **Grilling question:** keep the ID list (clients derive it) or add `IsCompleted` to
  `CourseViewItemResponse`?

#### L2 — No catalog status filter
- **Axis:** [Spec]
- **Evidence:** `CourseQueryService.cs:95-96` hard-codes Published; the decision lists
  “subject/status/search/sort/windows”.
- **Assessment:** published-only satisfies stories 1/14 and is documented in the plan comment;
  this is a literal-decision gap only. Decide whether to amend the decision.

#### L3 — Search wildcards are not escaped
- **Axis:** [Correctness]
- **Evidence:** `CourseQueryService.cs:101-103` interpolates user input into `%...%`; `%`/`_` act
  as wildcards (`Search=%` matches everything). Parameterization prevents injection.
- **Fix:** escape `\`, `%`, `_` before interpolation.

#### L4 — Sort validation is case-sensitive
- **Axis:** [API]
- **Evidence:** `ListCourseCatalog.cs:61-63` accepts only lowercase `newest|title`;
  `CourseQueryService.cs:111` lowercases. `Sort=Title` returns 400.
- **Fix:** normalize before validation or use `OrdinalIgnoreCase`.

#### L5 — Deleting the last incomplete item can make an enrollment 100% with no event
- **Axis:** [Correctness]
- **Evidence:** `CourseActiveItemsDomainEventHandler.cs:41-48` deactivates deleted bits;
  `ProgressCalculation` then intersects with the remaining active mask. No completion check runs
  on deletion.
- **Assessment:** story 9 speaks of completing the last required item; the decision ties the
  event to a completion attempt. This may be intended. Decide explicitly.

#### L6 — `IsAllComplete` is vacuously true with zero active items
- **Axis:** [Correctness]
- **Evidence:** `ProgressCalculation.cs:55-58` returns true when `ActiveItems == 0`; percentage
  returns 0. `CompleteItem` cannot reach it today (`Enrollment.cs:64` rejects every bit when the
  mask is empty), but a future/backfill caller could stamp completion.
- **Fix:** return false when there are no active items.

#### L7 — Legacy RichText projection bug remains (outside #32)
- **Axis:** [Correctness]
- **Evidence:** `CourseQueryService.cs:323` still projects `DescriptionContent.Value`, which the
  #33 probe showed yields null. The new catalog/view avoid RichText; the `ResourceArn.Value`
  projection (`:206`) is safe and integration-tested.
- **Action:** fix in the owning issue by materializing the overview row and mapping in memory;
  do not fold into #32 unless the view starts consuming rich overview.

#### L8 — Frontend schemas are not aligned
- **Axis:** [API]
- **Evidence:** no `lms-types` schemas exist for catalog/view;
  `enrollment.schema.ts:34` expects `enrolledAt` while the backend emits `enrolledOn`.
- **Assessment:** predates #32, but clients consuming the overlay will hit it.

#### L9 — Catalog predicate has no supporting index
- **Axis:** [Performance, suspicion]
- **Evidence:** only `(TenantId)`/soft-delete indexes exist; `Status = Published` plus
  leading-wildcard `ILIKE` will scan.
- **Action:** add `(TenantId, Status)`; consider `pg_trgm` GIN if volume warrants. Verify with
  `EXPLAIN`, do not assume.

## Known prior findings — verification status

1. **Overlay once returned only Active enrollments — confirmed fixed.**
   `EnrollmentQueryService.cs:167-183` has no status filter. Note there is no `Completed` status
   (`EnrollmentStatus` is Active/Inactive/Suspended/Expired); completion is `CompletedOn`/100%,
   which the overlay does not expose.
2. **Completion once used `Progress.IsAllComplete` — confirmed moved to
   `ProgressCalculation.IsAllComplete` over the active mask.** Sequential exactly-once works
   through retries and section deletion; concurrency is still unsafe (H1).
3. **RichText/ResourceArn null projections — partially fixed.** `ResourceArn.Value` is safe and
   asserted by `CourseViewTests`; the RichText null-projection bug remains in the legacy overview
   path (L7).
4. **`TestAuthHandler` always authenticates — still present**, compounded by
   `TestTenantProvider` and `BypassIAM` (M1).
5. **Migration squash — consistent.** `CompletedOn` appears once, snapshot/designer match; the
   operational caveat is M5.

## Confirmed working

- **Catalog:** title+description `ILIKE`, subject filter, newest/title sort, paging bounds,
  published-only, soft-deleted exclusion, subject name/cover/counts/duration/plans/timestamps
  enrichment, tenant filter for authenticated callers, stable `ThenBy(Id)`.
- **View:** public shape, caller-scoped overlay (no `studentId` parameter), any-status overlay,
  active-mask filtering, admin no overlay, soft-delete 404, tenant isolation, real ARNs,
  metadata bucket dropped, `/courses/catalog` beats `/courses/{id}`.
- **Progress:** monotonic bit allocation/no reuse, stale/deleted bits excluded, zero-length masks
  grow lazily, percentage clamped, 400 out-of-range / 409 inactive item.
- **Completion:** persisted `CompletedOn`, sequential idempotency, handler publishes
  `CourseCompletedIntegrationEvent` through the same module outbox registration
  (`CoursesModule.cs:52-55`, `DomainEventHandler.cs:42-51`).
- **Scope:** #32 did not change the existing admin list/detail/dashboard shapes or access.

## Needs verification (commands/tests that would settle it)

1. **Anonymous behavior on a real host** — an integration test with `TestAuthHandler` bypassed and
   no tenant header; assert catalog returns published rows and view returns 200 for published,
   404 for draft.
2. **Outbox delivery** — the test host remaps `IPublishEndpoint` to the in-memory bus
   (`ApiFactory.cs:82`), so `CompletionEventTests` assert only `CompletedOn`. Query the outbox
   tables in a host that uses the real module bus.
3. **Concurrent completion** — two parallel `POST /courses/enrollments/{id}/complete` requests
   for the final item(s); assert one `CompletedOn`, one event, no lost bit.
4. **Real IAM policy for draft visibility** — use the non-bypass host plus a real course
   assignment; decide whether M4 is a defect.
5. **Migration on an already-migrated DB** — apply the pre-merge Initial, then merge and start;
   confirm M5’s failure mode.
6. **Deletion-driven completion** — complete all but one item, delete that item’s section, and
   decide whether 100% without an event is acceptable (L5).
7. **Price source** — identify the cross-module source and product decision (M2).

## Suggested grilling agenda

1. B1: public-tenant resolution and security boundary. Decide before touching anything else.
2. H1: ownership and fix shape (rowversion vs atomic update vs inbox dedupe).
3. M1: test-host changes required to make B1/M4 provable.
4. M4: permission model for non-published view.
5. M2/M3/L1: spec amendments vs implementation changes for price, overview, overlay shape.
6. M5: release/migration strategy.
7. L2–L9: batch as follow-ups; assign owners (#29/#31/#35/product) rather than fixing inline
   in #32.

## Constraints for the next session

- Keep the review read-only until decisions are made.
- Per `docs/agents/per-issue-runbook.md`, cross-issue defects stay with their owning issue;
  log observations, do not fix inline.
- The merge resolution at `eedf0871` is authoritative; re-read HEAD before changing behavior.
- Do not modify or commit `.qoder/`.
