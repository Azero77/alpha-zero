# Handoff: issue #33 caching — interceptor eviction pivot (2026-10-28)

## 0. How to use this file

This is the architecture-deciding context to continue issue #33 in a fresh AI agent.
It states **what is decided** (do not re-litigate), **what was removed deliberately**,
**what is open**, and **which statements in the repo documents are superseded**.

Evicted details (rejected alternatives, option menus) are intentionally NOT included here:
they are not binding. If something is missing, decide by the hard boundaries in §9 and record
the decision in the decision register.

- Worktree: `/home/azero/.local/share/opencode/worktree/adfa55/spec-33-caching`
- Branch: `spec/33-caching`
- Full written plan: `spec-33/code-review-plan.md` (repo file — still partially describes
  the OLD flush design; §8 of this handoff lists the deltas that supersede it)

---

## 1. Decided architecture (binding)

### 1.1 Ownership

The `Caching*RepositoryDecorator` classes (Courses infrastructure) own the cache logic on the
repository surface:

1. **Reads**: cacheable single-entity repository reads go through
   `ICachingService.GetOrCreateAsync` with a static `CourseCacheKeys` entity key.
   The existing `Cached{Course,Subject}QueryService` projection caching STAYS AS-IS.
   Decorator read caching is additive.
2. **Writes** (`Add`/`Update`/`Remove`): never cached; the decorator computes the affected
   cache keys **from the aggregate argument only** via static `CourseCacheKeys` key-set
   helpers and queues them in `IEvictionProvider`, before delegating to the inner repository.

### 1.2 Eviction currency

- **Keys are the primary currency.** Every enumerable surface evicts by exact keys
  (`ICachingService.RemoveAsync(key)` per key). Over-eviction = a cache miss = always safe.
  A key that was removed although never present is a no-op.
- **Tags exist ONLY for non-enumerable fan-out**: subject → public course views (G4).
  The subject decorator is the only Courses call site of `EvictTag`.
- `CourseCacheTags` is NARROWED, not deleted: it keeps exactly one fan-out tag builder
  (`CoursePublicViews(CacheScope)` or the name the next AI settles on). The tag is also the
  only tag carried by Courses read entries (public-view entries carry it; all other entries
  pass `[]`).

### 1.3 Flush machinery — EF interceptors, NOT MediatR behaviors

This supersedes the MediatR `CacheEvictionFlushBehavior` design that
`spec-33/code-review-plan.md` currently documents. New source of truth:

The provider keeps a **single queue per lane** (keys, tag) — there are **NO staged keys and
NO `ConfirmSave()`** (explicitly dropped by the issue owner). The queue contents are staged by
decorators and flushed at DB commit points by EF Core interceptors living in
`src/alphazero-api/AlphaZero.Shared/Infrastructure/UnitOfWork.cs`.

**Why staging-free is correct (do not reintroduce staging):**
- Without a transaction: every successful save flushes the queue to empty. At a failed save
  the queue contains exactly the keys of that failed save → `Discard()` is precise.
- Inside an open transaction: a failed save leaves its keys queued → flushed at commit →
  over-eviction only (never staleness).
- Bonus correctness win over the old behavior design: the old behavior discarded on
  `IErrorOr IsError` — dropping eviction of a write that had ALREADY committed inside a
  nested dispatch. Flush-at-commit removes that stale-cache class entirely.

**Interceptor contract (must be implemented exactly):**

```csharp
// 1. Save interceptor (SaveChangesInterceptor)
public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
    int result, CancellationToken ct = default)
{
    // Flush ONLY when this save was genuinely standalone:
    if (eventData.Context?.Database.CurrentTransaction is null
        && System.Transactions.Transaction.Current is null)     // ambient-transaction guard
    {
        await evictionProvider.FlushAsync(ct);
    }
    return await base.SavedChangesAsync(eventData, result, ct);
}

public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
{
    // failed standalone save: burn exactly its queued keys (staging-free variant of ConfirmSave)
    if (eventData.Context?.Database.CurrentTransaction is null)
        evictionProvider.Discard();
    return base.SaveChangesFailed(eventData, ct);   // always call the base hook
}
```

```csharp
// 2. Transaction interceptor (DbTransactionInterceptor)
TransactionCommittedAsync      -> await evictionProvider.FlushAsync(ct);     then base
TransactionRolledBackAsync/
TransactionRolledBack          -> evictionProvider.Discard();                then base
TransactionFailedAsync         -> if (eventData.Action == "Commit")
                                        await evictionProvider.FlushAsync(CancellationToken.None);
                                  // commit outcome ambiguous → flush (over-eviction is safe;
                                  // defer risks stale). Else -> evictionProvider.Discard()
```

**Provider flow rules:**
- Queue lives flow-scoped (`AsyncLocal`) in `ScopedEvictionProvider` → interceptor events fire
  in the same flow, and the provider can be registered as a singleton (state is AsyncLocal —
  write that reason in a code comment).
- Decorators queue before delegating. Nothing flushes at queueing time.
- `Discard()` clears both lanes. `FlushAsync` snapshots-and-clears both lanes IN PLACE before
  calling the cache (never clear by assignment; never retry a failed flush).
- `OperationCanceledException` propagates; every other flush exception is logged and swallowed
  (the write is already committed; entries die by TTL).

**DI wiring:**
- Register both interceptors on the Courses `AppDbContext` via the existing
  `opts.AddInterceptors(sp.GetRequiredService<...>())` pattern
  (`Modules/Courses/Infrastructure/DependencyInjection.cs`, currently holding
  `SoftDeleteInterceptor` + `CoursesEvictionCoverageInterceptor`).
- They are no-ops when the queue is empty → safe to add to other module contexts later.

**Transaction semantics already covered for free:**
- The only explicit transaction site in the repo is `Modules/Courses/Infrastructure/Behaviors/TransactionalBehavior.cs`
  (`BeginTransactionAsync`; commit path flushes via the DbTransaction interceptor).
- `EnableRetryOnFailure` execution strategies wrap retries in internal transactions →
  `CurrentTransaction != null` → flush routed through the commit interceptor. No work needed.

### 1.4 The provider contract (Shared)

```csharp
public interface IEvictionProvider
{
    bool IsActive { get; }
    bool HasPendingKeys { get; }               // replaces HasPendingTags
    void Begin();
    void Evict(CacheKey key);
    void Evict(IEnumerable<CacheKey> keys);
    void EvictTag(CacheTag tag);               // fan-out only
    Task FlushAsync(CancellationToken ct);
    void Discard();
}
```

`ScopedEvictionProvider`: two `AsyncLocal<HashSet<...>>` lanes — `CacheKey` set and `CacheTag`
set (HashSet gives dedupe for free). `FlushAsync` = `RemoveAsync` per unique key, then
`RemoveByTagAsync` per unique tag. `ICachingService` stays UNCHANGED
(`RemoveAsync(CacheKey)` and `RemoveByTagAsync(CacheTag)` both already exist).

### 1.5 Read-caching rules (repository decorators)

- Tenant scope for a READ comes from the ambient tenant context (same source the query
  services already use). Write-side rules are unchanged: NEVER `ITenantProvider` in a write
  decorator; the aggregate owns the tenant; empty tenant (`Guid.Empty`) queues nothing
  (`CacheScope` throws on `Guid.Empty` — guard with `scope.IsEnabled` or tenant check).
- Cache ONLY single-entity reads (`GetById`-shaped). Collection/expression-filter reads
  (`GetFirst(filter)`, `Get(filter)`, `GetAll`, `Query()`, `Count`, `Any`, `IgnoreQueryFilter`)
  delegate straight through. Non-default shapes never enter any key.
- Which entity reads are cacheable + serialization safety of aggregate graphs (G12) is
  an open grilling item (§7) — if a graph cannot be cached safely, that read delegates
  directly; no fallback mechanism.

---

## 2. Cache tree (keys, tags, TTLs)

`<T>` = tenant segment (always; `CacheScope` enforces). Only default-shaped requests cache.

```
<T>:tag:views                                    THE one tag (fan-out) — on public-view entries

<T>:courses:subjects                             Subject list (default page)      TTL 15m
<T>:subjects:{subjectId}                         SubjectDto                       TTL 15m
<T>:courses:catalog:staff                        staff catalog, default page      TTL 2m
<T>:courses:catalog:student                      student catalog, default page    TTL 2m
<T>:course:{courseId}:tree                       staff tree projection            TTL 10m
<T>:course:{courseId}:view:public                public summary projection        TTL 10m
<T>:course:{courseId}:overview                   overview DTO                     TTL 10m
<T>:course:{courseId}:readiness                  readiness                        TTL 1m
<T>:course:{courseId}:status                     status string (tiny)             TTL 30s-1m
<T>:entity:course:{courseId}                     decorator read-through           TTL 5m
```

- Variant budget per family: **hard cap 2** (e.g. catalog `newest` default + `title`; page=1
  only). Search / subjectId-filtered / non-default pages: BYPASS to the database.
- Catalog is a single entry per tenant → evict by KEY, never by tag.
- Every key family belongs to exactly one registry function; no literal key/tag strings
  outside `CourseCacheKeys`/narrowed `CourseCacheTags`.

### Eviction matrix (each row's keys derive only from the aggregate argument)

| Write | Queue |
|---|---|
| Course (any root/section/item/resource/plan/reorder/status/soft-delete) | `catalog:staff`, `catalog:student`, `tree`, `view:public`, `overview`, `readiness`, `status`, `entity:course` — 8 keys |
| CourseOverview upsert | `overview`, `readiness`, `view:public`, `catalog:staff`, `catalog:student` — 5 keys |
| CourseAsset (any TPH subtype; state/duration/info) | `readiness`, `view:public`, `catalog:staff`, `catalog:student` — 4 keys. **NOT `tree`** — verified: the staff-tree projection reads only `r.CourseAssetId`, never the asset table |
| Subject (create/rename/update) | `courses:subjects` key + `EvictTag` on the views tag — 1 key + 1 tag |
| Enrollment / analytics / saga / course pool | nothing (uncached surfaces) |

### Key-set helpers live in `CourseCacheKeys` (single registry)

Read functions (already exist at HEAD): `StaffCatalog`, `StudentCatalog`, `CourseView`,
`CoursePublicView`, `CourseOverview`, `CourseReadiness`, `SubjectList`.
New: `CourseEntity`, `CourseOverviewEntity`, `CourseAssetEntity`, `SubjectEntity` (decorator
read-through) + key-set helpers `EvictCourse(scope, id)`, `EvictCourseOverview(scope, id)`,
`EvictCourseAsset(scope, id)` (returns `[]` for `CourseId == Guid.Empty`),
`EvictSubject(scope, subjectId)` — each including the matching `*Entity` key.
DELETE from `CourseCacheKeys`: tag builders `CatalogTag`, `SubjectsTag`, `CourseViewsTag`,
`CourseTag`, `CourseAssetsTag`; DELETE the tag position on Courses read entries except
public-view entries carry the fan-out tag.

---

## 3. What to DELETE (Gap 3 closure — file-level)

| Path | Action |
|---|---|
| `AlphaZero.Shared/Infrastructure/Caching/CacheEvictionFlushBehavior.cs` | delete (class + tests + DI) |
| `AlphaZero.Shared/Infrastructure/Caching/EvictionFlushScope.cs` | delete |
| `AlphaZero.Shared/Infrastructure/UnitOfWork.cs` | keep the new interceptors listed in §1.3; REMOVE the old MediatR-behavior comment; complete the missing pieces (`SaveChangesFailedAsync`, `Transaction.Current` guard) |
| `AlphaZero.Shared/Infrastructure/Caching/IEvictionProvider.cs` | rewrite to §1.4 |
| `AlphaZero.Shared/Infrastructure/Caching/ScopedEvictionProvider.cs` | rewrite to §1.4 (dual lane) |
| `Modules/Courses/Infrastructure/DependencyInjection.cs` | delete `CacheEvictionFlushBehavior` registration (currently line ~92-93 `AddOpenBehavior`); delete `CoursesEvictionCoverageInterceptor` (file + registration lines ~40/43 + tests); register the two new interceptors |
| `Modules/Courses/Infrastructure/Caching/CoursesEvictionCoverageInterceptor.cs` (+ its test) | delete (it was the last ChangeTracker read in the eviction path) |
| `Modules/Courses/Infrastructure/Caching/CourseCacheTags.cs` | narrow to the single fan-out builder |
| `Modules/Courses/Infrastructure/Caching/Caching{Course,CourseOverview,CourseAsset,Subject}RepositoryDecorator.cs` | convert to key-set helpers (§2) + add read-through on cacheable reads |
| `Modules/Courses/Infrastructure/Caching/Cached{Course,Subject}QueryService.cs` | keep; drop tag args except fan-out tag on public-view entries |
| `Modules/Courses/Application/Courses/Commands/AddSection/AddSection.cs` | FIX: handler mutates the tracked Course but never calls `_courseRepository.Update(course)` → add the update (verified bypass bug; the integration test `CourseView_AddSection_EvictsWarmedTree` fails until fixed) |

Already deleted previously and must NOT return: `IEvictionStrategy`, `IEntityEvictionPolicy`,
`ChangeTrackerEvictionStrategy`, `CourseEvictionPolicy`, `UnitOfWork.CollectAsync`,
any `Evict(entity)` dispatch, any database lookup inside eviction, any ChangeTracker read
in the caching path.

---

## 4. Key files at HEAD (reference)

- Shared caching: `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/`
  (`ICachingService.cs`, `HybridCachingService.cs`, `IEvictionProvider.cs`,
  `ScopedEvictionProvider.cs`, `CacheKey.cs`, `CacheScope.cs`, `CachePolicy.cs`,
  `CacheEvictionFlushBehavior.cs` [to delete], `EvictionFlushScope.cs` [to delete])
- `src/alphazero-api/AlphaZero.Shared/Infrastructure/UnitOfWork.cs`
  (user has DRAFTED the two interceptors here in the working copy — verify on arrival:
  what exists = flush-on-no-transaction in `SavedChangesAsync`; commit/rollback/failure
  overrides on `DbTransactionInterceptor`. Missing = `SaveChangesFailedAsync` Discard and
  the `Transaction.Current` ambient guard)
- Courses caching: `src/alphazero-api/Modules/Courses/Infrastructure/Caching/`
  (`CourseCacheKeys.cs`, `CourseCacheTags.cs` [narrow], `CachedCourseQueryService.cs`,
  `CachedSubjectQueryService.cs`, 4 `Caching*RepositoryDecorator.cs`,
  `CoursesEvictionCoverageInterceptor.cs` [delete])
- Courses DI: `Modules/Courses/Infrastructure/DependencyInjection.cs` (context interceptors
  lines ~38-41; decorator registrations lines 49-57; MediatR behavior line ~93)
- Cache-safe projections: `Modules/Courses/Infrastructure/Queries/CourseQueryService.cs`,
  `SubjectQueryService.cs`
- Only explicit-transaction site: `Modules/Courses/Infrastructure/Behaviors/TransactionalBehavior.cs`
- L1-only (no Redis registered). Update `AddHybridCache` options via mutation, not reassignment
  (fixed + tested; don't regress).
- Shared tests: `tests/AlphaZero.Shared.Tests/` (caching registration/eviction pipeline tests);
  Courses unit: `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/`;
  integration: `tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs`

---

## 5. Decisions snapshot (supersedes older decision rows)

| # | Decision (final) |
|---|---|
| D13 | Eviction currency: **keys for every enumerable surface; exactly one fan-out tag** (subject → public views). Dual-queue provider. |
| D20 | Read caching lives in the repository decorators (single-entity reads, `*Entity` keys) IN ADDITION to the existing query-service projection caching. |
| D21 | The next AI authors the fan-out tag list (one tag today) and grills the lookup issue. |
| NEW | Flush machinery: **EF interceptors in UnitOfWork.cs** (save flush when standalone; commit flush; rollback/failed-save discard; commit-ambiguity flush). NO staging states, NO `ConfirmSave()`. NO MediatR eviction behavior. |
| NEW | Ambient transactions: `Transaction.Current` guard in the flush path (fail fast — the write path is cache-unsafe because an ambient commit has no EF hook) + `Enlist=false` in Npgsql connection strings as belt-and-braces. Nothing in the repo uses `TransactionScope` today; the guard is the invariant. |
| NEW | `TransactionFailedAsync`: commit-path failure → flush defensively (can't know if DB committed); else discard. |

---

## 6. Tests (target state)

Shared unit (provider + interceptors, harness with a real DbContext):
- provider: queue keys/tag once, dedupe, `FlushAsync` → `RemoveAsync` per key + `RemoveByTagAsync`
  per tag, snapshots-and-clears, cancel propagates, other failures logged/swallowed,
  `Discard` clears both lanes, empty queue = no-op.
- interceptor matrix: standalone save success → flush; standalone save FAILURE → discard;
  transaction (2 saves) + commit → single flush; transaction + rollback → discard;
  `TransactionFailed` commit → flush; ambient transaction → the guard throws (and nothing was
  flushed); saves with no decorator staging → no calls; retry-strategy internal transaction →
  flush routed to commit.

Courses unit: each decorator queues exactly the §2 key set; subject decorator adds exactly one
`EvictTag`; read-through: cacheable read → inner repo once on miss, zero on hit; keyed by the
`*Entity` function; collection reads bypass; `TenantId == Guid.Empty` / `CourseId == Guid.Empty`
queues nothing and does not throw; every queued key equals a `CourseCacheKeys` return value.

Courses integration (`CoursesCacheTests`): keep the ghost-write pattern (direct EF write proves
the entry is cached; command write proves eviction) and the matrix — AddSection bypass,
reorders, plan update/remove, item add/update/delete, course update/delete/state, overview
cover-image upsert (cover → catalogs/public view/readiness), asset-available consume path from
a null-tenant bus scope (tenant comes from the entity's `TenantId`, NOT the bus scope),
asset duration sync, subject rename (views tag mechanism), subject delete blocked-while-
referenced, non-default paging/search/sort bypass, tenant isolation across all seven cached
surfaces, dashboard/enrollment stays uncached, decorator read-through freshness.

Handler audit: every cache-feeding write calls a decorated repository (fix `AddSection`).
Keep the audit table current in the PR.

---

## 7. Open items (grill before/while coding — one at a time, demand file/line evidence)

- **G4 (lookup issue, first)**: subject → public-view fan-out. The fan-out tag is the chosen
  mechanism; pin down the tag's name, granularity, TTL and the exact entries that carry it.
  Lookups (option "query courses by subject then remove keys") are FORBIDDEN — they defeat the
  cache and were explicitly rejected by the issue owner.
- **G11**: which repository reads are cacheable; read-side tenant source (working assumption:
  ambient tenant context, same as query services); bounded key space; serialization safety of
  aggregate graphs.
- **G12**: cached-copy vs EF-tracked aggregate — verify per write-handler that handing a cached
  instance to the inner repository is correct (mutating a detached copy silently no-ops).
  If a mismatch appears: skip caching that read or map/re-attach EXPLICITLY in the decorator.
  NEVER fix this with a ChangeTracker scan. Tracked writes keep going through decorated calls.
- **G1/G2**: asset single-ownership (assignment rejects `asset.CourseId != request.CourseId`;
  `CourseId` private-set, no move method) and the empty-`CourseId` = unassigned pool asset case.
- **G3**: no parent moves; if one is ever added, the handler loads and updates BOTH course roots.
- Record answers in the decision ledger; do not write fan-out tag code before G4 closes.

---

## 8. Reconciliation with `spec-33/code-review-plan.md` (supersede these)

The plan file is mostly still valid (facts §2, key registry §4.4 incl. `*Entity` keys,
eviction matrix §5, decoding/no-change-tracker principles §4.1/§4.6, decorator rules §4.5,
handler audit §4.7). Amend exactly:

- §4.2 + §4.3 + §4.8: flush machinery = interceptors (this file §1.3), NOT the MediatR
  behavior; remove depth-gating language; failure table gains: standalone-save-failure →
  discard, ambient → throw, commit-ambiguity → flush.
- §4.7/§4.9: the interceptor delete list ALREADY covered; keep the handler audit.
- D12: "flush at EF save/commit interceptors (no staging states)".
- New decisions: D22 read caching ownership, D23 tag authorship by next AI, D24 ambient
  TransactionScope guard.
- §7 decision register + §10/§11/§14: renumber tests to the interceptor harness (§6 above).
- Keep AddSection fix (Phase 3) — unchanged priority.

---

## 9. Hard boundaries (do not cross — these define the architecture)

1. No ChangeTracker reads anywhere in cache/eviction/read-caching code. No `Evict(entity)`.
2. No database lookups (or extra entity loads) inside eviction OR for building read keys.
3. No queueing/flushing logic outside: decorator queueing + the two interceptors. One mechanism.
4. No batching/staging states (`ConfirmSave`-style) — dropped deliberately; rationale §1.3.
5. No ambient (`TransactionScope`) usage — the interceptor fails fast; add `Enlist=false`.
6. No raw key/tag strings outside the two registries (`CourseCacheKeys`, `CourseCacheTags`).
7. Keys/tags are ALWAYS tenant-scoped (built through `CacheScope`; it throws when tenantless).
8. No caller paging/search/sort/page values in any key. Non-default shapes bypass entirely.
9. Cache only projections + single-entity reads; never cache pool/enrollment/analytics/saga
   data; never cache collection/expression reads.
10. Tags: never add a second Courses tag family; a new "can't enumerate" case returns to
    grilling, not to a quick tag.
11. Over-eviction is always acceptable (misses); under-eviction is a bug. When between two key
    sets, choose the larger and note the cell in §2.
12. Flush failure after commit is logged, never surfaced; a successful write never fails
    because of the cache.

---

## 10. Verification (end-state greps)

```bash
grep -rn "CacheEvictionFlushBehavior\|EvictionFlushScope\|HasPendingTags\|CoursesEvictionCoverageInterceptor" src/
# expected: no hits

grep -rn "ChangeTracker" src/alphazero-api/Modules/Courses/Infrastructure/Caching \
  src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching
# expected: no hits

grep -rn "EvictTag" src/alphazero-api/Modules/Courses
# expected: subject decorator only

grep -rn "CourseCacheTags" src/alphazero-api/Modules/Courses
# expected: the narrowed fan-out builder + its one registration

grep -rn "TransactionScope" src/
# expected: no hits (the ambient guard should never trip)

dotnet build AlphaZero.sln --nologo -v q; then unit + integration suites separately
(integration alone — known MSB4018 file-lock issue).
```
