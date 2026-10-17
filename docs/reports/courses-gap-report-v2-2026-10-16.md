# 🔍 Courses Module — Gap Report v2

> Re-verification of the **v1 Comprehensive Gap Analysis** against the codebase as of commits
> `12f5071a` (VideoSecurity Architecture) → `77f6e248`, plus a pass for **new bugs and dead code
> introduced or left behind by those commits**.
>
> Date: 2026-10-16 · Module: `src/alphazero-api/Modules/Courses/`

---

## Executive Summary

The last four commits landed **significant new work, most of it outside the v1 P0 list**: the
playback-ticket streaming pattern (HMAC-signed tickets, Cloudflare signed cookies, Cloudflare edge
manifests), resource-identity cleanup (ResourceId / CourseAssetId / VideoId disambiguation), and a
large test-coverage push — the module now has a real integration host (WebApplicationFactory +
Testcontainers Postgres + WireMock + Respawn) and substantial domain unit tests.

The **v1 P0 CRUD gaps are effectively untouched**: UpdateCourse, section rename/delete, course
delete, subject update/delete, enrollment deactivate, and student-enrollment listing are all still
unwired. The playback/streaming feature was initially flagged with three security holes in this
report, but all three were **retracted after verification** (§2-A): the manifest route lives in the
Next.js BFF, device binding is IAM's `IsMainDevice` operator, and permission-based access
intentionally admits admins.

| Area | v1 | v2 | Change |
|------|----|----|--------|
| Domain Model | ★★★★☆ | ★★★★☆ | Unchanged — strong core; Suspended/Expired still unreachable |
| Application Layer | ★★★☆☆ | ★★★★☆ | ↑ Validators ×6, duplicate-enroll check, UnitOfWork behavior confirmed |
| Presentation (Endpoints) | ★★★☆☆ | ★★★☆☆ | +Playback ticket; P0 CRUD gaps remain |
| Infrastructure | ★★★★☆ | ★★★★☆ | Soft-delete interceptor verified by tests; ResourceARns cleaned up |
| Test Coverage | ★★☆☆☆ | ★★★★☆ | ↑↑ Integration project + domain unit tests; a few handler gaps remain |
| Spec Compliance | ★★★☆☆ | ★★★☆☆ | P0 untouched; streaming verified complete (incl. frontend BFF) |

---

## 1. What Changed Since the v1 Report (DONE)

| v1 item | Status now | Evidence |
|---------|-----------|----------|
| **#9 Video streaming URL resolution** | **DONE (verified end-to-end)** | `Application/Courses/Commands/Playback/IssuePlaybackTicket.cs` + `Presentation/Courses/Items/IssuePlaybackTicket.cs`; HMAC-SHA256 tickets (`Shared/Infrastructure/Security/PlaybackTicketSigner.cs`); Cloudflare signed-cookie playback session in VideoUploading (`CreatePlaybackSession`) + **Next.js BFF manifest/segment proxies** (`src/alphazero-frontend/apps/app/app/api/player/...`); device-lock enforced by IAM `IsMainDevice` per `docs/OneDeviceLockSystemDesign.md` |
| **#15 Duplicate enrollment prevention** | **DONE** | `EnrollInCourse.cs:55-56` checks `(StudentId, CourseId)` → 409 `Enrollment.Exists`; covered by `EnrollmentTests.cs:Idempotency_Should_Fail_WhenEnrollingTwice` |
| **#12 Missing FluentValidation validators** | **LARGELY DONE (12 of ~27 message types)** | `CreateCourse`, `AddSection`, `Create/Update/DeleteCourseSectionItem`, `Add/Update/RemovePlan`, `EnrollInCourse`, `CreateVideoCourseAsset`, `CreateSubject`, `GetCourseReadiness` now have validators. See §2 for the residual list |
| **Arch-E UnitOfWork pipeline behavior** | **DONE (verified)** | `Shared/Application/UnitOfWorkDecoratorCommandHandler.cs` registered at `Courses/Infrastructure/DependencyInjection.cs:84`; saves after success on `ICommand` |
| **Test coverage (part of Sprint 3)** | **SUBSTANTIALLY DONE** | New `Courses.Tests.Integration` project: `CourseTests`, `EnrollmentTests`, `DashboardTests`, `CourseOverviewTests`, `CourseBuilderAndLifecycleIntegrationTests`, `CourseAnalyticsTests`, `SoftDeleteTests`, `SubjectTests`, `SyncResourceMetadataIntegrationTests`. Domain units: `CourseTests`, `CourseAggregateTests`, `ProgressTests`, `BitmaskStabilityTests`, asset state-machine tests, playback-ticket handler tests |
| **Soft-delete plumbing (side gifts)** | **DONE at infrastructure level** | EF SaveChanges interceptor sets `IsDeleted/OnDeleted`; global query filters hide deleted rows — verified by `SoftDeleteTests` (sections + courses). |
| **Resource identity confusion (f22d7a21, 77f6e248)** | **PARTIALLY DONE** | 3-layer resolution chain (curriculum placement → pool asset → external video id) is now server-translated and explicit (`CourseResourceDetailsDto`), but response DTOs still alias ids (§2-B2) |

---

## 2. 🆕 New Findings (since `12f5071a`)

### 🟢 A. Playback/streaming — verified complete by design (initial v2-draft flags RETRACTED)

Three items in my first pass of this section turned out to be **by design** once the frontend BFF
and the Identity docs were checked; recorded here so they don't resurface as "gaps":

1. **Manifest route EXISTS** — `/api/player/{videoId}/master` lives in the Next.js BFF
   (`src/alphazero-frontend/apps/app/app/api/player/[videoId]/master/route.ts`, plus the `media/[...path]`
   variant proxy), rewriting Cloudflare manifests and forwarding signed cookies. The API's
   `MasterManifestUrl` reference resolves correctly.
2. **Device binding is an Identity concern, correctly placed** — `docs/OneDeviceLockSystemDesign.md`:
   `IsMainDevice` is an ABAC operator in the Policy Evaluation Engine, backed by cryptographic
   device signatures (X-Signature/X-Timestamp) and `UserMainDeviceId`. Courses' ticket `fp` stamp is
   informational context only — device enforcement happens on the request pipeline, not in Courses.
3. **No explicit enrollment query at ticket time is intentional** — access is policy-evaluated on
   `courses:View` at request time, which deliberately permits admins/instructors to watch courses
   too (support/demo), per the handler's documented decision.

Residual (minor) items:
- The playback-ticket endpoint's Swagger summary still claims *"Validates student enrollment"* —
  align the summary text with the handler's actual policy-based access model.
- `PlaybackTicketSigner` falls back to a hardcoded dev secret (`PlaybackTicketSigner.cs:18-21`);
  production should fail-fast when `PlaybackTicketSecret` is missing rather than silently degrade.
- DASH/Widevine-FairPlay remain absent (clear-key infra exists unused) — PRD ask, deferred.

### 🟠 B. Dead code and drift introduced/exposed by the refactor

1. **`Course.EvaluateReadiness()` is dead — and its logic now lives twice.** `Course.cs:120-123`
   still `new`s `CourseReadinessService` inline (v1 finding C, unchanged), but nothing calls it:
   the readiness *query* was rewritten as SQL projections in `CourseQueryService.GetCourseReadinessAsync`
   (lines 80-139) with inline `blockingReasons` strings — a **duplicate implementation** that can
   drift from `CourseReadinessService` (the only consumer left is the Publish path via
   `CoursePublishingService`). Two sources of truth for "is this course ready".
2. **GetCourse response still aliases/confuses ids.** `ResourceDto.Arn` is actually
   `CourseAssetId.ToString()` (`CourseQueryService.cs:47`) — not an ARN — and `Metadata` is always
   an empty `{}` (`EmptyJsonElement`, lines 16, 50). This is the exact confusion commit `f22d7a21`
   ("we need to debug ResourceId, CourseAssetId, VideoId differences") was meant to kill, still
   unresolved at the response boundary.
3. **Two competing handlers for `EnrollementCreatedDomainEvent`.** `CoursePublishedDomainEventHandler.cs`
   and `EnrollementCreatedDomainEventHandler.cs` (log-only placeholders, v1 #17) still exist, while
   the real integration-event publishing lives in `Application/Enrollements/EventHandlers/DomainEventHandler.cs`.
   Both transient handlers for the same event are registered — delete the placeholders.
4. **Debug leftover in shared pipeline.** `UnitOfWorkDecoratorCommandHandler.cs:19` —
   `Console.WriteLine("XXXXXXXXX UnitOfWorkDecoratorCommandHandler START…")` prints on **every
   command in the platform**, not just Courses.
5. **Section soft-delete domain methods are unreachable.** `CourseSection.Delete()`/`Restore()`
   (`CourseSection.cs:130-148`, `internal`) have zero callers; `SoftDeleteTests` even comments
   *"Assuming for now we don't have a public endpoint for DeleteSection yet"* and exercises the
   plumbing directly through `DbContext`. The DB partial index exists — only the domain wiring and
   endpoint are missing.
6. **Empty aggregate directories persist** (v1 Arch-B): `Domain/Aggregates/Course/`,
   `Domain/Aggregates/Enrollement/`, `Application/Resolvers/` — all still empty (Assessments now
   uses its `Resolvers/`, showing it is viable there).

---

## 3. v1 Item-by-Item Status (RESIDUAL GAPS)

### 🔴 P0 — Still blocking basic instructor/admin UX (all carried over, none done)

| # | Item | v2 verdict | Detail |
|---|------|-----------|--------|
| 1 | **UpdateCourse endpoint** | ❌ STILL MISSING | `Course.UpdateInformation()` (`Course.cs:113-118`) has **zero callers**. No command, no `PUT /courses/{id}` |
| 2 | **UpdateSection / DeleteSection endpoints** | ❌ STILL MISSING | `CourseSection.Update(title, order)` is `internal` and only used internally by reorder. `Delete()/Restore()` uncalled. Soft-delete plumbing works (tested via DbContext) — only command + endpoint needed |
| 3 | **DeleteCourse endpoint** | ⚠️ PARTIAL | `ArchiveCourseCommand` + handler exist but **no endpoint sends them**. `Course.Archive()` only sets `Status=Archived`; **nothing ever writes `Course.IsDeleted`** (no Delete method at all, unlike sections) |
| 4 | **UpdateSubject / DeleteSubject** | ❌ STILL MISSING | `Subject` (30 lines) has only `Create()`; no `Update()`/`Delete()` in domain, none in app/presentation |
| 5 | **DeactivateEnrollment endpoint** | ❌ STILL MISSING | Command + handler exist (`Deactivate/DeactivateEnrollment.cs`); only caller is the revocation saga consumer. No HTTP route |
| 6 | **ListStudentEnrollments endpoint** | ❌ STILL MISSING | `GetStudentEnrollmentsQuery` / `GetStudentEnrollmentsForTenantQuery` + `IEnrollmentQueryService` impl exist — **the MediatR queries are dead code** (only dashboard uses the service directly across tenants) |

### 🟠 P1 — Student UX remains the biggest product gap

| # | Item | v2 verdict | Detail |
|---|------|-----------|--------|
| 7 | Catalog browsing | ❌ STILL MISSING | `ListCoursesQuery(SubjectId, Page, PerPage)` — no search, **no status filter**, hard-coded `OrderBy(c => c.Title)`. `CourseSummaryDto` = 5 fields (Id, Title, Description, SubjectId, Status) |
| 8 | Student course-detail view | ❌ STILL MISSING | `GET /courses/{id}` returns admin shape (`courses:View` + raw sections + empty metadata). Zero student-view symbols exist anywhere; enrollment data (dashboard/`GET /courses/enrollments/{id}`) is disjoint from course detail |
| 9 | Streaming URL resolution | ✅ DONE | Verified end-to-end (see §2-A); residual: summary text mismatch + dev-fallback secret |
| 10 | ListCoursesForStatus | ❌ STILL MISSING | No status parameter anywhere; only individual state-mutation endpoints |
| 11 | `CourseCompletedDomainEvent` | ❌ STILL MISSING | `Progress.IsAllComplete` exists (`Progress.cs:71-83`) but `Enrollement.CompleteItem` (line 41-59) only raises `ItemCompletedDomainEvent` — final completion is invisible to the domain. Still in `DOMAIN_DESIGN.md` as "planned" only |
| 12 | Validators | ⚠️ PARTIAL | 12 exist. Still missing: `CompleteItemCommand` (no `BitIndex >= 0` check at boundary), `DeactivateEnrollmentCommand`, all 5 Asset mutation commands except CreateVideo, all 5 State commands, 2 Reorder commands, `UpsertCourseOverview` (endpoint-level validator only), `IssuePlaybackTicketCommand`, `SyncResourceMetadataCommand` |

### 🟡 P2

| # | Item | v2 verdict |
|---|------|-----------|
| 13 | Enrollment reactivation endpoint | ❌ `Enrollement.Activate()` still has zero callers |
| 14 | Suspended/Expired transitions | ❌ No `Suspend()`/`Expire()` domain methods; states unreachable |
| 15 | Duplicate enrollment prevention | ✅ DONE + tested |
| 16 | Empty `Resolvers/` | ❌ Still empty |
| 17 | Log-only event handlers | ⚠️ Publishing handler exists; two decorative duplicates remain (see §2-B3) |
| 18 | `CourseDto` enrichment | ❌ Still 6 fields; no TenantId/timestamps/plans/counts/cover/subject name + id confusion (§2-B2) |
| 19 | `CourseSummaryDto` enrichment | ❌ Still 5 fields |
| 20 | Caching layer | ❌ `Shared/.../CachingRepository` + `AddHybridCache` exist; **Courses doesn't use them** (Identity/VideoUploading/Tenants do) |

### 🟢 P3 (unchanged, not started)
Community/discussion, gamification, certificates (blocked on #11), ratings, bookmarks, prerequisites, course cloning.

### 🧱 Architecture

| v1 item | Status |
|---------|--------|
| A. "Enrollement" typo | ❌ Worse: now a hybrid (223 typo/38 files vs 259 correct/40 files). Route string is `/courses/enrollements/{id}/complete`. Rename is a mechanical, high-value cleanup — do it before writing more enrollment endpoints |
| B. Empty singular aggregate dirs | ❌ Still empty |
| C. `EvaluateReadiness` inline `new` | ❌ Still there — now genuinely dead; delete it and keep one readiness source of truth (§2-B1) |
| D. Empty `ISectionRepository`/`ICurriculumItemRepository` | ❌ Still empty marker interfaces, registered in DI |
| E. UnitOfWork behavior | ✅ Verified registered (§1) |

### 🧪 Remaining test gaps (much smaller than v1)

Covered now: CompleteItem (unit+integration), Course aggregate invariants, Progress/bitmask stability, asset state machine, replay ticket issuance, reorder (integration), overview upsert (integration), sync metadata (unit+integration), sagas' enroll consumer.
**Still missing:**
- ❌ `EnrollInCourseCommand` handler unit test (only integration)
- ❌ `DeactivateEnrollmentCommand` handler + `RevokeStudentEnrollmentFromSagaConsumer`
- ❌ Plan command handlers (Add/Update/RemovePlan) — aggregate methods only
- ❌ `CourseRedemptionSaga` / `CourseRevocationSaga` state machines
- ❌ Reorder handlers (unit), `UpsertCourseOverview` (unit)
- ❌ Any test asserting **playback session** behavior end-to-end (VideoUploading side)

---

## 4. Updated Endpoint Inventory

All 33 FastEndpoints verified. Still absent from the inventory:

```
PUT    /courses/{id}                       UpdateCourse
PUT    /courses/{id}/sections/{sectionId}  RenameSection
DELETE /courses/{id}/sections/{sectionId}  DeleteSection
DELETE /courses/{id}                       DeleteCourse   (or PATCH /archive wiring ArchiveCourseCommand)
PUT    /courses/subjects/{id}              UpdateSubject
DELETE /courses/subjects/{id}              DeleteSubject
PATCH  /courses/enrollments/{id}/deactivate  DeactivateEnrollment
PATCH  /courses/enrollments/{id}/activate    ReactivateEnrollment
GET    /students/{id}/enrollments          ListStudentEnrollments   (queries already exist!)
GET    /courses/catalog                    Student catalog (search/sort/status)
GET    /courses/{id}/view                  Student course view w/ progress overlay
```

(Note: `GET /api/player/{videoId}/master` is served by the frontend BFF, not the API — it was
listed in an earlier draft of this section and is **not** missing.)

---

## 5. Recommended Priority (v2)

1. **Maintenance leftover in playback docs** — playback-ticket endpoint's Swagger summary
   still claims enrollment validation; align with policy-based access. Fail-fast on missing
   ticket secret in production config.
2. **Unwire the dead paths** — delete `EvaluateReadiness()`, the two placeholder event handlers,
   the `Console.WriteLine`, empty dirs, empty repo interfaces. Cheap, reduces drift.
3. **P0 CRUD** — UpdateCourse, section rename/delete (domain ready), course delete/IsDeleted path
   (needs a domain Delete method), subject Update/Delete (needs domain methods first),
   deactivate endpoint (command ready), student-enrollments list (queries ready — smallest win).
4. **Student UX** — catalog (search/status/sort), student course view, completion event,
   DTO enrichment, caching.
5. **Naming + validation residue** — Enrollment rename, residual validators (~5 command types).
6. **Sagas + handler unit tests** for the remaining recoverable paths.

---

*Method: all verdicts verified by direct code reading (file:line) across Domain/Application/
Infrastructure/Presentation and both test projects; three parallel verification passes cross-check.*
