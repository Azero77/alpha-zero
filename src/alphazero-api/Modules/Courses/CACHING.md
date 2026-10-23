# Courses module — caching tolerances (#33)

The Courses module caches its hot read paths through the platform `HybridCache`
(in-process L1 (+ Redis L2 where configured) — see `AlphaZero.Shared/Infrastructure/DependencyInjection.cs`,
`AddHybridCache`). Read-through decorators: `Infrastructure/Caching/CachedCourseQueryService.cs`,
`CachedSubjectQueryService.cs`. Eviction: `Infrastructure/Caching/CourseCacheEvictionBehavior.cs`
(tag invalidation on the exact mutation commands, executed **after** the UnitOfWork commit).

## Cache keys and tenant isolation

Every key and every eviction tag **starts with the owning tenant id**. One academy's cached
entries can never be served to another. Tenant isolation is asserted behaviorally by the
integration suite (`CoursesCacheTests`). Requests without a tenant context (background bus
handlers) bypass the cache entirely and read the database directly.

## Surface × TTL × eviction triggers

| Surface (endpoint / query) | TTL | Key family | Evicted by (stale at most the TTL, and only until the next mutation) |
|---|---|---|---|
| Catalog list `GET /courses` (`ListCoursesQuery`) | 2 min | `courses:catalog:{tenantId}:subject=?...` | course create/update/delete; `Submit/Approve/Reject/Publish/Archive` (status in the summary); subject rename/delete (`SubjectName` is a catalog field) |
| Student catalog `GET /courses/catalog` (`ListCourseCatalogQuery`) | 2 min | `courses:catalog:{tenantId}:search=...:subject=...:sort=...` | same catalog family as above |
| Course tree view `GET /courses/{id}` | 10 min | `courses:view:{tenantId}:{courseId}:class=staff` | section add/rename/delete, item create/update/delete, both reorders, assign/unassign/dismiss asset |
| Public/student course view `GET /courses/{id}/view` | 10 min | `courses:view:{tenantId}:{courseId}:class=public` | same per-course family (enrollment overlay is resolved outside the cached summary) |
| Course overview `GET /courses/{id}/overview` | 10 min | `courses:overview:{tenantId}:{courseId}` | overview upsert, state transitions |
| Readiness companion `GET /courses/{id}/readiness` | 3 min | `courses:readiness:{tenantId}:{courseId}` | section/item/plan/asset mutations, overview upsert, transcoding state flips (`MarkCourseAssetAvailable/Failed`), metadata sync |
| Tenant subject listing `GET /courses/subjects` | 15 min | `courses:subjects:{tenantId}:...` | subject create/update/delete |

Tag families (eviction commands are consolidated in `CourseCacheEvictionBehavior`):

- `courses:catalog:{tenantId}` — catalog list entries (admin list + student catalog).
- `courses:course:{tenantId}:{courseId}` — tree view + public view + overview + readiness of one course.
- `courses:assets:{tenantId}` — tenant-wide asset-reader family, carried only by readiness
  entries (the asset-pipeline commands carry no course id; their stale bound is the 3-min
  readiness TTL).
- `courses:subjects:{tenantId}` — subject listing entries.

## Explicitly uncached

Enrollment checks in write paths, enroll/redeem, complete-item, enrollment state commands and
the bus pipeline, the student dashboard, admin listing/pool/analytics playback ticketing —
write-path correctness over speed; these surfaces always read DB-consistent state.

`GetCourseStatusAsync` (the single-column status read behind the view pre-request's
existence/visibility masking) is also a pass-through: caching it would widen the window in
which an unpublished course is still served to a caller who is no longer allowed to see it.

## Visibility variants

Visibility classes never share a key. The staff tree view (`GET /courses/{id}`) uses
`class=staff`; the public/student summary (`GET /courses/{id}/view`) uses `class=public`.
The public summary is enrollment-free by construction — the caller's progress overlay is
resolved by a separate enrollment query after the cached summary returns — so one entry can
serve every caller without leaking one student's completion state to another. Any future
visibility variant (e.g. a staff-only enrolled view) must carve yet another class segment.

## Known gaps on the current base (pre-existing, outside #33's caching scope)

Observed with the test-host probes (reported on the owning issues, not fixed here):

- `GET /courses/{id}/overview` always returns `descriptionContent: null`: the read projection
  `o.DescriptionContent.Value` on the value-converted `RichText` property does not materialize
  (verified: the DB row holds real content, the materialized entity reads it, the projection
  yields null). Caching wraps the surface as specified; the wrapper inherits the defect.
- `UpsertCourseOverviewCommand` is an `IRequest` (not `ICommand`), so the UnitOfWork behavior
  never saves it — overview edits are silently discarded. Its eviction case in
  `CourseCacheEvictionBehavior` therefore does not fire yet; it activates as soon as that
  command implements `ICommand`. The course tag family already invalidates overview entries via
  every other course-family mutation (verified in `CoursesCacheTests`).

Because of the first gap the overview cache tests assert the observable state (response `ETag`)
for cache-serve and tag-family eviction rather than description content.

## Stampede

Concurrent cold loads are protected by `HybridCache`'s own single-flight guarantee; this module
adds no stampede code.
