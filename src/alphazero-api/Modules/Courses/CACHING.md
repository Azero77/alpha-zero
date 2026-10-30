# Courses module — caching tolerances (#33)

The Courses module caches its hot read paths through the platform `ICachingService`
(`AlphaZero.Shared/Infrastructure/Caching`). That abstraction is the only place that touches
ASP.NET Core `HybridCache`; module code never references `Microsoft.Extensions.Caching.Hybrid`.
The platform runs a single in-process L1 cache — there is **no Redis L2** in this deployment.

Caching lives in three layers, all in `Infrastructure/Caching/`:

1. **Projection read decorators** — `CachedCourseQueryService.cs`, `CachedSubjectQueryService.cs`
   (default-shape reads of the seven listed surfaces).
2. **Repository write decorators** — `CachingCourseRepositoryDecorator`,
   `CachingCourseOverviewRepositoryDecorator`, `CachingCourseAssetRepositoryDecorator`,
   `CachingSubjectRepositoryDecorator`. On every `Add`/`Update`/`Remove` they queue the exact
   **key set** for the write (not tags — see below) and then delegate to the real repository.
3. **One repository read-through** — `CachingCourseOverviewRepositoryDecorator.GetByCourseIdAsync`
   (see "Decorator read-through" below).

## Cache keys and tenant isolation

Every Courses key and tag is built through `CacheScope`, which prepends the owning tenant id:
`{family}:{tenantId:N}:…` (for example `courses:view:{tenant}:{courseId}:class=staff`). One
academy's entries can never be served to another. This is asserted behaviorally across all
seven cached surfaces by `CoursesCacheTests`.

Requests without a tenant context (background bus handlers, tenant-less test scopes) **bypass
the cache entirely** and read the database. `CacheScope.Key/Tag` throw rather than ever writing
a placeholder segment such as `none`.

Only default-shaped queries are cached. Paging must be `page=1, perPage=10`; the student
catalog also requires no `search`, no `subject` filter and the default sort (`null`/empty or
`newest`). Caller-supplied paging, search, subject and sort values therefore never enter a
cache key — a non-default request bypasses the cache and reads current DB state immediately.

## Surface × TTL × eviction

| Surface (endpoint / query) | TTL | Key shape (after `{tenant}`) | Tags carried |
|---|---|---|---|
| Staff catalog `GET /courses` (`ListCoursesQuery`) | 2 min | `courses:catalog:{tenant}:class=staff` | none |
| Student catalog `GET /courses/catalog` (`ListCourseCatalogQuery`) | 2 min | `courses:catalog:{tenant}:class=student` | none |
| Course tree view `GET /courses/{id}` | 10 min | `courses:view:{tenant}:{courseId}:class=staff` | none |
| Public/student view `GET /courses/{id}/view` | 10 min | `courses:view:{tenant}:{courseId}:class=public` | **`courses:views:{tenant}`** (the one fan-out tag, G4.1) |
| Course overview `GET /courses/{id}/overview` | 10 min | `courses:overview:{tenant}:{courseId}` | none |
| Readiness companion `GET /courses/{id}/readiness` | 3 min | `courses:readiness:{tenant}:{courseId}` | none |
| Tenant subject listing `GET /courses/subjects` | 15 min | `courses:subjects:{tenant}` | none |

The staff `GET /courses` listing stays cached deliberately: it is a hot admin surface, and every
mutation that changes it is evicted through the write decorators below.

**Tags are fan-out only.** There is exactly one Courses tag family — the tenant-wide
`courses:views` (`CourseCacheTags.CoursePublicViews`, G4.1) — and it is carried by exactly one
read entry (the public view, `CachedCourseQueryService.cs:86`). Every other Courses entry passes
`[]`. Never add a second tag family: a new "can't enumerate this family" case returns to
grilling rather than to a quick tag.

## Eviction is a persistence concern

Commands and handlers contain **no cache logic**. The pipeline:

1. A handler mutates an aggregate and calls a write repository method (`Update(course)`,
   `Add(asset)`, `Update(overview)`, …).
2. The Scrutor decorator for that repository derives the affected **key set** from the aggregate
   argument alone via the static `CourseCacheKeys` key-set helpers (`EvictCourse`,
   `EvictCourseOverview`, `EvictCourseAsset`, `EvictSubject`) and queues it in
   `IEvictionProvider`; the subject decorator additionally queues the fan-out tag. No
   change-tracker scan and no parent lookup are involved; the aggregate's own `TenantId` scopes
   the keys.
3. `UnitOfWork.SaveChangesAsync` drains domain events and hands the transaction to EF. It has no
   caching dependency. **The flush machinery is two EF Core interceptors in
   `AlphaZero.Shared/Infrastructure/UnitOfWork.cs`** — there is no MediatR eviction behavior:
   - `EvictionSaveChangesInterceptor` — a **standalone** save (no open EF transaction) flushes
     at its own `SavedChangesAsync`; a failed standalone save burns exactly its queued keys
     (`SaveChangesFailedAsync` → `Discard`).
   - `EvictionTransactionInterceptor` — inside an explicit EF transaction the keys ride until
     **commit** → one flush; **rollback** → `Discard`; ambiguous **commit-path failure** →
     flush (over-eviction is safe; a delayed eviction is not).
   - Ambient `System.Transactions` guard: a save inside `Transaction.Current` **throws
     immediately** — an ambient commit has no EF hook, so its queue could never be flushed.
     Belt-and-braces: `DatabaseSettings` appends `Enlist=false` to every connection string.
4. On success the flush calls `ICachingService.RemoveAsync(key)` per key and
   `RemoveByTagAsync(tag)` per tag. A cache failure after commit is logged and swallowed
   (entries age out by TTL); only `OperationCanceledException` propagates.

The eviction queue lives on the **request-scoped** `ScopedEvictionProvider` instance (two
lane `HashSet`s guarded by a lock): decorators, the unit of work, the DbContext and both
interceptors resolve the same scoped instance, and each `IModule.Send` creates one child
scope — one scope is exactly one queue. It is deliberately NOT `AsyncLocal`: the interceptor
fires from the pipeline level that called `SaveChangesAsync`, an ancestor context of the
handler that queued, and AsyncLocal writes never flow back up to an ancestor.

### Handler conversion rule (the bypass audit)

A handler that mutates a tracked aggregate **without calling the decorated repository method**
bypasses eviction — the queue never sees its keys. Every Courses command therefore follows the
conversion rule: **mutate, then explicitly re-commit via the write decorator**
(`_courseRepository.Update(course)` / `_assetRepository.Update(asset)` / …). The audit below
was re-run against the source in Phase 3 (2026-10-29) and is enforced by the integration matrix:

| Decorated write (the callers) | Key set queued (`CourseCacheKeys`) | Count |
|---|---|---|
| `ICourseRepository` `Add`/`Update`/`Remove(course)` — create/update/delete/status/reorder/section/item/plan/resource* | `catalog:staff`, `catalog:student`, `view:staff`, `view:public`, `overview`, `readiness`, `status`, `entity:course` | 8 keys |
| `ICourseOverviewRepository` `Add`/`Update`/`Remove(overview)` — `UpsertCourseOverviewCommand` | `overview`, `readiness`, `view:public`, `catalog:staff`, `catalog:student`, `entity:overview` | 6 keys |
| `IRepository<CourseAsset>` `Add`/`Update`/`Remove(asset)` — any TPH subtype; `SyncResourceMetadata`, transcode-state flips | `readiness`, `view:public`, `catalog:staff`, `catalog:student`, `entity:asset` (no tree — the staff tree reads only `r.CourseAssetId`); an unassigned pool asset (`CourseId == Guid.Empty`) queues **nothing** | 5 keys |
| `ISubjectRepository` `Add`/`Update`/`Remove(subject)` | `courses:subjects`, `entity:subject` + **the one fan-out tag** `courses:views` | 2 keys + 1 tag |

\* Sections, plans, curriculum items and resources have no decorator of their own: every write
to them loads the `Course` aggregate and calls `ICourseRepository.Update(course)`, including
`AddSection` (fixed in Phase 3, `AddSection.cs:41`) and all three mask-mutation paths in
`CourseActiveItemsDomainEventHandler`. Empty-tenant aggregates (`TenantId == Guid.Empty`) queue
nothing (verified: the asset-available consumer path runs from a null-tenant bus scope and the
tenant comes from the entity itself).

Over-eviction is deliberate: an extra queued key is a future cache miss (safe); a missing one
is a stale-cache bug.

### Subject-rename staleness bounds (G4.1/G4.2)

A rename/delete of a subject is not attributable to any single course, so it fans out by tag:

- **Public course views** are invalidated by the tag immediately — worst-case staleness equals
  their TTL, **10 minutes** (e.g. if the tagged flush were to fail; through failure the entries
  die by TTL, never indefinitely).
- **Staff/student catalogs** are deliberately NOT on the tag: they are evicted by key on every
  course write, and a subject rename lands in them within **at most 2 minutes** (their TTL) —
  accepted for these rare operations instead of over-evicting two hot entries per subject write.
- **The subject list** (`courses:subjects`) is a plain key and flushes with the write itself.

## Decorator read-through (G11) and the tracked-entity rule (G12)

Exactly **one** repository read is cached: `ICourseOverviewRepository.GetByCourseIdAsync` — a
flat, JSON-safe DTO — under the `entity:overview` key, ambient tenant, 5-minute TTL (`EntityPolicy`).
The upsert handler reads that DTO for its add-or-update decision, which is why every overview
write queues the entity key too: a cached copy can never outlive the write that would change it.

All other aggregate reads (`Course.GetById`, `CourseAsset.GetById`, `Subject.GetById*`, any
collection/filtered read) **delegate uncached**. Aggregate graphs are tracked-identity bridges:
a hand-off of a cached, deserialized copy would collide with the live tracked instance in the
ambient `AppDbContext` (two instances, one identity key), and a collection read's shape is
caller-defined (never a key). Never route these reads through the cache, and never "fix" a
cached-vs-tracked mismatch by scanning the EF change tracker — a new cacheable read needs a
flat write-coupled DTO plus its `*Entity` key queued by every write family that feeds it.

## Explicitly uncached

Enrollment checks in write paths, enroll/redeem, complete-item, enrollment state commands and the
bus pipeline, the student dashboard, admin listing/pool/analytics, playback ticketing — write-path
correctness over speed; these surfaces always read DB-consistent state.

`GetCourseStatusAsync` (the single-column status read behind the view pre-request's
existence/visibility masking) is also a pass-through: caching it would widen the window in which
an unpublished course is served to a caller who is no longer allowed to see it. The
`courses:status` key still exists as an **eviction-only** entry in the course key set — a
defensive no-op (removing an absent key) that already covers the surface if it is ever cached.

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
