# Issue #33 caching — code review and solution plan

Generated 2026-10-23 from a read-only review of the `spec/33-caching` worktree.
**Rewritten 2026-10-27** after the second eviction-design pivot agreed with the issue owner.
**Reconciled 2026-10-29** per `spec-33/spec-33-caching-handoff.md` §8 (that handoff is the
binding source of truth; this plan now matches the as-built implementation. Phases 0–3 are
implemented and committed; the verified key-set deltas and the filled G1–G12 ledger are in
§5/§11).

> **Design pivot (binding for this document).**
>
> The `Caching*RepositoryDecorator` classes own the cache logic on the repository surface:
>
> 1. **Reads are cached by the decorator.** Cacheable repository read methods go through
>    `ICachingService.GetOrCreate` with keys from the static `CourseCacheKeys` entity
>    functions. The existing `Cached{Course,Subject}QueryService` projection caching is kept
>    as-is; the decorator read-through caching is additive and grilled in G11/G12.
> 2. **Writes evict through the decorator.** A write decorator receives the aggregate that is
>    actually being written, computes the affected cache **keys** by calling the same static
>    `CourseCacheKeys` functions, and queues them in `IEvictionProvider`.
> 3. **Tags are reserved for non-enumerable fan-out.** The only known case is subject →
>    public course views (G4). The subject decorator also queues the fan-out tag. Tags are not
>    a general eviction currency. As built (G4.1, §5): exactly one tag — the tenant-wide
>    `courses:views` — carried by the public-view read entry only.
> 4. The queue is scope-local and dual. Two EF Core interceptors in
>    `AlphaZero.Shared/Infrastructure/UnitOfWork.cs` flush it at DB commit points —
>    `EvictionSaveChangesInterceptor` (standalone saves; discards standalone failures) and
>    `EvictionTransactionInterceptor` (commit → flush, rollback → discard, ambiguous
>    commit-path failure → flush). An ambient `System.Transactions` transaction fails fast.
>    There is NO MediatR eviction behavior and no staging state. Apologies to past readers:
>    this item used to describe `CacheEvictionFlushBehavior`, which is deleted.
>
> No change tracker and no lookups: `IEvictionStrategy`, `IEntityEvictionPolicy`,
> `ChangeTrackerEvictionStrategy`, `CourseEvictionPolicy`, `UnitOfWork.CollectAsync`, any
> `Evict(entity)` dispatch, and any database read inside eviction do not exist and must not
> be reintroduced. The aggregate argument is the only eviction input.
>
> Open for the next AI: authoring the fan-out tag list (§5) and the lookup grilling agenda
> (§6, G4 first). Do not write tag code outside the narrowed fan-out row before those close.

*Sections G1–G12 are closed — the full answers with evidence live in §11; the as-built tag list
is in §5 "Confirmed cells".*

This file started as plan-only input for the next AI session; Phases 0–3 of it are now
implemented and committed (see §8 status marks and `spec-33/spec-33-caching-handoff.md`).

---

## 0. Handoff summary (read this first)

You are continuing issue #33 on branch `spec/33-caching`.

1. Read §2 (verified facts) and §4 (architecture). Do not reintroduce a change-tracker scan,
   an `Evict(entity)` dispatch, or a database lookup in the eviction path.
2. Add read caching to the four repository decorators (§4.5): read-through with static
   `CourseCacheKeys` `*Entity` functions. Keep the `Cached{Course,Subject}QueryService`
   projection caching unchanged.
3. Author the eviction-key table in §5 and the fan-out tag list (the only tags in the Courses
   path). Every key must be produced by a static `CourseCacheKeys` function; no raw strings.
4. Run the grilling agenda in §6. Close G4 (subject fan-out / the lookup issue) and G1–G3
   (asset ownership / parent moves) with file/line evidence before writing the key/tag rows
   that depend on them. Close G11/G12 for the read-caching rules.
5. Fix the handler bypass in §4.7 (`AddSectionCommandHandler` currently does not call
   `Update(course)`), then run the tests in §10.
6. Update the docs listed in §8.5; several still describe the deleted change-tracker guard.

Hard rules: commands and handlers stay cache-agnostic; no cache logic in the Application
layer; no key can be built without an owning tenant; no caller paging/search/sort/page value
enters a key; eviction uses only the aggregate argument — no ChangeTracker, no lookups.

---

## 1. Review context

| Item | Value |
|---|---|
| Worktree | `/home/azero/.local/share/opencode/worktree/adfa55/spec-33-caching` |
| Branch | `spec/33-caching` |
| HEAD | `01d094a2` (caching docs commit) plus uncommitted decorator work |
| Pre-#33 base | `5947b942` |
| Spec | GitHub issue #33, repo `Azero77/alpha-zero` |
| Conventions | `docs/agents/per-issue-runbook.md` §3 row #33, `docs/CachingArchitecture.md` |

Review scope command:

```bash
git diff 5947b942...HEAD -- \
  src/alphazero-api/Modules/Courses/Infrastructure/Caching \
  src/alphazero-api/Modules/Courses/CACHING.md \
  src/alphazero-api/Modules/Courses/Infrastructure/DependencyInjection.cs \
  src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching \
  src/alphazero-api/AlphaZero.Shared/Infrastructure/UnitOfWork.cs \
  tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs \
  tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching
```

Files that matter at HEAD:

- `Modules/Courses/Infrastructure/Caching/CachedCourseQueryService.cs`
- `Modules/Courses/Infrastructure/Caching/CachedSubjectQueryService.cs`
- `Modules/Courses/Infrastructure/Caching/CourseCacheKeys.cs`
- `Modules/Courses/Infrastructure/Caching/Caching{Course,CourseOverview,CourseAsset,Subject}RepositoryDecorator.cs`
- `Modules/Courses/Infrastructure/Caching/CourseCacheTags.cs` (to be deleted)
- `Modules/Courses/Infrastructure/Caching/CoursesEvictionCoverageInterceptor.cs` (recommended delete, §4.9)
- `Modules/Courses/Application/Courses/Commands/AddSection/AddSection.cs`
- `Modules/Courses/Application/Courses/Commands/UpsertOverview/UpsertCourseOverview.cs`
- `AlphaZero.Shared/Infrastructure/Caching/{ICachingService,HybridCachingService,IEvictionProvider,ScopedEvictionProvider,CacheEvictionFlushBehavior,EvictionFlushScope,CacheKey,CacheScope,CachePolicy}.cs`
- `AlphaZero.Shared/Infrastructure/UnitOfWork.cs`
- `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/*`
- `tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs`

What this review did **not** cover: the #29/#32 chain beyond caching interactions, a full
build/test run of every project, production Redis sizing (there is no Redis), or latency
benchmarking.

---

## 2. Verified facts at HEAD

*Captured 2026-10-27, pre-implementation. F9/F11/F15 describe the pre-pivot state they were
meant to replace; §4.2, §4.7 and §5 are the as-built truth.*

Every row was checked against the source on 2026-10-27. Re-derive only if the branch moves.

| # | Fact | Evidence |
|---|---|---|
| F1 | The shared caching seam exists: `ICachingService`, `HybridCachingService`, typed `CacheKey`/`CacheTag`, `CacheScope`, `CachePolicy`. | `AlphaZero.Shared/Infrastructure/Caching/` |
| F2 | Module code no longer calls `HybridCache`; only comments mention it. | `grep -rn "HybridCache" src/alphazero-api/Modules` returns comments only |
| F3 | No distributed cache is registered; caching is L1-only. | `AddHybridCache` without `AddStackExchangeRedisCache`; `docs/CachingArchitecture.md` |
| F4 | The `AddHybridCache` options lambda now mutates the options (10 min absolute / 5 min local) instead of reassigning it. | `AlphaZero.Shared/Infrastructure/DependencyInjection.cs:76-83` |
| F5 | Ambient tenant context and MassTransit consume/publish filters exist; the three video lifecycle events carry `TenantId`. | `AmbientTenantContext`, `TenantConsumeFilter`, `TenantPublishFilter`; `VideoUploading/IntegrationEvents/Events.cs` |
| F6 | Four Courses write decorators exist and are registered with Scrutor: course, overview, asset, subject. | `Modules/Courses/Infrastructure/DependencyInjection.cs:49-57` |
| F7 | The change-tracker machinery is already deleted: no `IEvictionStrategy`, `IEntityEvictionPolicy`, `ChangeTrackerEvictionStrategy`, or `CourseEvictionPolicy` in source. | `grep` returns no source hits |
| F8 | `UnitOfWork.SaveChangesAsync` drains domain events and saves; it has no caching dependency. | `AlphaZero.Shared/Infrastructure/UnitOfWork.cs:21-48` |
| F9 | `CacheEvictionFlushBehavior` is registered as the outermost Courses behavior and flushes only on the outermost successful dispatch. | `Courses/Infrastructure/DependencyInjection.cs:88-97` |
| F10 | Read decorators use static `CourseCacheKeys` functions and pass tags to `ICachingService`. | `CachedCourseQueryService.cs:33-127`, `CachedSubjectQueryService.cs:25-30` |
| F11 | Write decorators currently queue **tags** from `CourseCacheTags`, not keys. This is the main conversion in this plan. | `CachingCourseRepositoryDecorator.cs:69-70`; `CourseCacheTags.cs` |
| F12 | `CourseCacheKeys` already defines the seven read keys: staff catalog, student catalog, staff view, public view, overview, readiness, subject list. | `CourseCacheKeys.cs:27-50` |
| F13 | The four write aggregates carry the scope needed for keys: `Course.TenantId/Id`, `CourseOverview.TenantId/CourseId`, `CourseAsset.TenantId/CourseId`, `Subject.TenantId`. | Domain aggregates |
| F14 | A course asset is single-owner: assignment rejects an asset whose `CourseId` differs from the target course, and `CourseId` has a private setter with no move method. | `AssignAssetToCurriculum.cs:40-41`; `CourseMediaAsset.cs:28` |
| F15 | `AddSectionCommandHandler` mutates the tracked `Course` but never calls `_courseRepository.Update(course)`. This bypasses decorator eviction and contradicts `CACHING.md:57-60`. | `AddSection.cs:32-44`; `CACHING.md:57-60` |
| F16 | `UpdatePlan`, `RemovePlan`, both reorder handlers, `AddPlan`, section add/rename/delete, item commands, state commands, asset assign/unassign, `UpdateCourse`, `DeleteCourse`, subject create/update/delete and overview upsert all call a decorated repository write. | `grep -rn "Repository\\.(Add\\|Update\\|Remove)" Modules/Courses/Application` |
| F17 | `UpsertCourseOverviewCommand` is `ICommand<Success>` and writes through `ICourseOverviewRepository`, so the UnitOfWork persists it and the overview decorator can evict. | `UpsertCourseOverview.cs:11-15,54,67` |
| F18 | Subject deletion is blocked while any live course references the subject; a rename is allowed. | `DeleteSubject.cs:32-36` |
| F19 | Catalog DTO carries `SubjectName`, `CoverImageDocumentId`, `SectionCount`, `ItemCount`, `TotalDurationSeconds`, `Plans`. Public view adds sections/items/description and embeds the same summary fields. Overview owns `CoverImageDocumentId`; `VideoCourseAsset.Duration` feeds catalog and public-view duration. | `ListCourseCatalog.cs:28-39`; `CourseQueryService.cs:138-150,199-228`; `CourseOverview.cs:16` |
| F20 | `CoursesEvictionCoverageInterceptor` is the only remaining `ChangeTracker` read in the caching path. It warns only; it derives no keys and never queries. | `CoursesEvictionCoverageInterceptor.cs:56-78` |
| F21 | Tenant isolation is now asserted across all seven cached surfaces; non-default paging/search bypass is tested. | `CoursesCacheTests.cs:478-527`, `:245-310` |
| F22 | The asset-available consumer test publishes through a null static tenant and relies on the consume filter's ambient tenant. | `CoursesCacheTests.cs:419-474` |

---

## 3. Findings and status

Severity is reviewer judgement. "Resolved" means the current branch already satisfies the
finding or the design pivot removes it; "Open" means this plan still requires work.

| ID | Severity | Status | Summary |
|---|---|---|---|
| H1 | High | Resolved in design | Background asset writes derive the tenant from `CourseAsset.TenantId`; per-course readiness keys can now be computed without `ITenantProvider`. Residual: confirm the empty-`CourseId` case (G2). |
| H2 | High | **Open — grilling** | Subject rename affects `SubjectName` in every public course view; the `Subject` aggregate cannot enumerate course ids. This is the only non-enumerable key family (§6 G4). |
| H3 | High (design) | Resolved | Shared `ICachingService` seam exists and modules no longer reference `HybridCache`. |
| M1 | Medium | **Open — key table** | Catalog/view duration, cover image and plans must be evicted by the right decorators. The current tag table is incomplete (asset writes do not queue catalog; overview writes do not queue catalog/view/readiness). The key table in §5 fixes this. |
| M2 | Medium | Resolved | Overview upsert is an `ICommand<Success>` and writes through the decorated overview repository. |
| M3 | Medium | Open — tests | Freshness coverage is still partial; §10 completes the matrix. |
| M4 | Medium | Resolved | Tenant isolation covers all seven surfaces. |
| M5 | Medium (design) | Resolved | The 20-case switch is gone; decorators own eviction. Residual: handler bypass (M6). |
| M6 | **High** | **Open — code gap** | `AddSectionCommandHandler` does not call `Update(course)`, so adding a section does not evict. The existing integration test `CourseView_AddSection_EvictsWarmedTree` expects eviction and will fail until fixed. |
| M7 | Medium | **Open — queue change** | `IEvictionProvider` and `ScopedEvictionProvider` queue `CacheTag` only; this plan converts them to a dual queue: `CacheKey` (static functions) plus `CacheTag` (fan-out only). |
| M8 | Medium | **Open — conversion** | The four write decorators call `CourseCacheTags` only; they must queue key sets built from the static `CourseCacheKeys` functions directly, narrow tags to the fan-out row, and gain read-through caching (new in this pivot). |
| L1 | Low | Resolved | `CacheScope` throws instead of writing a `none` tenant; no tenant sentinel remains. |
| L2 | Low | Open — docs | `CACHING.md`, `docs/caching/README.md` and `docs/CachingArchitecture.md` still describe tags and claim `AddSection` calls `Update`. |
| L3 | Low–Medium | Resolved | Staff `/courses` caching is declared in scope (D3=A). |
| L4 | Low | Open — tests | Check and remove any unused helper / duplicated assertion while touching `CoursesCacheTests`. |
| P1 | High (platform) | Resolved | The `AddHybridCache` lambda is fixed and covered by `SharedCachingRegistrationTests`. |
| P2 | Medium (platform) | Documented | L1-only; Redis/backplane is a `TODOS.md` follow-up, not part of #33. |
| P3 | Medium (design) | **Open** | `CourseCacheTags` is narrowed to the fan-out tag only; tag-carrying Courses read entries are removed except where the fan-out tag attaches; keys are the eviction currency for every enumerable surface. |

---

## 4. Architecture: decorator-owned caching with key + fan-out-tag eviction

### 4.1 Principle

1. Reads and writes share **one key registry**: `CourseCacheKeys`.
2. The **repository decorators own the cache logic on the repository surface**:
   - **Reads** (single-entity reads) are cached through `ICachingService.GetOrCreate` with a
     static `CourseCacheKeys` entity key. The existing projection caching in
     `Cached{Course,Subject}QueryService` is kept; decorator read caching is additive. G11/G12
     define the exact read methods and the tenant source.
   - **Writes** (`Add`/`Update`/`Remove`) are never cached; they queue eviction instead.
3. A write decorator computes the exact keys affected by the aggregate it receives and queues
   them in `IEvictionProvider`.
4. **Tags exist only for non-enumerable fan-out.** The only known case is subject → public
   course views (G4). The subject decorator queues the fan-out tag; no other decorator queues
   a tag. The next AI authors the tag list (§5/§6).
5. The queue is scope-local and dual: two EF Core interceptors in
   `AlphaZero.Shared/Infrastructure/UnitOfWork.cs` flush it at DB commit points —
   `EvictionSaveChangesInterceptor` (standalone saves) and `EvictionTransactionInterceptor`
   (explicit EF transactions). There is NO MediatR eviction behavior (superseded; handoff §1.3),
   no staging states and no `ConfirmSave()`.
6. No component inspects the EF change tracker, resolves a parent through the database, or
   dispatches on an entity type to find tags. The aggregate argument is the only eviction
   input.
7. Over-eviction is acceptable (remove an extra key is a cache miss); under-eviction is a bug.
8. If a key cannot be derived from the aggregate argument, stop and settle it in §6. Do not
   add a lookup or a second mechanism.

### 4.2 Call graph

```text
READ PATH
query service / command handler
   │  _courseRepository.GetCourseByIdAsync(id)
   ▼
Caching<Course>RepositoryDecorator             (Courses infrastructure)
   │  scope = CacheScope.ForTenant(<ambient tenant>)        ← same source as query services
   │  key   = CourseCacheKeys.CourseEntity(scope, id)       ← static function
   │  ICachingService.GetOrCreate(key, inner read)
   ▼
inner repository → DbSet read                 (only on a cache miss)

WRITE PATH
command handler (cache-agnostic)
   │  course.Update...(); _courseRepository.Update(course)
   ▼
Caching<Course>RepositoryDecorator            (Courses infrastructure)
   │  scope = CacheScope.ForTenant(course.TenantId)
   │  keys  = CourseCacheKeys.EvictCourse(scope, course.Id) ← static functions
   │  _eviction.Evict(keys)                  (+ EvictTag only in the subject decorator)
   ▼
inner repository → DbSet write
   ▼
UnitOfWork.SaveChangesAsync                    (no caching dependency; drains domain events)
   ▼
DbContext.SaveChangesAsync  — the flush machinery lives here, in the EF pipeline
   │
   ├─ No EF transaction (standalone save)
   │    EvictionSaveChangesInterceptor (UnitOfWork.cs)
   │      SavedChangesAsync → provider.FlushAsync()
   │      SaveChangesFailedAsync → provider.Discard()   (burn exactly the failed save's keys)
   ▼
Explicit EF transaction (TransactionalBehavior CommitAsync; retry-strategy internal txns land
here too, because EnableRetryOnFailure wraps saves in internal transactions)
   EvictionTransactionInterceptor (UnitOfWork.cs)
   TransactionCommittedAsync      → provider.FlushAsync()   (ONE flush for the whole transaction)
   TransactionRolledBack          → provider.Discard()
   TransactionFailedAsync         → commit-path failure → FlushAsync (ambiguity = over-eviction);
                                    other failures  → Discard()
   ▼
ICachingService.RemoveAsync(key) for each queued key
ICachingService.RemoveByTagAsync(tag) for each queued tag
```

The flush machinery is **the two EF interceptors in `UnitOfWork.cs`, not a MediatR behavior**.
There is no depth gating: a transaction ends with exactly one flush; a standalone save flushes
at its own save point. Failure rows: standalone-save-failure → discard, ambient → throw,
commit-ambiguity → flush (§4.8).

Bus path is unchanged from D5=A:

```text
queue message (TenantId)
  → TenantConsumeFilter → AmbientTenantContext → ITenantProvider
  → Courses consumer → Send(command)
  → the same decorator/pipeline; the decorator still uses the entity's own TenantId
```

### 4.3 Contracts to change

The queue carries keys and tags — keys for every enumerable surface, tags only for fan-out.

```csharp
public interface IEvictionProvider
{
    bool IsActive { get; }
    bool HasPendingKeys { get; }          // renamed from HasPendingTags
    void Begin();
    void Evict(CacheKey key);
    void Evict(IEnumerable<CacheKey> keys);
    void EvictTag(CacheTag tag);          // fan-out only; the Courses path uses it once
    Task FlushAsync(CancellationToken cancellationToken);
    void Discard();
}
```

`ScopedEvictionProvider` (as built):

- Two `HashSet` lanes on the instance — the `CacheKey` set and the `CacheTag` set (a
  `HashSet` gives dedupe for free) — guarded by a private lock so queuers may queue from any
  thread inside the scope while `FlushAsync` snapshots-and-clears atomically; the cache I/O
  awaits run outside the lock.
- The lanes are **request-scoped instance state, not `AsyncLocal`**. The EF interceptor fires
  from the MediatR pipeline level that called `SaveChangesAsync` — an ANCESTOR context of the
  command handler in which the decorators queued — and AsyncLocal writes never flow back up to
  an ancestor context (proven live: every `FlushAsync` logged `keys=0 tags=0` under the
  AsyncLocal design). Decorators, the unit of work, the DbContext and both interceptors resolve
  the same scoped instance, and one `IModule.Send` creates one child scope — so one scope is
  exactly one queue; a disposed scope drops its queue with it.
- `FlushAsync` snapshots and clears both queues in place, then calls
  `ICachingService.RemoveAsync(key, cancellationToken)` for each key and
  `ICachingService.RemoveByTagAsync(tag, cancellationToken)` for each tag.
- `Discard` clears both queues.

`ICachingService` keeps `RemoveByTagAsync`; the Courses path now reaches it only through the
eviction flush, for the fan-out tag. `GetOrCreateAsync`/`SetAsync` keep the tag parameter, but
Courses read entries pass `[]` — only the public-view entry carries the one fan-out tag
(`CourseCacheTags.CoursePublicViews`, `CachedCourseQueryService.cs:86`). `CourseCacheKeys`
exposes no tag builders; tag building lives in the narrowed `CourseCacheTags` (fan-out row only).

### 4.4 Static key functions are the single registry

Read decorators already call these functions (`CourseCacheKeys.CourseView(scope, id)`, etc.).
The repository decorators call the same functions through small key-set helpers in the same
static class, plus one entity key per aggregate family for the read-through path. The helpers
are the only place that knows which surfaces a write affects.

```csharp
internal static class CourseCacheKeys
{
    // Existing read functions stay as-is:
    // StaffCatalog(scope), StudentCatalog(scope), CourseView(scope, courseId),
    // CoursePublicView(scope, courseId), CourseOverview(scope, courseId),
    // CourseReadiness(scope, courseId), SubjectList(scope)

    // New entity keys for the decorator read-through path (§4.5):
    // CourseEntity(scope, courseId), CourseOverviewEntity(scope, courseId),
    // CourseAssetEntity(scope, courseId), SubjectEntity(scope, subjectId)

    public static IReadOnlyCollection<CacheKey> EvictCourse(CacheScope scope, Guid courseId) =>
    [
        StaffCatalog(scope),
        StudentCatalog(scope),
        CourseView(scope, courseId),
        CoursePublicView(scope, courseId),
        CourseOverview(scope, courseId),
        CourseReadiness(scope, courseId),
        CourseEntity(scope, courseId)         // decorator read cache
    ];

    public static IReadOnlyCollection<CacheKey> EvictCourseOverview(CacheScope scope, Guid courseId) =>
    [
        CourseOverview(scope, courseId),
        CourseReadiness(scope, courseId),     // HasCoverImage
        CoursePublicView(scope, courseId),    // CoverImageDocumentId
        StaffCatalog(scope),                  // CoverImageDocumentId
        StudentCatalog(scope),                // CoverImageDocumentId
        CourseOverviewEntity(scope, courseId) // decorator read cache
    ];

    public static IReadOnlyCollection<CacheKey> EvictCourseAsset(CacheScope scope, Guid courseId) =>
        courseId == Guid.Empty
            ? []
            : [
                CourseReadiness(scope, courseId),   // asset State
                CoursePublicView(scope, courseId),  // resource duration
                StaffCatalog(scope),                // TotalDurationSeconds
                StudentCatalog(scope),              // TotalDurationSeconds
                CourseAssetEntity(scope, courseId)  // decorator read cache
            ];

    public static IReadOnlyCollection<CacheKey> EvictSubject(CacheScope scope, Guid subjectId) =>
    [
        SubjectList(scope),
        StaffCatalog(scope),      // SubjectName
        StudentCatalog(scope),    // SubjectName
        SubjectEntity(scope, subjectId)
        // Public views: tag fan-out only — see §6 G4.
    ];
}
```

The fan-out tag lives in `CourseCacheTags`, narrowed to that single row; the next AI authors
it after closing G4:

```csharp
internal static class CourseCacheTags
{
    // The only remaining tag builder in Courses (G4). Attached to the public-view read
    // entries and queued by the subject decorator. Name and granularity to be confirmed in §6.
    public static CacheTag CoursePublicViews(CacheScope scope) => ...;
}
```

The exact rows are provisional. §5 is where the next AI confirms each cell against the
projections; §6 is where the open cells are grilled.

### 4.5 Decorator rules — the decorator owns the cache logic

The `Caching<Aggregate>RepositoryDecorator` handles both directions. Old read-caching /
eviction logic is removed from wherever it lives today and replaced by the rules below; no
new mechanism is added beyond them.

**Read methods (read-through, as resolved by G11/G12):**

1. Build `CacheScope.ForTenant(<ambient tenant>)` the same way the existing read decorators
   in the query services already do (there is no aggregate argument on a read; G11 settled
   the tenant source).
2. Wrap cacheable reads in `ICachingService.GetOrCreate` with the static entity key from
   `CourseCacheKeys` (§4.4). As built, exactly ONE repository read is cacheable:
   `ICourseOverviewRepository.GetByCourseIdAsync` — a flat, STJ-safe DTO — under
   `CourseOverviewEntity`. All aggregate `GetById`-shaped reads delegate UNCACHED (G12:
   the aggregate is a tracked-identity bridge).
3. Never cache collection reads whose shape depends on caller paging/search/sort — they
   bypass to the inner repository, exactly as non-default projections already do.
4. Serialization safety decided the G11/G12 boundary: aggregates are tracked-identity
   bridges (and many have private constructors/collections), so no aggregate graph is cached;
   a new cacheable read needs a flat, write-coupled DTO plus its `*Entity` key queued by every
   write family that feeds it.

**Write methods (`Add`/`Update`/`Remove`):**

Each method:

1. Builds `CacheScope.ForTenant(entity.TenantId)`.
2. Queues `entity.TenantId == Guid.Empty ? [] : keySet(scope, entity.Id)`, plus the fan-out
   tag in the subject decorator only.
3. Delegates to the inner repository.
4. Decorator-cached reads are invalidated by the same queue: each write's key set contains
   the entity keys (§4.4), so no separate read-invalidation path exists.

Example (the one as-built read-through, `CachingCourseOverviewRepositoryDecorator`):

```csharp
public async Task<CourseOverviewDto?> GetByCourseIdAsync(Guid courseId, CancellationToken ct)
{
    var scope = CacheScope.ForTenant(_tenantProvider.GetTenant());      // ambient source (F5)
    if (!scope.IsEnabled)
        return _inner.GetByCourseIdAsync(courseId, ct);

    return await _cache.GetOrCreateAsync(
        CourseCacheKeys.CourseOverviewEntity(scope, courseId),
        CourseCacheKeys.EntityPolicy,
        [],
        token => _inner.GetByCourseIdAsync(courseId, token), ct);
}

public void Update(CourseOverview overview)
{
    var scope = CacheScope.ForTenant(overview.TenantId);   // never ITenantProvider on writes
    if (scope.IsEnabled)
        _eviction.Evict(CourseCacheKeys.EvictCourseOverview(scope, overview.CourseId));

    _inner.Update(overview);
}
```

Rules:

- Queue before delegating. If the inner write throws, the outer behavior discards the queue.
- Never use `ITenantProvider` in a write decorator; the aggregate owns the tenant.
- Never use `Guid.Empty` as a key segment; `CacheScope` already rejects it.
- Never construct key strings in a decorator; call `CourseCacheKeys` only.
- No `Evict(entity)` dispatch, no change-tracker read; the aggregate argument is the only
  eviction input.
- Tags are queued only where keys cannot enumerate the affected family (currently only the
  subject decorator via G4); no other decorator queues a tag.
- `CourseAsset` has TPH subtypes; the decorator only needs the base `TenantId`/`CourseId`,
  so no type switch is involved.

### 4.6 The no-lookup rule

The decorator may read only the aggregate argument. Consequences:

- **Course** → `TenantId` + `Id` are on the aggregate.
- **CourseOverview** → `TenantId` + `CourseId` are on the aggregate.
- **CourseAsset** → `TenantId` + `CourseId` are on the aggregate. Fact F14 makes the asset
  single-owner, so one per-course key set is complete. If that ever changes, see G1/G2.
- **Subject** → `TenantId` is on the aggregate. The subject list and both catalogs are
  tenant-wide keys and can be removed exactly. Public course views cannot: they are per
  course, and the subject does not carry course ids. This is the one permitted exception and
  must be settled by G4.
- **Children (sections, items, resources, plans)** → every write goes through
  `ICourseRepository.Update(course)`, so the Course decorator covers them. No child
  decorator and no parent lookup.

If a write path cannot satisfy this rule, change the write path (load and update the scope-
bearing aggregate) or over-evict a tenant-wide key set. A database lookup inside eviction is
not an option: it defeats the cache and reintroduces the cost the decorator design removed.
The same rule applies on the read side: a decorated read may not fetch extra data just to
build its key; the entity key is built from the id and tenant alone.

### 4.7 Handler contract and bypass audit

Every command that can change a cached projection must call a decorated repository write.
Current audit (F16):

| Handler / area | Decorated write | Status |
|---|---|---|
| `CreateCourse` | `ICourseRepository.Add` | OK |
| `UpdateCourse`, `DeleteCourse` | `ICourseRepository.Update` | OK |
| `SectionCommands` (add/rename/delete) | `Update(course)` | OK |
| `AddSection` | `Update(course)` | OK — fixed in Phase 3 (`AddSection.cs:41`); its regression test `CourseView_AddSection_EvictsWarmedTree` passes. Phase 3 also re-committed all three mask-mutation paths in `CourseActiveItemsDomainEventHandler` with explicit `Update` |
| `CourseSectionItemCommands` | `Update(course)` | OK |
| `ReorderCommands` (sections/items) | `Update(course)` | OK |
| `AddPlan`, `UpdatePlan`, `RemovePlan` | `Update(course)` | OK |
| `CourseStateCommands` | `Update(course)` | OK |
| `AssignAssetToCurriculum`, `UnassignAssetFromCurriculum` | `Update(course)` | OK |
| `CreateVideoCourseAsset` | `IRepository<CourseAsset>.Add` | OK |
| `MarkCourseAssetAvailable`, `MarkCourseAssetFailed`, `DismissAsset`, `SyncResourceMetadata` | `IRepository<CourseAsset>.Update` | OK |
| `UpsertCourseOverview` | `ICourseOverviewRepository.Add/Update` | OK |
| `CreateSubject`, `UpdateSubject`, `DeleteSubject` | `ISubjectRepository.Add/Update` | OK |
| `EnrollInCourse`, enrollment commands | none | OK — enrollment surfaces are uncached |

The `AddSection` gap is a real stale-cache bug, not a test-only concern. Fix it in the same
change as the key conversion.

### 4.8 Failure semantics (as built — the §1.3 handoff contract)

Flush points have no depth gating. The rows below replace the old outermost-behavior table;
there is no MediatR eviction behavior and no `EvictionFlushScope`.

| Situation | Queue | Cache call | Caller observes |
|---|---|---|---|
| Standalone save succeeds | Snapshot, cleared in place | `RemoveAsync` per key + `RemoveByTagAsync` per tag | Normal result |
| Standalone save fails | `SaveChangesFailedAsync` → `Discard()` | None | The failure; nothing evicted |
| Saves inside an explicit EF transaction | Kept through intermediate saves | None | Commit interceptor decides |
| EF transaction commits | ONE flush for the whole transaction | `RemoveAsync` per key + `RemoveByTagAsync` per tag | Normal result |
| EF transaction rolls back | `Discard()` | None | Rollback; nothing evicted |
| `TransactionFailed` on the commit path | Flushed defensively | `RemoveAsync`/`RemoveByTagAsync` | Ambiguous commit → over-eviction only (never stale) |
| `TransactionFailed` otherwise (e.g. rollback failure) | `Discard()` | None | The failure |
| Save inside an ambient `System.Transactions.Transaction` | Guard throws in `SavedChangesAsync` | None | `InvalidOperationException` — fail fast; an ambient commit has no EF hook, so queued keys could never be flushed |
| Cache I/O cancels | Cleared before calls | `OperationCanceledException` rethrown | Cancellation propagates (the write already committed) |
| Cache I/O throws | Cleared before calls | Logged and swallowed | Normal result; entries age out by TTL |
| Handler error result before any save | Keys remain queued; dropped with the request scope | None | Nothing evicted (safe no-op) |

Belt-and-braces for the ambient row: `DatabaseSettings.cs:26-28` appends `Enlist=false` to every
connection string that lacks it, so an opt-in `TransactionScope` cannot silently enlist. Today
grep proves no `TransactionScope` usage exists in `src/`.

A flush failure after commit must never turn a successful write into a failed request.

### 4.9 Delete the coverage interceptor — DONE (Phase 2)

`CoursesEvictionCoverageInterceptor`, its DI registration and
`CoursesEvictionCoverageInterceptorTests` were deleted in Phase 2 (commit `72260467`); the
§10 verification grep confirms zero hits. It was the last `ChangeTracker` read in the caching
path, and it could only warn.

Replaced by the handler audit (§4.7), the decorator unit tests (§10.1) and the
mutation-family integration tests (§10.2). `HasPendingTags` is gone from the provider API;
tags are queued without a pending flag via `EvictTag` (the subject decorator is the only
Courses call site).

Tradeoff on record: a future handler that forgets `Update(course)` is caught by tests and
review, not at runtime. That is acceptable because the write surface is small (four
repositories) and the audit is explicit. If the team later wants a runtime alarm, it must not
use `ChangeTracker`; that would be a new design question, not part of this plan.

### 4.10 Deleted or narrowed by this design

- `Modules/Courses/Infrastructure/Caching/CoursesEvictionCoverageInterceptor.cs` (and its test)
- Tag builders in `CourseCacheKeys`: `CatalogTag`, `SubjectsTag`, `CourseViewsTag`,
  `CourseAssetsTag`
- Tag arguments on Courses read entries — except the public-view entries, which carry the one
  fan-out tag once G4 is closed
- `Modules/Courses/Infrastructure/Caching/CourseCacheTags.cs` is **narrowed, not deleted**: it
  keeps only the G4 fan-out tag builder
- `ScopedEvictionProvider` keeps a tag queue, widened to keys; in the Courses path the tag
  queue accepts only the fan-out tag
- `IEvictionProvider.HasPendingTags` (replaced by `HasPendingKeys`; tags use `EvictTag`)

Already deleted and must not return: `IEvictionStrategy`, `IEntityEvictionPolicy`,
`ChangeTrackerEvictionStrategy`, `CourseEvictionPolicy`, `CollectAsync` in `UnitOfWork`, any
`Evict(entity)` dispatch, and any database read inside eviction.

---

## 5. Eviction key + tag table (draft for the next AI to author/confirm)

Each row must be confirmed against the projections and the listed evidence. "Static keys" means
the functions in `CourseCacheKeys`; no literal strings. Tags are authored by the next AI and
appear only where marked.

| Decorator write | Aggregate inputs | Static keys queued | Tags queued | Why | Open cell |
|---|---|---|---|---|---|
| `ICourseRepository` `Add`/`Update`/`Remove(course)` | `TenantId`, `Id` | `StaffCatalog`, `StudentCatalog`, `CourseView`, `CoursePublicView`, `CourseOverview`, `CourseReadiness`, `CourseEntity` | none | Title/status/plans/counts/duration feed catalog and views; state/plans feed readiness; delete removes the overview | Is evicting the overview key on every course update acceptable over-eviction? Default: yes |
| `ICourseOverviewRepository` `Add`/`Update`/`Remove(overview)` | `TenantId`, `CourseId` | `CourseOverview`, `CourseReadiness`, `CoursePublicView`, `StaffCatalog`, `StudentCatalog`, `CourseOverviewEntity` | none | `CoverImageDocumentId` appears in catalog, public view and readiness; overview content is the overview key | Confirm `CourseView` (staff tree) does not project overview/cover |
| `IRepository<CourseAsset>` `Add`/`Update`/`Remove(asset)` | `TenantId`, `CourseId` | `CourseReadiness`, `CoursePublicView`, `StaffCatalog`, `StudentCatalog`, `CourseAssetEntity` | none | Asset `State` feeds readiness; `VideoCourseAsset.Duration` feeds catalog and public-view durations | Confirm staff tree does not project duration/state; confirm empty `CourseId` assets are not referenced by a cached surface |
| `ISubjectRepository` `Add`/`Update`/`Remove(subject)` | `TenantId`, `Id` | `SubjectList`, `StaffCatalog`, `StudentCatalog`, `SubjectEntity` | **fan-out tag (G4)** | Subject name appears in catalog and public view; subject list changes on create/delete | G4: keys cannot enumerate every course view from the subject; the fan-out tag is the only tag in the Courses path |

Notes for the author:

- Read entries are created only for default-shaped requests; the key functions already encode
  that. Do not add a key variant for search/sort/page.
- The `*Entity` keys serve the decorator read-through path (§4.5). Which repository read
  methods are cacheable, and the tenant source on a read, are G11/G12 authoring work.
- The fan-out tag list is the next AI's authoring task: name, granularity, TTL, and which
  read entries carry it. Until G4 closes, no tag code is written outside the subject
  decorator.
- A key is removed even if it was never created; `HybridCache.RemoveAsync` is cheap and safe.
- `CourseCacheKeys` and the narrowed `CourseCacheTags` are `internal` and live in the
  Infrastructure assembly, so decorators can call them directly.
- The key set for `Add` may be narrower than `Update`/`Remove` (no per-course entry exists yet),
  but a uniform set is simpler and over-eviction is acceptable.
- If G4 chooses the generation fallback instead of the fan-out tag, `CoursePublicView` (and
  `CourseView` if needed) gains a generation segment; the static function signature changes,
  not the decorator rule. The tag is the default recommendation (D13/D21).

**Confirmed cells (as built — Phase 2 deltas over this draft, 2026-10-29):**

| Helper | As built | Delta vs draft |
|---|---|---|
| `EvictCourse(scope, courseId)` | **8 keys**: StaffCatalog, StudentCatalog, CourseView (tree), CoursePublicView, CourseOverview, CourseReadiness, **CourseStatus**, CourseEntity (`CourseCacheKeys.cs:93-103`) | Draft had 7; adds the eviction-only `CourseStatus` key (the status read is deliberately uncached; queuing the key is a defensive no-op until it ever caches) |
| `EvictCourseOverview(scope, courseId)` | **6 keys**: CourseOverview, CourseReadiness, CoursePublicView, StaffCatalog, StudentCatalog, CourseOverviewEntity (`:109-117`) | Matches draft incl. the entity key (prevents a stale cover resurrection: the upsert handler decides add-or-update from the read-through DTO) |
| `EvictCourseAsset(scope, courseId, assetId)` | **5 keys**: CourseReadiness, CoursePublicView, StaffCatalog, StudentCatalog, CourseAssetEntity(assetId) — `[]` for `courseId == Guid.Empty` (`:123-136`) | Draft had 4 + entity; the asset entity key is keyed by **assetId**, so the helper takes both ids; no tree key (confirmed: the staff tree reads only `r.CourseAssetId`, never the asset table — G7) |
| `EvictSubject(scope, subjectId)` | **2 keys**: SubjectList, SubjectEntity (`:140-144`) + the one tag | Draft queued both catalogs too. Dropped: catalogs are 2-min-TTL and evicted by key on every course write; G4.2 accepts their ≤2-min staleness after the rare rename/delete instead of over-evicting two hot entries on every subject write |
| Entity keys | `CourseEntity`, `CourseOverviewEntity`, `CourseAssetEntity`, `SubjectEntity`, TTL 5 m (`EntityPolicy`, `:27`) | Family split per aggregate. Only `CourseOverviewEntity` is actually read-cached today (G11); the other three are eviction-only keys (G12 uncached aggregate reads) |
| Tag (G4.1) | exactly one: `CourseCacheTags.CoursePublicViews(scope)` = tenant-wide `courses:views`; carried by the public-view read entry only (`CachedCourseQueryService.cs:86`); queued by the subject decorator only (`CachingSubjectRepositoryDecorator.cs:76`) | Fan-out tag authored; strictly one family, tenant-granular |

---

## 6. Grilling agenda — the lookup issue

Run this with the next AI — the same session that authors the fan-out tag list (D21) — before
writing the key/tag table rows that depend on the answers. Ask one question at a time. For every
claim demand a file/line reference or a test result. Record each answer in §7/§11. G4 is the
lookup issue the user wants grilled; G11/G12 govern the decorator read-caching rules.

### G1 — Can a `CourseAsset` belong to more than one course?

**Why it matters:** the asset decorator emits one per-course key set from `asset.CourseId`. If
an asset can back resources in several courses, one course id under-evicts the others and the
only alternatives are enumeration/lookup.

**Evidence already checked:** `AssignAssetToCurriculumCommandHandler` rejects
`asset.CourseId != request.CourseId` (`AssignAssetToCurriculum.cs:40-41`); `CourseAsset.CourseId`
has a private setter and there is no move/rebind method (`CourseMediaAsset.cs:28`).

**Ask:** can an asset be reassigned to another course through any other command, migration,
or admin path? If no, confirm the single-owner invariant and add a regression test. If yes,
stop and reopen the design.

**Default:** single-owner, no lookup. The asset decorator is complete.

### G2 — What does an empty `CourseAsset.CourseId` mean?

**Why it matters:** the draft key set emits nothing for `Guid.Empty`. That is correct only if
such an asset is not referenced by any cached projection.

**Evidence already checked:** assignment requires the ids to match, so an empty-course asset
cannot be attached to a course through `AssignAssetToCurriculum`; the course pool is uncached.

**Ask:** which commands create assets with an empty course id, and can any of them be attached
to a course without setting `CourseId`? If yes, decide the owner before caching readiness.

**Default:** empty course id = unassigned pool asset, no cached dependency, no keys. Add a unit
test asserting the decorator queues nothing and does not throw.

### G3 — Can sections, items, resources or plans move between courses?

**Why it matters:** a move reaches the decorator only through the new root `Course`, so the old
course's keys would remain.

**Evidence already checked:** `CourseSection.CourseId` and `CoursePlan.CourseId` are private-set;
no move command was found. Items/resources are owned through the section/item graph.

**Ask:** is there any move/re-parent operation (now or planned)? If yes, the command must load
both courses and call `Update` on both; that still uses aggregate arguments and needs no lookup.
If no, lock the invariant with a test or a domain rule.

**Default:** no moves; the Course decorator is complete. If a move is added later, update both
roots in the same handler.

### G4 — How do we evict every public course view after a subject rename/delete?

This is the only key family that cannot be derived from the aggregate. `Subject` has a tenant id
but no course ids; public view entries are per course and embed `SubjectName`.
`DeleteSubject` is blocked while a live course references the subject, but `UpdateSubject`
(rename) is allowed. Walk the options:

| Option | Mechanism | Pros | Cons |
|---|---|---|---|
| 1. Tenant view generation segment | Public-view key includes a tenant-scoped generation value: `courses:view:{tenant}:g{generation}:{courseId}`. The read decorator reads a cached `courses:view-generation:{tenant}` key; a subject write sets a new generation. | Key-only; no lookup; no tags; old entries become unreachable and expire by TTL. | One extra cached read per view request; a new generation key and lifecycle; slightly more complex key builder. |
| 2. One tenant-wide tag for views | Keep `CacheViewsTag(tenant)` only on public-view entries; subject writes evict by tag. | Smallest correct change; tags already exist in `ICachingService` for Tenants. | Not strictly key-only; one tag survives in Courses; the provider would need both key and tag queues. |
| 3. Accept the TTL | Document that a subject rename is visible within the 10-minute view TTL. | No mechanism. | Contradicts the spec's bounded-freshness promise for a visible field; H2 remains. |
| 4. Look up course ids by subject and remove each key | Query courses by `SubjectId`, build every view key. | Precise. | A database read during eviction; rejected by the no-lookup rule and defeats the cache purpose. |

**Ask:** confirm the fan-out tag (option 2) as the mechanism and pin down its details: the tag
key, which read entries carry it, and the provider's dual queue (keys + tags) and why that is
not a second invalidation framework. Option 1 (generation) is the fallback only if the tag
granularity is rejected. Options 3 and 4 stay rejected — option 4 is a database read during
eviction and defeats the cache.

**Default recommendation (user decision 2026-10-27, 2nd revision):** option 2 — one tenant-scoped
view tag. Keys for everything enumerable; tags only for non-enumerable fan-out. Record the
chosen details.

### G5 — Does subject deletion ever leave cached course entries?

**Why it matters:** deletion is blocked while a live course references the subject (F18). If a
soft-deleted course can still have cached view/overview/readiness entries when its subject is
deleted, those keys need eviction.

**Ask:** can `DeleteSubject` run when only soft-deleted courses reference it? Does course delete
already evict every per-course key? If yes, no extra subject keys are needed. If no, add the
same tenant fan-out decision as G4.

**Default:** course delete evicts every per-course key; subject delete needs only the subject/
catalog keys (and the G4 decision for any remaining views).

### G6 — Which overview fields feed catalog, public view and readiness?

**Why it matters:** the overview decorator's key set depends on it.

**Evidence already checked:** catalog and public view project `CoverImageDocumentId` from
`CourseOverview`; readiness checks `HasCoverImage`; overview content is served by its own key.

**Ask:** confirm there is no other overview field in those projections (title/description
come from `Course`, not the overview). If confirmed, use the draft row. If a field is missed,
add the corresponding key.

**Default:** draft row in §5 (overview, readiness, public view, both catalogs).

### G7 — Which asset fields feed the staff tree?

**Why it matters:** the asset decorator currently emits no `CourseView` (staff tree) key.

**Evidence already checked:** catalog and public view use `VideoCourseAsset.Duration`; readiness
uses `State`. The staff tree projection carries resource ARN/type/order/metadata, not clearly
duration/state.

**Ask:** trace `GetCourseByIdAsync`'s projection. If it can show duration or asset state, add
`CourseView` to the asset key set. If not, leave it out and state why in the test.

**Default:** no staff-tree key for assets; add one only if the projection proves otherwise.

### G8 — Is the handler audit complete?

**Why it matters:** decorator eviction is opt-in. A handler that mutates tracked state without
a repository write is silently stale; the interceptor that used to warn is being deleted.

**Evidence already checked:** all write handlers call a decorated repository except `AddSection`
(F15/F16).

**Ask:** confirm the audit in §4.7 covers every command that mutates `Course`, `CourseOverview`,
`CourseAsset`, `Subject`, section/item/resource/plan; add the missing `Update(course)` in
`AddSection`; add an integration test for it.

**Default:** fix `AddSection`, keep the audit table current in the PR description.

### G9 — Do course soft-delete and subject delete cascade?

**Why it matters:** cascade writes could change entities whose keys were not queued.

**Evidence already checked:** `DeleteCourse` calls `Update(course)` (F16); `DeleteSubject`
blocks while a live course exists (F18).

**Ask:** confirm there is no database-level cascade that writes a cache-feeding entity outside
the decorated repositories. If there is, it must go through the same aggregate write.

**Default:** no hidden cascade; the decorated write is the only path.

### G10 — What remains tagged?

**Why it matters:** tags are now reserved for non-enumerable fan-out, but the Tenants branding
consumer still tags orthogonally (`tenant-branding:{subdomain}`).

**Ask:** confirm the only other `RemoveByTagAsync` call is Tenants branding and is out of
scope. Record `courses:views` (or the authored fan-out tag name) as the single in-Courses
exception and document it.

**Default:** Courses is key-only except the one fan-out tag; Tenants keeps its tag.

### G11 — Which repository read methods are cacheable, and where does the read tenant come from?

**Why it matters:** the decorator read-through cache (§4.5) needs a bounded key space and a
tenant scope. Repository list/query methods shaped by caller parameters would explode the key
space; single-entity reads take ids.

**Ask:** enumerate the read methods of the four repositories; mark single-entity reads as
cacheable and parameterized collection reads as bypass (same rule as D11 for default shapes).
Confirm the tenant source for a read — the working assumption is the same ambient tenant
context the query services use (F5). Decide whether caching aggregates double-stores data
alongside the projection cache (accepted tradeoff), and whether serializing
`Course`/`CourseOverview`/`CourseAsset`/`Subject` graphs is safe; if a graph cannot be cached
safely, that read delegates directly.

**Default:** single-entity reads cached with the `*Entity` keys; collection reads bypass;
ambient tenant; the query-service projection caching stays unchanged.

### G12 — Does decorator read caching break tracked-entity writes?

**Why it matters:** command handlers load aggregates through decorated repository reads. If
`GetOrCreate` returns a serialized/detached copy while the inner repository and EF expect a
tracked instance, mutating that copy would silently do nothing — a correctness landmine that
must not be "fixed" with a change-tracker scan.

**Ask:** for every write handler, verify which instance it mutates (tracked load vs cached
copy) and whether handing a cached instance to the inner repository's `Update` is correct. If a
cached-vs-tracked mismatch appears, skip caching the reads that precede writes in that flow, or
re-attach explicitly — and if re-attachment is needed, it belongs in the decorator, done
explicitly, never via `ChangeTracker` inspection.

**Default:** cache only the reads that are proven safe (per G11's enumeration); tracked writes
keep using a repository write path, and the audit in §4.7 stays the enforcement.

---

## 7. Decision register

Final state of the decisions that matter for implementation.

| # | Decision | Final answer | Consequence |
|---|---|---|---|
| D1 | Shared `ICachingService` vs direct `HybridCache` | A — shared seam (done) | Modules depend on `ICachingService`; only Shared references `HybridCache` |
| D2 | Overview command | B — fix persistence in this work (done) | `UpsertCourseOverviewCommand` is `ICommand<Success>` and writes through the decorated repository |
| D3 | Staff `/courses` caching | A — in scope; issue and docs amended | Default-page staff catalog key is cached and evicted by writes |
| D4 | Eviction ownership | Write-side repository decorators (done) | No change-tracker strategy, no entity policy, no `Evict(entity)` |
| D5 | Bus-scope tenant | A — ambient tenant + consume/publish filters (done) | Events carry `TenantId`; decorators still use the entity's tenant |
| D6 | Redis/L2 | A — defer, L1-only, `TODOS.md` | Docs must not claim Redis |
| D7 | `AddHybridCache` lambda | C — fix in this work (done) | 10 min absolute / 5 min local defaults are applied and tested |
| D8 | Key representation | Typed `CacheKey`/`CacheTag` (done); Courses uses `CacheKey` everywhere and `CacheTag` only for the fan-out | No raw strings in Courses |
| D9 | Test host Redis | No container; L1 behavior tests | — |
| D10 | Docs | Update after code | `CACHING.md`, caching README, architecture doc |
| D11 | Catalog key cardinality | Default first page only (done) | No search/sort/page/perPage in keys |
| D12 | Flush point | **Flush at the EF save/commit interceptors in `UnitOfWork.cs` — no staging states, no depth gating** (supersedes the outermost-behavior design: handoff §1.3) | `EvictionSaveChangesInterceptor` flushes standalone saves and discards standalone failures; `EvictionTransactionInterceptor` flushes at commit, discards on rollback, flushes an ambiguous commit-path failure; the ambient-transaction guard throws (D24) |
| D13 | Eviction currency | **Keys for enumerable surfaces; tags only for non-enumerable fan-out** (user decision 2026-10-27, 2nd revision) | `IEvictionProvider` dual queue: `Evict(CacheKey)` + `EvictTag(CacheTag)`; `FlushAsync` calls `RemoveAsync` then `RemoveByTagAsync` |
| D14 | Key construction | **Static `CourseCacheKeys` functions only** | Decorators call the same functions the read decorators use; no literals |
| D15 | ChangeTracker | **No change-tracker read in eviction or in read caching**; delete the coverage interceptor | Bypass detection moves to the handler audit + tests |
| D16 | Handler bypass | Fix `AddSection`; keep the audit in §4.7 current | Every cache-feeding write calls a decorated repository |
| D17 | Subject fan-out | **Open — G4**; tag (option 2) is the default | Do not write the tag row until G4 is closed |
| D18 | Asset ownership | **Open — G1/G2** | The per-course asset key set assumes single-owner and empty=unpooled |
| D19 | Key table authorship | Next AI authors/confirms §5 before code | The plan is the input; the table is the implementation contract |
| D20 | Read caching ownership | **Repository decorators cache their entity reads; query-service projection caching is kept** (user decision 2026-10-27, 2nd revision) | Read-through via static `*Entity` keys; details grilled in G11/G12 |
| D21 | Tag authorship | **Next AI authors the tag list** (user decision 2026-10-27) | Only the G4 fan-out tag may exist in Courses until the list is decided |
| D22 | Read-caching ownership (G11/G12 outcome) | **Exactly ONE repository read is cached**: `ICourseOverviewRepository.GetByCourseIdAsync` — a flat, STJ-safe DTO under the `CourseOverviewEntity` key, `EntityPolicy` (5 m), ambient `ITenantProvider`, `[]` tags (`CachingCourseOverviewRepositoryDecorator.cs:46-50`). All aggregate `GetById`-shaped reads (Course/Asset/Subject) delegate UNCACHED | Aggregate graphs are tracked-identity bridges (G12); serializing them would hand handlers detached copies. Never fix with a change-tracker scan; new cacheable reads need a flat, write-coupled DTO and their `*Entity` key queued by every write family |
| D23 | Tag authorship (G4 outcome) | **ONE fan-out tag, tenant-widest granularity**: `courses:views` per tenant (`CourseCacheTags.CoursePublicViews`), carried ONLY by the public-view read entry (`CachedCourseQueryService.cs:86`), queued ONLY by the subject decorator (`CachingSubjectRepositoryDecorator.cs:76`) | G4.1: subject rename is not attributable to any single course; public views recapitulate the subject name. Worst-case staleness = public-view TTL = 10 min. G4.2: catalogs are NOT on the tag — accepted ≤2-min staleness after rename/delete (catalog TTL 2 min, evicted by key on course writes); lookups remain forbidden |
| D24 | Ambient `TransactionScope` | **Fail-fast guard + `Enlist=false`** (user decision, handoff §5) | `EvictionSaveChangesInterceptor.SavedChangesAsync` throws `InvalidOperationException` when `System.Transactions.Transaction.Current` is not null (an ambient commit has no EF hook → unflushable queue → stale cache); `DatabaseSettings.cs:26-28` appends `Enlist=false` to every connection string. Empty `TransactionScope` usage in `src/` today; the guard is the invariant |

---

## 8. Remaining work

### 8.1 Phase 0 — grill (no code) — COMPLETE (ledger §11 filled)

- Run §6 G1–G12; record answers in a decision ledger next to this plan.
- Close G4 first (the lookup issue); the fan-out tag is the default, and its details are the
  next AI's authoring task (D21).
- Close G11/G12 before writing the decorator read-through caching.
- Close G6/G7 before finalizing the overview/asset key rows.

### 8.2 Phase 1 — dual queue (shared) — COMPLETE (`f0a31c81`, plus the interceptor pivot)

1. Convert `IEvictionProvider` to a dual queue: `Evict(CacheKey)` /
   `Evict(IEnumerable<CacheKey>)` plus `EvictTag(CacheTag)`; remove `HasPendingTags`; add
   `HasPendingKeys`.
2. Update `ScopedEvictionProvider`: replace the tag-only store with a key queue and a tag
   queue; `FlushAsync` calls `RemoveAsync` per key and `RemoveByTagAsync` per tag; `Discard`
   clears both.
3. Update the provider tests (keys and tags: queue once, flush once, discard,
   cancellation, failure).
4. ~~Leave `CacheEvictionFlushBehavior` and `EvictionFlushScope` unchanged.~~ Superseded: both
   were DELETED in the pivot to flush-at-commit interceptors (handoff §1.3, §4.2).

### 8.3 Phase 2 — Courses decorators — COMPLETE (`72260467`)

1. Add the `*Entity` keys and key-set helpers to `CourseCacheKeys` (§4.4), using the same
   read functions.
2. Add read-through caching to the four decorators' cacheable reads (G11/G12 decide which
   reads and the tenant source).
3. Convert the four decorators to queue key sets from `CourseCacheKeys`; the subject decorator
   also queues the fan-out tag (G4 only, authored per §5).
4. Narrow `CourseCacheTags` to the fan-out tag builder; remove tag arguments from the other
   Courses read decorators.
5. Delete the unused tag builders from `CourseCacheKeys`.
6. Delete `CoursesEvictionCoverageInterceptor`, its registration, and its test (D15).
7. Update `CoursesRepositoryDecoratorTests` to assert exact keys, exact tag calls (fan-out
   only), and read-through caching.

### 8.4 Phase 3 — handler fix — COMPLETE (`2c184204`)

1. Add `_courseRepository.Update(course)` to `AddSectionCommandHandler` after a successful
   `AddSection` and before returning.
2. Confirm `CourseView_AddSection_EvictsWarmedTree` passes.
3. Re-run the audit in §4.7 against the source (all write handlers).

### 8.5 Phase 4 — tests — COMPLETE (Phase 2 unit + Phase 3 integration additions)

1. Unit: each decorator queues exactly the §5 key set (and the subject decorator the fan-out
   tag); read methods cache with the `*Entity` keys but queue nothing; `TenantId ==
   Guid.Empty` queues nothing; `CourseAsset.CourseId == Guid.Empty` queues nothing.
2. Unit: provider flushes keys via `RemoveAsync` and the fan-out tag via `RemoveByTagAsync`;
   dedupe; failure/cancellation.
3. Integration: the mutation matrix in §10.2, including `AddSection`, reorder, plan update/
   remove, asset available from a null-tenant bus scope, overview cover-image change, subject
   rename, asset duration change, tenant isolation across seven surfaces, non-default bypass,
   and the read-through flows.
4. Delete the interceptor test with the interceptor.
5. Remove the unused `c(string)` helper / duplicate assertion in `CoursesCacheTests` if still
   present.

### 8.6 Phase 5 — docs — THIS RECONCILIATION (2026-10-29)

- `Modules/Courses/CACHING.md`: keys for enumerable surfaces and the narrowed fan-out tag;
  document decorator read caching; correct the false `AddSection` claim; state the G4
  decision; remove the interceptor from the enforcement table.
- `docs/caching/README.md`: key-based write path with the single fan-out tag; decorator
  read-through caching; no interceptor; update the invariants table.
- `docs/CachingArchitecture.md`: key eviction + fan-out tag; decorator read caching; no
  change tracker.
- ~~`docs/caching/spec-33/issues/course-eviction-parent-lookup.md`: this plan's companion
  grilling document; it is rewritten for the decorator-owned design and §6 (G4 first). Do not
  leave the old tag table there.~~ Out of the handoff §8 amendment scope — not touched.

### 8.7 Phase 6 — verification

```bash
dotnet build AlphaZero.sln --nologo -v q

dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj --nologo -v q

dotnet test tests/Modules/Courses/Courses.Tests.Integration/Courses.Tests.Integration.csproj --nologo -v q
```

Run the integration suite alone (not concurrently with the unit build) to avoid the known
MSB4018 file-lock issue recorded in `spec-32/code-review.md`.

Source checks:

```bash
grep -rn "ChangeTracker" src/alphazero-api/Modules/Courses/Infrastructure/Caching \
  src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching
# expected: no eviction/tag/key use

grep -rn "CourseCacheTags\|RemoveByTagAsync" src/alphazero-api/Modules/Courses
# expected: only the narrowed fan-out tag builder and the eviction flush

grep -rn "HybridCache" src/alphazero-api/Modules
# expected: comments only

grep -rn "EvictTag" src/alphazero-api/Modules/Courses
# expected: the subject decorator only
```

---

## 9. Acceptance checklist

- [ ] No `IEvictionStrategy`, `IEntityEvictionPolicy`, `ChangeTrackerEvictionStrategy`,
      `CourseEvictionPolicy`, `CollectAsync`, or `Evict(entity)` dispatch exists.
- [ ] No `ChangeTracker` read exists in the caching/eviction path or in read caching.
- [ ] `IEvictionProvider` has a dual queue; `FlushAsync` calls `RemoveAsync` for keys and
      `RemoveByTagAsync` for tags; `Discard` drops both.
- [ ] Every Courses write decorator builds keys only through static `CourseCacheKeys`
      functions; no literal key strings.
- [ ] Read methods of the repository decorators cache cacheable entity reads through the
      static `*Entity` keys (G11/G12 settled); collection reads bypass.
- [ ] The `Cached{Course,Subject}QueryService` projection caching is unchanged.
- [ ] Tags appear only in the subject decorator, the narrowed `CourseCacheTags` builder, and
      the entries that carry the fan-out tag; nowhere else in Courses.
- [ ] `AddSectionCommandHandler` calls `Update(course)`; its integration test passes.
- [ ] Every handler in §4.7 calls a decorated repository write.
- [ ] G1/G2 confirm asset single-ownership and the empty-`CourseId` case.
- [ ] G3 confirms no parent moves (or the move command updates both roots).
- [ ] G4 is decided; the fan-out tag details are implemented and tested.
- [ ] Overview writes evict the cover-image-dependent surfaces (overview, readiness, public
      view, both catalogs) plus the overview entity key.
- [ ] Asset writes evict readiness and every surface that projects duration.
- [ ] A course/overview/asset write also evicts the corresponding entity key.
- [ ] Subject rename is visible in the catalog and every public view within the chosen bound.
- [ ] All seven cached surfaces have a cross-tenant isolation test.
- [ ] No key contains caller search/sort/page/perPage; non-default requests bypass.
- [ ] Flush happens only after the outermost commit; errors discard; failures are logged.
- [ ] Docs match the implementation; no stale tag/guard/`AddSection` claims.
- [ ] Unit + integration suites green.

---

## 10. Test plan

### 10.1 Unit (Courses.UnitTests/Infrastructure/Caching)

The interceptor matrix uses a minimal SQLite-backed stub context (kept-open
`SqliteConnection`, `EnsureCreated`, real saves/transactions): the interceptors are
context- and provider-agnostic, so no module context is needed — the module model uses
Postgres-only column types (`jsonb`/`varbit`) and cannot build on SQLite. Assertions go through
an `ICachingService` stub (the provider is real, so `provider.Received()` is not usable;
the flush result is observable via the cache stub).

| # | Test (file) | Assertion |
|---|---|---|
| U1 | `Course` decorator writes (`CoursesRepositoryDecoratorTests`) | `Add`/`Update`/`Remove` queue exactly the 8-key §5 row (including `CourseStatus` and `CourseEntity`); read methods queue nothing |
| U2 | `CourseOverview` decorator writes | Queues overview, readiness, public view, both catalogs, overview entity key (6 keys) |
| U3 | `CourseAsset` decorator writes | Queues readiness, public view, both catalogs, asset entity key (5 keys, no tree); queues nothing for `CourseId == Guid.Empty` |
| U4 | `Subject` decorator writes | Queues subject list + subject entity key (2 keys) and the ONE fan-out tag via `EvictTag` — exact call count |
| U5 | Disabled scope | `TenantId == Guid.Empty` queues nothing and does not throw |
| U6 | Key provenance | Every queued key equals the value returned by the corresponding `CourseCacheKeys` function |
| U7 | Provider contract (`ScopedEvictionProviderTests`) | Dual lanes; dedupe; `FlushAsync` → `RemoveAsync` per unique key + `RemoveByTagAsync` per unique tag; snapshots-and-clears; empty queue is a no-op; `Discard` clears both; failure logged/swallowed; cancellation propagates |
| U8 | Interceptor matrix (`EvictionInterceptorTests`) | Standalone save success → flush; standalone save FAILURE → discard; transaction (2 saves) + commit → ONE flush; transaction + rollback → discard; `TransactionFailed` commit → flush; ambient transaction → the guard throws and nothing was flushed; saves with nothing queued → no cache calls |
| U9 | Read-through (G11) | `GetByCourseIdAsync` calls the inner repository once on miss and not at all on hit; keyed by `CourseOverviewEntity` under the ambient tenant; aggregate `GetById` reads delegate UNCACHED (G12) |
| U10 | Read bypass | Parameterized collection reads delegate directly; nothing caches and nothing queues |
| U11 | Key registry hygiene | No decorator/test references a change tracker, an entity dispatch, a lookup, or a raw key/tag string outside the two registries (also asserted by the §8.7 verification greps) |

### 10.2 Integration (`CoursesCacheTests`)

Keep the ghost-write pattern: a direct EF write bypasses the command pipeline and proves the
entry is cached; a command write proves eviction.

| # | Flow | Assertion |
|---|---|---|
| I1 | `AddSection` | Warm tree → add section → next read shows it (regression for F15) |
| I2 | Reorder sections/items | Warm tree → reorder → next read shows the new order |
| I3 | Plan update/remove | Warm view/catalog/readiness → mutate plan → next read is fresh |
| I4 | Item add/update/delete | Warm view → mutate → next read is fresh |
| I5 | Course update/delete/state | Warm catalog/view/overview/readiness → mutate → next read is fresh |
| I6 | Overview upsert with cover image | Warm catalog/public view/readiness/overview → upsert → next read shows the new cover |
| I7 | Asset available from a null-tenant bus scope | Warm readiness → publish event → next readiness read reflects `Available` |
| I8 | Asset duration sync | Warm catalog/public view → metadata sync changes duration → next read is fresh |
| I9 | Subject rename | Warm catalog/public view → rename → next read shows the new name (G4 mechanism) |
| I10 | Subject delete | Blocked while referenced; after all references are gone, subject list is fresh |
| I11 | Non-default paging/search/sort | Bypasses the cache and shows DB state immediately |
| I12 | Tenant isolation | All seven surfaces: tenant B cannot see tenant A's entries; A still resolves after B |
| I13 | Dashboard/enrollment | Remains uncached; direct DB change is immediately visible |
| I14 | Decorator read-through | Warm an entity read → direct DB change without a command → the next repository read is fresh after the command write evicts the entity key |

Phase 3 additions (all green): `Overview_UpsertCoverChangeExternal_RefreshesCatalogsViewOverviewAndReadiness`
(zeros in on I6 with a ghost cover + ghost draft), `Course_UpdateStateAndDelete_CommandWrites_RefreshCachedLists`
(I5 via command writes: update, direct publish, soft-delete),
`Asset_DurationSyncThroughHandler_RefreshesPublicViewTimings` (I8 driven by the real
`SyncResourceMetadataCommand` path in a fresh module scope),
`OverviewRepository_ReadThrough_ServesStaleCopyUntilCommandWriteEvicts` (I14 against the
repository surface resolved from the courses module scope).

---

## 11. Decision ledger (filled — grilling complete, Phase 0–3)

| ID | Question | Answer | Evidence / consequence |
|---|---|---|---|
| G1 | Asset single-owner? | **Yes — single owner.** Assignment rejects a mismatched course; `CourseId` is private-set with no move/rebind method | `AssignAssetToCurriculum.cs:40` (`asset.CourseId != request.CourseId` → rejected), same guard in `DismissAsset.cs:31`; `CourseMediaAsset.cs:28` `public Guid CourseId { get; private set; }` and no rebind API exists. No command, consumer or migration re-parents an asset. One per-course key set is complete (per-course asset keys, P1 resolved) |
| G2 | Empty `CourseAsset.CourseId`? | **Unassigned pool asset → queues NOTHING**; also `TenantId == Guid.Empty` queues nothing on every decorator | `CourseCacheKeys.EvictCourseAsset` returns `[]` for `Guid.Empty` (`CourseCacheKeys.cs:123-126`); `CachingCourseAssetRepositoryDecorator.Evict` early-returns when `TenantId == Guid.Empty` or `keys.Count == 0` (`:77-90`). A pool asset is referenced by no cached surface. Proven behaviorally: the asset-available integration test runs from a null-tenant bus scope (`TenantConsumeFilter`) and the tenant comes from the entity's own `TenantId` |
| G3 | Parent moves? | **No move exists for sections, items, resources or plans** — every child write loads and updates the Course root | All curriculum/plan/reorder/status commands mutate the loaded `Course` and call `Update(course)` (audit §4.7); children carry course placement ids with no re-parent command. If one is ever added: the handler loads and updates BOTH course roots (and each root's 8-key set)` |
| G4 | Subject→view fan-out mechanism | **The tag. Exactly one, tenant-wide**: `courses:views` (`CourseCacheTags.CoursePublicViews(scope)`), carried by the public-view read entry ONLY, queued by the subject decorator ONLY | G4.1: `CourseCacheTags.cs` (single builder); carried at `CachedCourseQueryService.cs:86` only — every other read entry passes `[]`; queued at `CachingSubjectRepositoryDecorator.cs:76` (the ONLY `EvictTag` call site in `Modules/Courses`). Lookup rejected (never sources `ITenantProvider` into eviction; never queries courses for the rename). **G4.2 staleness bounds (accepted):** public views ≤10 min (their TTL); student/staff catalogs ≤2 min after a subject rename/delete (catalog TTL 2 min, evicted by key on course writes) — deliberately NOT on the tag to avoid over-evicting two hot entries per subject write; subject list key is flushed immediately. Behavioral proof: `StudentCatalog_SearchBypassesCache_AndSubjectRenameRefreshesCatalogAndView` |
| G5 | Subject delete / cascade | **Blocked while referenced**; no cascade reaches cached entries otherwise | `DeleteSubject.cs:34-36`: `Any(c => c.SubjectId == subjectId)` → `Error.Conflict("Subject.InUse", ...)`. Cached per-course entries can therefore never outlive a (blocked) referenced-subject delete; after the last reference is gone, the SubjectList key is flushed by the delete write itself. Integration: `SubjectDelete_...` test |
| G6 | Which overview fields feed catalog / public view / readiness? | **`CoverImageDocumentId`** feeds all three (plus `DescriptionContent` feeds the overview endpoint and readiness's description check); title/description of the AGGREGATE come from the Course row, not the overview | `UpsertCourseOverview` read-modify-write shape — the upsert handler makes add-or-update from the `GetByCourseIdAsync` DTO and preserves the existing cover on update (no command can set a cover; it is only ever materialized externally). Hence `EvictCourseOverview` includes both catalogs + view + readiness + the overview key itself. Actually observed by the Phase 3 cover-upsert integration test |
| G7 | Asset fields in staff tree? | **None.** The staff tree reads only `r.CourseAssetId` from resources — never the asset table | The tree is built from the Course aggregate sections (placement ids) via the query service/get handler (`GetCourseView.cs` consumes `r.DurationSeconds`, the placement snapshot). Consequently `EvictCourseAsset` deliberately omits the tree key (the draft's confirmed §5 cell) |
| G8 | Handler audit complete? | **Yes — every cache-feeding write goes through a decorated repository (§4.7).** The last gap (`AddSection`) was fixed in Phase 3 | `AddSection.cs:41` now calls `_courseRepository.Update(course)` after the error check; `CourseActiveItemsDomainEventHandler` re-commits all three mask-mutation paths with explicit `Update` (`:48`, `:58`, `:68`); the tracked-identity bridge in `AddItem`/`RemoveItem` handlers (:74-82 region) is untouched by design (G12). Regression: `CourseView_AddSection_EvictsWarmedTree` green |
| G9 | Hidden cascades? | **Two scoped ones, both handled**: (a) course soft-delete is state-agnostic — deletes draft/published alike and the 8-key set still evicts; (b) subject delete is blocked while referenced (G5). No cascade writes pool/enrollment/analytics surfaces | `Course.Delete()` soft-deletes without status gating (verified PH1); staff list observed to drop the deleted course in the Phase 3 lifecycle test. Nothing else cascades between cached families |
| G10 | Remaining tags? | **Exactly one** — the G4 fan-out tag on public-view entries. No second family may be added; a new "can't enumerate" case returns to grilling | `CourseCacheTags.cs` has a single builder; the §8.7 grep proves `EvictTag` appears only in the subject decorator |
| G11 | Cacheable repository reads / read tenant source | **Only `GetByCourseIdAsync` is read-cached.** Tenant source: ambient `ITenantProvider` (the same source the query services use); `CacheScope` throws when tenantless, and a disabled scope bypasses to the inner repository | `CachingCourseOverviewRepositoryDecorator.cs:46-50/58-61` (key `CourseOverviewEntity(scope, courseId)`, policy `EntityPolicy` 5 min `CourseCacheKeys.cs:27`, tags `[]`). The DTO is a flat STJ round-trip-safe record coupled exclusively to overview writes (which queue the key). Collection/filtered reads (`GetFirst`/`Get`/`GetAll`/`Any`/`Count`/`Query`) bypass — no caller shape ever enters a key |
| G12 | Cached entity vs tracked entity in handlers | **All aggregate `GetById`-shaped reads delegate UNCACHED** — the aggregate is a tracked-identity bridge: a cached, deserialized copy handed to the inner repository collides with the live tracked instance in the same `AppDbContext` (identity resolution conflict: two instances, one key), and the read-through cannot proxy a tracked aggregate. Never "fix" with a change-tracker scan | `CachingCourseRepositoryDecorator.GetById` → `_inner.GetById` (uncached, class doc); same for asset/subject decorators (`ById` pass-through; the asset/subject entity keys are eviction-only today). The upsert overview handler works entirely from the flat `GetByCourseIdAsync` DTO — it reconstructs the entity and writes via explicit `Add`/`Update` — which is safe only because `CourseOverviewEntity` is queued by EVERY overview write (§5 delta): the add-or-update decision can never be made from a stale copy. This is exactly why the one read-through had to be a flat, write-coupled DTO rather than the aggregate |

**Phase 2 key-set deltas (supersede the §5 draft):** 8-key `EvictCourse` (adds eviction-only
`CourseStatus`), 6-key `EvictCourseOverview` (includes `CourseOverviewEntity` to prevent stale
cover resurrection), 5-key `EvictCourseAsset` keyed by `(courseId, assetId)` with `[]` for
`CourseId == Guid.Empty`, 2-key + 1-tag `EvictSubject` (catalogs dropped per G4.2),
entity-family split `CourseEntity`/`CourseOverviewEntity`/`CourseAssetEntity`/`SubjectEntity`
(TTL 5 m, `EntityPolicy`; only the overview one is read-cached per G11/G12), and readiness TTL
fixed at 3 min (the §2 tree's 1-min row was draft-only). The `courses:views` tag is tenant-wide.
**Phase 3 pipeline deltas (supersede handoff §1.3/§1.4 lane wording):** the queue lives on the
request-scoped `ScopedEvictionProvider` instance under a lock — NOT `AsyncLocal` (ancestor
context invisibility proven live, see §4.3) — and `HybridCachingService` resolves the provider
lazily via `IServiceProvider` to break the constructor cycle. Flush-at-commit semantics,
failure table and dual-lane contract are unchanged.

---

## 12. Review record (condensed)

- **Architecture A1 (bus-scope tenant):** resolved by D5=A; the asset-available test runs with
  the static test tenant disabled.
- **Architecture A2 (L1-only):** documented; Redis is a platform follow-up, not #33.
- **Architecture A3 (change-tracker policy queries):** removed with the design pivot; there is
  no `CollectAsync` and no policy.
- **Code quality Q1 (policy dispatch):** removed; decorators use concrete aggregate types.
- **Code quality Q2 (null-tenant duplication):** resolved by `CacheScope.IsEnabled`.
- **Code quality Q3 (dead `CachingRepository`):** deleted.
- **Code quality Q4 (cancellation swallowed):** resolved; `OperationCanceledException`
  propagates.
- **Code quality Q5 (Identity global keys):** documented as globally unique; out of Courses.
- **Code quality Q6 (tag builders):** this plan narrows `CourseCacheTags` to the fan-out tag;
  decorators use static key functions for everything else.
- **Performance P1 (coarse asset eviction):** resolved by per-course asset keys.
- **Performance P3 (change-tracker cost):** removed entirely.
- **Test gaps:** covered by §10; the critical gaps are the `AddSection` bypass and the G4
  subject fan-out.
- **Failure modes:** flush failure is non-fatal; a missed decorator write is caught by the
  audit/tests; L1-only staleness is bounded by TTL.

---

## 13. Non-goals

- Redis/L2 registration, multi-instance invalidation, or cache sizing.
- Changing student catalog/view response shapes or pagination contracts.
- Changing authentication, authorization, or HTTP tenant precedence.
- Caching repository collection reads shaped by caller paging/search/sort parameters (they
  bypass, the same rule as non-default projections).
- Caching reads anywhere other than the four Courses repository decorators and the existing
  `Cached{Course,Subject}QueryService` classes.
- Adding a second invalidation framework (marker interfaces, command eviction scopes,
  outbox-driven invalidation, a second tag family).
- Denormalizing `CourseId`/`TenantId` onto curriculum children; the Course-root write covers
  them.
- Migrating Identity/Tenants/VideoUploading away from their existing key/tag usage beyond what
  the shared eviction queue requires.

---

## 14. One-paragraph prompt for the next AI

Issue #33 is implemented on `spec/33-caching` (Phases 0–3 committed; the `Caching*RepositoryDecorator`
classes own the cache: one read-through — `ICourseOverviewRepository.GetByCourseIdAsync` under
`CourseOverviewEntity` — and write-side key sets from `CourseCacheKeys` into `IEvictionProvider`,
flushed by the two EF interceptors in `UnitOfWork.cs` at save/commit points; the ONE fan-out tag
is tenant-wide `courses:views`, carried by the public-view entry and queued only by the subject
decorator). If you touch this area: queue exact key sets built from the aggregate argument only;
never introduce a change-tracker read, an `Evict(entity)` dispatch, a database lookup in eviction
or for key building, a staging state, a second tag family, or a raw key/tag string outside
`CourseCacheKeys`/`CourseCacheTags`; keep the queue on the request-scoped provider instance (NOT
`AsyncLocal` — ancestor-context invisibility, §4.3) and keep the ambient-transaction fail-fast
guard + `Enlist=false`. Documented decisions live in §7 (D1–D24) and the filled ledger in §11;
the verified key-set deltas are in §5; run the §8.7 verification greps and both suites
(integration ALONE) after any change.
