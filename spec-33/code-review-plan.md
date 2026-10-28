# Issue #33 caching — code review and solution plan

Generated 2026-10-23 from a read-only review of the `spec/33-caching` worktree.
**Rewritten 2026-10-27** after the second eviction-design pivot agreed with the issue owner.

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
>    a general eviction currency; the next AI authors the exact tag list (§5/§6).
> 4. The queue is flow-local and dual. The outermost MediatR behavior
>    (`CacheEvictionFlushBehavior`) removes the queued keys with `RemoveAsync` and the queued
>    tags with `RemoveByTagAsync`, only after the UnitOfWork commits.
>
> No change tracker and no lookups: `IEvictionStrategy`, `IEntityEvictionPolicy`,
> `ChangeTrackerEvictionStrategy`, `CourseEvictionPolicy`, `UnitOfWork.CollectAsync`, any
> `Evict(entity)` dispatch, and any database read inside eviction do not exist and must not
> be reintroduced. The aggregate argument is the only eviction input.
>
> Open for the next AI: authoring the fan-out tag list (§5) and the lookup grilling agenda
> (§6, G4 first). Do not write tag code outside the narrowed fan-out row before those close.

This file is self-contained input for the next AI session. No code was changed to produce it.

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
5. The queue is flow-local and dual: `CacheEvictionFlushBehavior` (outermost) removes the
   queued keys with `RemoveAsync` and the queued tags with `RemoveByTagAsync` after the
   UnitOfWork has committed.
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
UnitOfWork.SaveChangesAsync                    (no caching dependency)
   ▼
DbContext.SaveChangesAsync  (commit)
   ▼
TransactionalBehavior.CommitAsync              (only for ITransactionalCommand)
   ▼
CacheEvictionFlushBehavior (outermost)         ← after commit
   │  provider.FlushAsync()
   ▼
ICachingService.RemoveAsync(key) for each queued key
ICachingService.RemoveByTagAsync(tag) for each queued tag
```

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

`ScopedEvictionProvider` changes:

- `AsyncLocal<HashSet<CacheKey>?>` for the key queue (replacing `HashSet<CacheTag>?`), plus a
  second `AsyncLocal<HashSet<CacheTag>?>` for the tag queue.
- `FlushAsync` snapshots and clears both queues in place, then calls
  `ICachingService.RemoveAsync(key, cancellationToken)` for each key and
  `ICachingService.RemoveByTagAsync(tag, cancellationToken)` for each tag.
- Failure semantics stay: `OperationCanceledException` propagates; every other exception is
  logged and swallowed because the write already committed.
- `Discard` (or an error result / exception) drops both queues.

`ICachingService` keeps `RemoveByTagAsync`; the Courses path now reaches it only through the
eviction flush, for the fan-out tag. `GetOrCreateAsync`/`SetAsync` keep the tag parameter, but
Courses read entries pass `[]` — an entry carries a tag only for the fan-out family authored
by the next AI (§5). `CourseCacheKeys` stops exposing tag builders; tag building moves to a
narrowed `CourseCacheTags` (fan-out row only).

`CacheEvictionFlushBehavior` and `EvictionFlushScope` do not change; they flush whatever the
queues hold.

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

**Read methods (new read-through caching):**

1. Build `CacheScope.ForTenant(<ambient tenant>)` the same way the existing read decorators
   in the query services already do (there is no aggregate argument on a read; G11 settles
   the tenant source).
2. Wrap cacheable reads (single-entity reads per G12) in `ICachingService.GetOrCreate` with
   the static entity key from `CourseCacheKeys` (§4.4).
3. Never cache collection reads whose shape depends on caller paging/search/sort — they
   bypass to the inner repository, exactly as non-default projections already do.
4. Serialization safety of cached aggregates is an authoring task for the next AI (G12): if
   an aggregate cannot be cached safely, that read delegates directly and nothing else
   changes.

**Write methods (`Add`/`Update`/`Remove`):**

Each method:

1. Builds `CacheScope.ForTenant(entity.TenantId)`.
2. Queues `entity.TenantId == Guid.Empty ? [] : keySet(scope, entity.Id)`, plus the fan-out
   tag in the subject decorator only.
3. Delegates to the inner repository.
4. Decorator-cached reads are invalidated by the same queue: each write's key set contains
   the entity keys (§4.4), so no separate read-invalidation path exists.

Example:

```csharp
public async Task<Course?> GetCourseByIdAsync(Guid courseId, CancellationToken ct)
{
    var scope = CacheScope.ForTenant(_tenant.CurrentTenantId);   // same source as query services
    return await _caching.GetOrCreateAsync(
        CourseCacheKeys.CourseEntity(scope, courseId),
        () => _inner.GetCourseByIdAsync(courseId, ct), ct);
}

public void Update(Course course)
{
    var scope = CacheScope.ForTenant(course.TenantId);
    if (scope.IsEnabled)
        _eviction.Evict(CourseCacheKeys.EvictCourse(scope, course.Id));

    _inner.Update(course);
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
| `AddSection` | none | **BROKEN — add `_courseRepository.Update(course)`** |
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

### 4.8 Failure semantics

| Situation | Queue | Cache call | Caller observes |
|---|---|---|---|
| Success, flush succeeds | Snapshot, cleared in place | `RemoveAsync` per key + `RemoveByTagAsync` per tag | Normal result |
| Success, cache throws | Cleared before calls | Failures logged | Normal result; entries age out by TTL |
| Success, cache call cancels | Cleared | `OperationCanceledException` rethrown | Cancellation propagates (write already committed) |
| `ErrorOr` error / validator rejects | Discarded | None | The error result |
| Exception anywhere in the pipeline | Discarded | None | The exception rethrown |
| Nested dispatch returns | Kept | None | Inner result; outer flush applies the keys and tags |
| Save outside the outermost behavior | Queued invariants kept by the provider guard | None until a flush | Documented guard path |

A flush failure after commit must never turn a successful write into a failed request.

### 4.9 Delete the coverage interceptor (recommended)

`CoursesEvictionCoverageInterceptor` is the only remaining `ChangeTracker` read in the caching
path (F20). It cannot derive keys, only warn. The direction for this change is no change-tracker
involvement in eviction, so:

- Delete `CoursesEvictionCoverageInterceptor`, its DI registration, and
  `CoursesEvictionCoverageInterceptorTests`.
- Replace it with the handler audit (§4.7), the decorator unit tests (§10.1), and the
  mutation-family integration tests (§10.2).
- Remove `HasPendingTags` from the provider API (it exists only for the guard); tags are
  queued without a pending flag via `EvictTag`.

Tradeoff to state: a future handler that forgets `Update(course)` will be caught by tests and
review, not at runtime. That is acceptable because the write surface is small (four
repositories) and the audit is now explicit. If the team later wants a runtime alarm, it must
not use `ChangeTracker`; that would be a new design question, not part of this plan.

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
| D12 | Flush point | Outermost behavior, after commit, depth-gated (done) | No early flush from nested sends |
| D13 | Eviction currency | **Keys for enumerable surfaces; tags only for non-enumerable fan-out** (user decision 2026-10-27, 2nd revision) | `IEvictionProvider` dual queue: `Evict(CacheKey)` + `EvictTag(CacheTag)`; `FlushAsync` calls `RemoveAsync` then `RemoveByTagAsync` |
| D14 | Key construction | **Static `CourseCacheKeys` functions only** | Decorators call the same functions the read decorators use; no literals |
| D15 | ChangeTracker | **No change-tracker read in eviction or in read caching**; delete the coverage interceptor | Bypass detection moves to the handler audit + tests |
| D16 | Handler bypass | Fix `AddSection`; keep the audit in §4.7 current | Every cache-feeding write calls a decorated repository |
| D17 | Subject fan-out | **Open — G4**; tag (option 2) is the default | Do not write the tag row until G4 is closed |
| D18 | Asset ownership | **Open — G1/G2** | The per-course asset key set assumes single-owner and empty=unpooled |
| D19 | Key table authorship | Next AI authors/confirms §5 before code | The plan is the input; the table is the implementation contract |
| D20 | Read caching ownership | **Repository decorators cache their entity reads; query-service projection caching is kept** (user decision 2026-10-27, 2nd revision) | Read-through via static `*Entity` keys; details grilled in G11/G12 |
| D21 | Tag authorship | **Next AI authors the tag list** (user decision 2026-10-27) | Only the G4 fan-out tag may exist in Courses until the list is decided |

---

## 8. Remaining work

### 8.1 Phase 0 — grill (no code)

- Run §6 G1–G12; record answers in a decision ledger next to this plan.
- Close G4 first (the lookup issue); the fan-out tag is the default, and its details are the
  next AI's authoring task (D21).
- Close G11/G12 before writing the decorator read-through caching.
- Close G6/G7 before finalizing the overview/asset key rows.

### 8.2 Phase 1 — dual queue (shared)

1. Convert `IEvictionProvider` to a dual queue: `Evict(CacheKey)` /
   `Evict(IEnumerable<CacheKey>)` plus `EvictTag(CacheTag)`; remove `HasPendingTags`; add
   `HasPendingKeys`.
2. Update `ScopedEvictionProvider`: replace the tag-only store with a key queue and a tag
   queue; `FlushAsync` calls `RemoveAsync` per key and `RemoveByTagAsync` per tag; `Discard`
   clears both.
3. Update `EvictionPipelineTests` (keys and tags: queue once, flush once, discard,
   cancellation, failure).
4. Leave `CacheEvictionFlushBehavior` and `EvictionFlushScope` unchanged.

### 8.3 Phase 2 — Courses decorators

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

### 8.4 Phase 3 — handler fix

1. Add `_courseRepository.Update(course)` to `AddSectionCommandHandler` after a successful
   `AddSection` and before returning.
2. Confirm `CourseView_AddSection_EvictsWarmedTree` passes.
3. Re-run the audit in §4.7 against the source (all write handlers).

### 8.5 Phase 4 — tests

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

### 8.6 Phase 5 — docs

- `Modules/Courses/CACHING.md`: keys for enumerable surfaces and the narrowed fan-out tag;
  document decorator read caching; correct the false `AddSection` claim; state the G4
  decision; remove the interceptor from the enforcement table.
- `docs/caching/README.md`: key-based write path with the single fan-out tag; decorator
  read-through caching; no interceptor; update the invariants table.
- `docs/CachingArchitecture.md`: key eviction + fan-out tag; decorator read caching; no
  change tracker.
- `docs/caching/spec-33/issues/course-eviction-parent-lookup.md`: this plan's companion
  grilling document; it is rewritten for the decorator-owned design and §6 (G4 first). Do not
  leave the old tag table there.

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

| # | Test | Assertion |
|---|---|---|
| U1 | `Course` decorator writes | `Add`/`Update`/`Remove` queue exactly the seven §5 keys (including `CourseEntity`); read methods queue nothing |
| U2 | `CourseOverview` decorator writes | Queues overview, readiness, public view, both catalogs, overview entity key |
| U3 | `CourseAsset` decorator writes | Queues readiness, public view, both catalogs, asset entity key; queues nothing for `CourseId == Guid.Empty` |
| U4 | `Subject` decorator writes | Queues subject list, both catalogs, subject entity key, and the fan-out tag via `EvictTag` |
| U5 | Disabled scope | `TenantId == Guid.Empty` queues nothing and does not throw |
| U6 | Key provenance | Every queued key equals the value returned by the corresponding `CourseCacheKeys` function |
| U7 | Flush | Provider calls `RemoveAsync` once per unique key and `RemoveByTagAsync` for the fan-out tag, clears both queues, dedupes |
| U8 | Failure/cancellation | Cache failure is logged/swallowed; `OperationCanceledException` propagates |
| U9 | Nesting/discard | Nested dispatch does not flush; `ErrorOr` error and exception discard keys and tags |
| U10 | Handler audit | A source test or the integration matrix proves `AddSection` and the tracking-only handlers evict |
| U11 | G4 mechanism | Subject write evicts the view fan-out tag; the next view read misses and repopulates |
| U12 | Read-through | A cacheable read calls the inner repository once on miss and not at all on hit; the key equals the `*Entity` function |
| U13 | Read bypass | Parameterized collection reads delegate directly; nothing caches and nothing queues |
| U14 | No new mechanisms | No decorator, key builder, or test references a change tracker, an entity dispatch, or a lookup |

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

---

## 11. Decision ledger (fill during the grilling)

| ID | Question | Answer | Evidence / consequence |
|---|---|---|---|
| G1 | Asset single-owner? | | |
| G2 | Empty `CourseAsset.CourseId`? | | |
| G3 | Parent moves? | | |
| G4 | Subject→view fan-out mechanism | | |
| G5 | Subject delete/cascade | | |
| G6 | Overview fields | | |
| G7 | Asset fields in staff tree | | |
| G8 | Handler audit complete? | | |
| G9 | Hidden cascades? | | |
| G10 | Remaining tags? | | |
| G11 | Cacheable repository reads / read tenant source | | |
| G12 | Cached entity vs tracked entity in handlers | | |

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

You are implementing issue #33 on `spec/33-caching`. The `Caching*RepositoryDecorator` classes
own the cache logic: they cache cacheable repository entity reads through static
`CourseCacheKeys` `*Entity` functions (the query-service projection caching stays as-is), and
on writes they queue exact key sets built from the same static functions into
`IEvictionProvider`. Tags are queued only for non-enumerable fan-out — currently subject →
public course views: author that tag list yourself per §5 and run the grilling agenda in §6,
starting with G4 (the lookup issue) and G1/G2/G3 (asset ownership, parent moves). Do not
introduce a change-tracker scan, an `Evict(entity)` dispatch, a database lookup in eviction, or
raw key/tag strings. The provider holds a key queue and a tag queue; the outermost behavior
flushes with `RemoveAsync` and `RemoveByTagAsync` after commit. Fix the `AddSection` bypass,
delete the coverage interceptor and the unused tag builders, update the tests and docs, and
report the G1–G12 answers with file/line evidence before changing code.
