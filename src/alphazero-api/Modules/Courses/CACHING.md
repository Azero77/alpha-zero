# Courses module — caching tolerances (#33)

The Courses module caches its hot read paths through the platform `ICachingService`
(`AlphaZero.Shared/Infrastructure/Caching`). That abstraction is the only place that touches
ASP.NET Core `HybridCache`; module code never references `Microsoft.Extensions.Caching.Hybrid`.
The platform runs a single in-process L1 cache — there is **no Redis L2** in this deployment.
Read-through decorators live in `Infrastructure/Caching/CachedCourseQueryService.cs` and
`CachedSubjectQueryService.cs`; write-side decorators around the four Courses write repositories
queue eviction tags (see below).

## Cache keys and tenant isolation

Every Courses key and tag is built through `CacheScope`, which prepends the owning tenant id:
`{family}:{tenantId:N}:…` (for example `courses:view:{tenant}:{courseId}:class=staff`).
One academy's entries can never be served to another. This is asserted behaviorally across all
seven cached surfaces by `CoursesCacheTests`.

Requests without a tenant context (background bus handlers, tenant-less test scopes) **bypass
the cache entirely** and read the database. `CacheScope.Key/Tag` throw rather than ever writing a
placeholder segment such as `none`.

Only default-shaped queries are cached. Paging must be `page=1, perPage=10`; the student catalog
also requires no `search`, no `subject` filter and the default sort (`null`/empty or `newest`).
Caller-supplied paging, search, subject and sort values therefore never enter a cache key — a
non-default request bypasses the cache and reads current DB state immediately.

## Surface × TTL × eviction

| Surface (endpoint / query) | TTL | Key shape (after `{tenant}`) | Tags carried |
|---|---|---|---|
| Staff catalog `GET /courses` (`ListCoursesQuery`) | 2 min | `courses:catalog:{tenant}:class=staff` | `courses:catalog:{tenant}` |
| Student catalog `GET /courses/catalog` (`ListCourseCatalogQuery`) | 2 min | `courses:catalog:{tenant}:class=student` | `courses:catalog:{tenant}` |
| Course tree view `GET /courses/{id}` | 10 min | `courses:view:{tenant}:{courseId}:class=staff` | `courses:course:{tenant}:{courseId}`, `courses:views:{tenant}` |
| Public/student view `GET /courses/{id}/view` | 10 min | `courses:view:{tenant}:{courseId}:class=public` | `courses:course:{tenant}:{courseId}`, `courses:views:{tenant}` |
| Course overview `GET /courses/{id}/overview` | 10 min | `courses:overview:{tenant}:{courseId}` | `courses:course:{tenant}:{courseId}` |
| Readiness companion `GET /courses/{id}/readiness` | 3 min | `courses:readiness:{tenant}:{courseId}` | `courses:course:{tenant}:{courseId}`, `courses:assets:{tenant}` |
| Tenant subject listing `GET /courses/subjects` | 15 min | `courses:subjects:{tenant}` | `courses:subjects:{tenant}` |

The staff `GET /courses` listing stays cached deliberately: it is a hot admin surface, and every
mutation that changes it is evicted through the write decorators below.

## Eviction is a persistence concern

Commands and handlers contain **no cache logic**. The pipeline is:

1. A handler mutates an aggregate and calls a write repository method (`Update(course)`,
   `Add(asset)`, …).
2. The Scrutor decorator for that repository derives tags from the aggregate argument
   (`CourseCacheTags`) and queues them in `IEvictionProvider`; then it delegates to the real
   repository. No change-tracker scan and no parent lookup are involved.
3. `UnitOfWork.SaveChangesAsync` drains domain events and commits the transaction. It has no
   caching dependency.
4. `CacheEvictionFlushBehavior` — the outermost MediatR behavior — flushes the queue only after
   the outer UnitOfWork has committed. Nested dispatches (domain-event handlers, `Send` inside a
   handler) never flush early.

Tag derivation is write-side and opt-in: a handler that mutates a tracked aggregate without
calling the decorated repository method bypasses eviction. All Courses handlers that relied on
implicit change tracking (`AddSection`, `UpdatePlan`, `RemovePlan`, both reorder handlers) now
call `_courseRepository.Update(course)`. As a backstop,
`CoursesEvictionCoverageInterceptor` logs a warning when an active eviction scope persists a
cache-feeding Courses entity with an empty queue; it never throws and never queries.

Failure semantics: an `ErrorOr` error result or an exception discards the queued tags; a flush
failure after a successful commit is logged and swallowed (entries then age out via TTL), except
`OperationCanceledException`, which always propagates. The queue and the nesting counter are
`AsyncLocal`-backed because each module dispatch runs in a new child container scope.

### Write decorator → tag mapping

| Decorated write | Tags queued |
|---|---|
| `ICourseRepository` `Add`/`Update`/`Remove` (create/update/delete, any status transition) | per-course + catalog |
| `ICourseOverviewRepository` `Add`/`Update`/`Remove` | per-course only |
| `IRepository<CourseAsset>` `Add`/`Update`/`Remove` (any concrete TPH type) | per-course when `CourseId` is set, otherwise tenant-wide `courses:assets` |
| `ISubjectRepository` `Add`/`Update`/`Remove` | subjects + catalog + views |

Sections, plans, curriculum items and resources have no decorator of their own: every write to
them loads the `Course` aggregate and calls `ICourseRepository.Update(course)`, which queues the
per-course and catalog tags. Adding, renaming, deleting or reordering sections/items,
assignment/dismissal of assets and transcode-state flips (`MarkCourseAssetAvailable/Failed`)
therefore all evict without any command-specific code. Over-eviction is deliberate: a missing tag
is a correctness bug, an extra tag is a cache miss.

Per-course asset eviction keeps unrelated courses cached. The tenant-wide `courses:assets` tag is
a coarse fallback only for assets with no course binding; readiness entries also carry it, so a
fallback eviction can only invalidate readiness rows, never a tenant's entire course family.

`UpsertCourseOverviewCommand` now implements `ICommand<Success>`, so the UnitOfWork decorator
persists overview edits and the overview repository decorator evicts the per-course family
(overview edits never affect catalog summary fields).

## Explicitly uncached

Enrollment checks in write paths, enroll/redeem, complete-item, enrollment state commands and the
bus pipeline, the student dashboard, admin listing/pool/analytics playback ticketing —
write-path correctness over speed; these surfaces always read DB-consistent state.

`GetCourseStatusAsync` (the single-column status read behind the view pre-request's
existence/visibility masking) is also a pass-through: caching it would widen the window in which
an unpublished course is served to a caller who is no longer allowed to see it.

Subject-by-id reads and non-default paging/filter requests are pass-through as well.

## Visibility variants

Visibility classes never share a key. The staff tree view uses `class=staff`; the public/student
summary uses `class=public`. The public summary is enrollment-free by construction — the caller's
progress overlay is resolved by a separate enrollment query after the cached summary returns — so
one entry serves every caller without leaking one student's completion state. Any future
visibility variant must carve yet another class segment.

## Known gap (pre-existing, outside #33's caching scope)

`GET /courses/{id}/overview` always returns `descriptionContent: null`: the read projection
`o.DescriptionContent.Value` on the value-converted `RichText` property does not materialize.
The DB row holds real content and the materialized entity reads it; only the projection yields
null. Caching wraps the surface as specified and inherits the defect. Cache-serve and eviction for
overview are therefore asserted through the response `ETag`.

## Stampede and multi-instance behaviour

Concurrent cold loads are protected by `HybridCache`'s own single-flight guarantee; this module
adds no stampede code. Invalidation is in-process only: with more than one API instance, a
mutation on one instance does not invalidate another instance's L1 entries — those entries remain
stale for at most their TTL. Introducing a distributed L2/backplane is tracked in `TODOS.md`.
