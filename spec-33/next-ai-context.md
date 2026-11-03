# spec-33 caching — CONTEXT FOR THE NEXT AI (Phase 1 done, commit `f0a31c81`)

**You are continuing issue #33 (repository caching) on branch `spec/33-caching` in this worktree.**
Phases 0 and 1 are COMPLETE and committed. Your job: Phase 2 → Phase 3 → Phase 4 → final verification.

**Binding source of truth:** `spec-33/spec-33-caching-handoff.md` (it supersedes
`spec-33/code-review-plan.md` wherever they disagree — see handoff §8). Do NOT re-litigate
architecture. All open design questions were grilled and CLOSED; every answer is in the
decision ledger below. Never code a design decision silently.

---

## 1. Where things stand

| Phase | Status | Evidence |
|---|---|---|
| 0 — verify + grilling | DONE | gaps confirmed (see ledger) |
| 1 — shared contract | DONE, committed `f0a31c81` | `Courses.UnitTests`: **233 passed / 0 failed** |
| 2 — Courses decorators | **← START HERE** | |
| 3 — AddSection fix + integration suite | todo | |
| 4 — docs | todo | |

### Phase 1 what was built (all in commit `f0a31c81`)
- `AlphaZero.Shared/Infrastructure/Caching/IEvictionProvider.cs` — rewritten to handoff §1.4:
  `IsActive`, `HasPendingKeys`, `Begin()`, `Evict(CacheKey)`, `Evict(IEnumerable<CacheKey>)`,
  `EvictTag(CacheTag)`, `FlushAsync(ct)`, `Discard()`. No staging states, no `ConfirmSave`.
- `ScopedEvictionProvider.cs` — two `AsyncLocal<HashSet<…>>` lanes (keys + tags, dedupe free).
  `FlushAsync` snapshots-and-clears BOTH lanes in place before touching the cache; `RemoveAsync`
  per key then `RemoveByTagAsync` per tag; `OperationCanceledException` propagates; every other
  exception logged (`LogWarning`) and swallowed; `Discard()` clears both lanes.
- `UnitOfWork.cs` — `EvictionSaveChangesInterceptor` + `EvictionTransactionInterceptor` complete:
  standalone save success → flush; save failure → Discard (only when no transaction);
  commit → flush exactly once; both rollback overrides → Discard; `TransactionFailedAsync` →
  commit-path `FlushAsync(CancellationToken.None)` else Discard; **ambient `System.Transactions`
  FAILS FAST — the guard THROWS** `InvalidOperationException` (this is the implemented reading of
  handoff §5/§6 "the guard throws"; an ambient commit has no EF hook to flush at, so a silent
  skip would serve stale cache).
- `ICachingService.cs` — `Evict(CacheKey)` / `Evict(IEnumerable<CacheKey>)` overloads added
  (`HybridCachingService` routes key-evicts through the provider, tag-evicts through `EvictTag`).
  Used by Identity module (`CachingTenantUserPrincipalAssignmentRepository.cs:57,63`).
- `DatabaseSettings.GetDatabaseSettings` — appends `Enlist=false` when absent.
- Courses `Infrastructure/DependencyInjection.cs` — both interceptors wired via
  `opts.AddInterceptors(...)`; `CacheEvictionFlushBehavior` `AddOpenBehavior` removed.
- DELETED: `CacheEvictionFlushBehavior.cs`, `EvictionFlushScope.cs`, `EvictionPipelineTests.cs`,
  `CoursesEvictionCoverageInterceptor.cs` + its tests, plus (already staged on arrival)
  `IEvictionStrategy.cs`, `IEntityEvictionPolicy.cs`, `ChangeTrackerEvictionStrategy.cs`,
  `CourseEvictionPolicy.cs`, `CourseEvictionPolicyContractTests.cs`.
- NEW tests: `ScopedEvictionProviderTests.cs` (two-lane matrix: dedupe, snapshot-clear, cancel
  propagates, fail-log-swallow, discard, empty no-op, Begin semantics, outside-Begin queueing),
  `EvictionInterceptorTests.cs` (full matrix driven by REAL saves against a SQLite stub context —
  see §6 environment facts), `CoursesRepositoryDecoratorTests.cs` (transitional tag assertions).

### Phase 1 deviations (all reported and user-visible; do not "fix" them away)
1. Interceptors RENAMED: `UnitOfWorkOutsideTransactionSaveChangesInterceptor` →
   `EvictionSaveChangesInterceptor`; `EvictionTransactionInterceptorCommitInterceptor` →
   `EvictionTransactionInterceptor` (draft-only names, previously unreferenced).
2. `CoursesEvictionCoverageInterceptor` deletion was pulled INTO Phase 1 (compile-forced: it read
   `HasPendingTags` and `ChangeTracker.Entries()` — both outlawed).
3. `Microsoft.EntityFrameworkCore.Sqlite` added to `Courses.UnitTests` (via `Directory.Packages.props`)
   for the real-save harness.
4. Transitional decorator adaptation: the 4 Courses decorators currently loop their OLD tag sets
   through `provider.EvictTag(tag)` — Phase 2 replaces this entirely.
5. **PRE-EXISTING UNRELATED BREAK:** `tests/Modules/VideoUploading/VideoUploading.Tests.Integration/
   CreatePlaybackSessionHandlerUnitTests.cs` — CS1503 ×4 (`FakeCurrentTenantUserRepository` →
   `IAuthorizationContextProvider`, `:91,117,150,185`) + CS1061 (`WatermarkContextDto.UserPhone`,
   `:212`). Present at branch HEAD, zero working-copy edits to those paths. Solution build FAILS on
   this alone. The user was asked to decide (fix out-of-scope vs exclude) but has not answered —
   ASK at the Phase 2 gate or the final-verification build will keep failing.

---

## 2. DECISION LEDGER (all grilling closed — binding)

### Grilling-phase decisions (user Q&A, file/line evidence demanded)
| # | Decision |
|---|---|
| G1 | Asset single-owner: `AssignAssetToCurriculum.cs:40-41`, `CourseMediaAsset.cs:28`. |
| G2 | Empty `CourseId` = unassigned pool asset → queues NOTHING (unit test required; `EvictCourseAsset` returns `[]` for `CourseId == Guid.Empty`). |
| G3 | No parent moves (private-set `CourseSection.CourseId` / `CoursePlan.CourseId`). |
| G4.1 | Fan-out tag = tenant-wide `courses:views` (name unchanged; `CourseCacheTags` keeps exactly ONE builder). Carried ONLY by the public-view entry (`CachedCourseQueryService.cs:83`). Staff-tree entry (line ~97) drops the tag. Freshness immediate at flush; documented worst-case bound = 10-min view TTL. |
| G4.2 | Subject writes queue `SubjectList` key + `SubjectEntity` key + `EvictTag(courses:views)`. Student-catalog staleness after subject rename is ACCEPTED (2-min TTL; rare operations). |
| G11 | Repository read-through scope = overview `GetByCourseIdAsync` ONLY (returns `CourseOverviewDto`, STJ-safe flat record), under new `CourseOverviewEntity` key keyed by courseId. Read-side tenant = ambient `ITenantProvider` (same as `CachedCourseQueryService.cs:29`). Collection/expression reads bypass. |
| G12 | ALL aggregate `GetById`-shaped reads delegate UNCACHED — `Course.GetById` is a tracked-identity bridge (`CourseActiveItemsDomainEventHandler.cs:68-76` needs the same tracked instance); asset/subject GetByIds are write-preceding loads (explicit `Update` re-commits as-of-load); `*Entity` keys serve eviction only. NEVER fix with ChangeTracker. |
| Conversion | Handlers must not rely on ChangeTracker; explicit repository `Update` for every mutation. Includes Phase 3 `AddSection` fix + explicit `_courseRepository.Update(course)` in the three mutating paths of `CourseActiveItemsDomainEventHandler` (`:41-48, :50-57, :59-66`). Asset/subject handlers already explicit. |

### Confirmed defaults
- Ambient `System.Transactions` guard THROWS (fail-fast) — implemented in Phase 1.
- DI lifetimes: `IEvictionProvider`, both interceptors stay SCOPED (singleton would create a
  captive-dependency cycle: provider → `ICachingService` → provider). Code comments note they MAY
  be singletons because queue state is static AsyncLocal.
- Retry-on-failure execution strategy wraps retries in internal transactions → flush routes
  through the commit interceptor automatically (nothing to do).
- Only explicit-transaction site: `Modules/Courses/Infrastructure/Behaviors/TransactionalBehavior.cs`.

### Handoff §5 decisions snapshot (restates plan rows it supersedes)
Currency = `CacheKey`/`CacheTag` values (NOT entity graphs); flush by key-set from the aggregate
argument; over-eviction always acceptable, under-eviction is a bug; flush failure after commit is
logged, never surfaced; no ChangeTracker reads in cache paths; no DB lookups in eviction/key
building; no caller paging/search/sort in keys; keys always tenant-scoped via `CacheScope`
(`Guid.Empty` tenant → queue nothing).

---

## 3. PHASE 2 — Courses decorators (NEXT)

Gate to STOP at: `dotnet test Courses.UnitTests` green → STOP for user review.

1. **`CourseCacheKeys.cs`** (Modules/Courses/Infrastructure/Caching/):
   - ADD read-through entity keys: `CourseEntity(scope, courseId)`, `CourseOverviewEntity(scope, courseId)`,
     `CourseAssetEntity(scope, assetId)`, `SubjectEntity(scope, subjectId)` (+ a policy, e.g. 5m per
     handoff §2 tree `<T>:entity:course:{courseId}` TTL 5m).
   - ADD key-set helpers: `EvictCourse(scope, id)` → 8 keys (catalog:staff, catalog:student, tree,
     view:public, overview, readiness, status, entity:course), `EvictCourseOverview(scope, id)` → 5 keys
     (overview, readiness, view:public, catalog:staff, catalog:student), `EvictCourseAsset(scope, id)`
     → 4 keys (readiness, view:public, catalog:staff, catalog:student — NOT tree; returns `[]` for
     `CourseId == Guid.Empty`), `EvictSubject(scope, subjectId)` → `SubjectList` + `SubjectEntity`.
   - DELETE tag builders: `CatalogTag` (:54), `SubjectsTag` (:56), `CourseTag` (:63),
     `CourseAssetsTag` (:68). `CourseViewsTag` (:60) is THE fan-out tag — keep it (or move it into
     the narrowed `CourseCacheTags`, which per handoff §2 holds "the single fan-out builder";
     exactly one registration + one builder must survive — see the §10 grep for `CourseCacheTags`).
2. **`CourseCacheTags.cs`** — narrow to a single builder for the `courses:views` fan-out tag (G4.1);
   delete `ForCourse`/`ForCourseOverview`/`ForAsset`/`ForSubject` tag-mapping methods.
3. **Convert the 4 write decorators** (`CachingCourseRepositoryDecorator.cs` :69-72,
   `CachingCourseOverviewRepositoryDecorator.cs` :64-67, `CachingCourseAssetRepositoryDecorator.cs`
   :60-63, `CachingSubjectRepositoryDecorator.cs` :61-64) — currently transitional tag loops:
   replace with key-set eviction via the new helpers (`_eviction.Evict(keys)`; subject decorator is
   the ONLY place `EvictTag` remains in Modules/Courses — handoff §10 grep expects exactly that).
   Write decorators must NOT read `ITenantProvider` — tenant comes from the aggregate
   (`course.TenantId`, `asset.TenantId`, `subject.TenantId`); empty tenant → queue nothing.
4. **Read-side (`CachedCourseQueryService.cs`, `CachedSubjectQueryService.cs`)** — drop tag args
   EXCEPT the public-view entry keeps `[CourseViewsTag]` (G4.1; today `:83` carries
   `[CourseTag, CourseViewsTag]` — becomes only the fan-out tag) and the staff-tree entry (~:97)
   drops tags entirely.
5. **G11 read-through** — `CachingCourseOverviewRepositoryDecorator.GetByCourseIdAsync` becomes the
   ONLY cached single-entity read: `GetOrCreateAsync(CourseOverviewEntity key, policy, [],
   factory)`. All `GetById`/`GetByIds`-shaped reads in Course/Asset/Subject decorators delegate
   UNCACHED (G12). Collection/expression reads bypass.
6. **Decorator tests** — rewrite `CoursesRepositoryDecoratorTests.cs`: §2 key-set assertions
   (exact keys queued per write row of the eviction matrix), provenance (no literal key/tag strings),
   empty-tenant / `CourseId == Guid.Empty` cases, read-through hit/miss for the overview decorator,
   and DELETE the tag assertions.

## 4. PHASE 3 — AddSection fix + integration suite

1. **`AddSection.cs`** (~line 40): add `_courseRepository.Update(course)` after
   `course.AddSection(...)` (verified bypass bug; integration test
   `CourseView_AddSection_EvictsWarmedTree` fails until fixed).
2. **`CourseActiveItemsDomainEventHandler.cs`**: add explicit `_courseRepository.Update(course)`
   to the three mutating paths (`:41-48, :50-57, :59-66`). DO NOT touch `:68-76` — `Course.GetById`
   is the tracked-identity bridge (G12); the same tracked instance is required.
3. **`Courses.Tests.Integration/CoursesCacheTests.cs`** — build the eviction matrix integration
   suite per handoff §6. Run the integration suite ALONE (`dotnet test
   tests/Modules/Courses/Courses.Tests.Integration/...` by itself) — known MSB4018 file-lock issue
   when run with other test projects.

## 5. PHASE 4 — docs

- `spec-33/code-review-plan.md` amendments: §4.2/§4.3/§4.8, decisions D22/D23/D24, fill ledger §11.
- `Modules/Courses/CACHING.md`, `docs/caching/README.md`, `docs/CachingArchitecture.md` — correct
  false AddSection claims (the bypass bug is now FIXED, docs must stop claiming it's cached-unsafe).
- Document: G4.1 fan-out tag semantics + 10-min worst-case staleness bound; G4.2 accepted
  2-min student-catalog staleness after subject rename (rare-op rationale).

## 6. FINAL VERIFICATION (handoff §10 — run exactly these)

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

dotnet build AlphaZero.sln --nologo -v q; then unit + integration suites run SEPARATELY
(integration ALONE — known MSB4018 file-lock issue).
```
Final report must include: filled decision ledger, files changed per phase, test results, grep
outputs. No silent gaps.

---

## 7. Environment facts / workarounds (learned the hard way — reuse)

- Build: `dotnet build AlphaZero.sln --nologo -v q` from repo ROOT (the .sln is at root, not in
  `src/alphazero-api`).
- Central package management via `Directory.Packages.props` — add versions there + bare
  `<PackageReference>` in csproj.
- **NO SQLite/InMemory provider for the MODULE context** — `AppDbContext` model uses Postgres-only
  `jsonb`/`varbit` (`CourseAnalyticsConfiguration.cs:16`, `CourseConfiguration.cs:49`,
  `CourseOverviewConfiguration.cs:26/32/38`, `EnrollmentConfiguration.cs:39`). But the interceptors
  are context/provider-agnostic → the interceptor matrix uses a minimal `StubDbContext` + SQLite
  in-memory (kept-open `SqliteConnection`, `EnsureCreated`). Real saves; force failures by
  `DROP TABLE` on the connection; real transactions via `Database.BeginTransactionAsync()`
  (assert through the cache stub `RemoveAsync`/`RemoveByTagAsync` — the provider is REAL, so
  `provider.Received()` will throw NotASubstituteException; assert via the `ICachingService` stub).
- `.NET 10`: `DbTransaction.Connection` is non-virtual (CS0506); the abstract member is
  `protected DbConnection DbConnection { get; }`. `UseTransaction` OPENS the connection → fake-transaction
  staging over a bogus Npgsql endpoint fails ("Connection refused"); use the SQLite harness instead.
  `Database.UseTransaction(fakeTx)` + `FakeDbTransaction` pattern still works in OTHER tests where
  the connection is never opened.
- EF Core 10 event-data ctors (for direct hook invocation) all start with
  `(EventDefinitionBase, Func<EventDefinitionBase, EventData, string>)` — pass `null!, null!`:
  - `SaveChangesCompletedEventData(def, gen, DbContext, int entitiesSavedCount)`
  - `DbContextErrorEventData(def, gen, DbContext, Exception)`
  - `TransactionEndEventData(def, gen, DbTransaction, DbContext?, Guid transactionId, Guid connectionId, bool async, DateTimeOffset startTime, TimeSpan duration)` (in EFCore.Relational)
  - `TransactionErrorEventData(def, gen, DbTransaction, DbContext?, Guid transactionId, Guid connectionId, bool async, string action, Exception, DateTimeOffset, TimeSpan)`
  - `ToString()` is lazy — the nulls are never dereferenced unless you log the event.
- `ICachingService.GetOrCreateAsync(key, policy, tags, factory, ct)` — tags param is
  `IReadOnlyCollection<CacheTag>`; `[]` = none; HybridCache L1-only (no Redis).
- `AddHybridCache` options mutation fix (10m absolute / 5m local) must not regress — covered by
  `SharedCachingRegistrationTests.cs`.
- Courses read key functions at HEAD (`CourseCacheKeys.cs:27-49`): `StaffCatalog`, `StudentCatalog`,
  `CourseView`, `CoursePublicView`, `CourseOverview`, `CourseReadiness`, `SubjectList`; policies at
  `:19-23` (catalog 2m, view 10m, overview 10m, readiness 3m, subjects 15m).
- Subjects: `SubjectName` embedded ONLY in public view (`CourseQueryService.cs:198`) and student
  catalog (`:137`); staff tree `:37` + staff catalog `:76` carry SubjectId only; readiness `:276`
  checks SubjectId existence only.
- `dotnet test` integration suite must run ALONE (MSB4018 file-lock).

## 8. Key files map

- Contract: `spec-33/spec-33-caching-handoff.md` (§1.3 interceptor code, §1.4 provider contract,
  §1.5 read rules, §2 tree/matrix/helpers, §3 deletions, §5 decisions, §6 tests, §10 greps).
- Plan facts: `spec-33/code-review-plan.md` (F1-F22, grilling §6 — CLOSED, ledger §11 to fill).
- Shared caching: `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/` (IEvictionProvider,
  ScopedEvictionProvider, ICachingService, HybridCachingService) + `UnitOfWork.cs` (interceptors),
  `DatabaseSettings.cs`, `DependencyInjection.cs`.
- Courses caching: `src/alphazero-api/Modules/Courses/Infrastructure/Caching/` — `CourseCacheKeys.cs`,
  `CourseCacheTags.cs` (4 decorators untracked-in-history until `f0a31c81`),
  `CachedCourseQueryService.cs`, `CachedSubjectQueryService.cs`, 4 × `Caching*RepositoryDecorator.cs`.
- Phase 3 targets: `Modules/Courses/Application/Courses/Commands/AddSection/AddSection.cs`,
  `Modules/Courses/Application/Courses/EventHandlers/CourseActiveItemsDomainEventHandler.cs`.
- Tests: `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/` (provider, interceptor,
  decorator suites), `tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs`.
