# Issue: Courses eviction should be emitted by write-side repository decorators

**Status:** Open — design agreed in principle, needs the remaining decisions closed before
implementation  
**Branch:** `spec/33-caching`  
**Base:** `a1ebb79e`  
**Area:** Courses write path / cache eviction  
**Related:** issue #33, [`spec-33/code-review-plan.md`](../../../../spec-33/code-review-plan.md),
[`docs/caching/README.md`](../../README.md),
[`Modules/Courses/CACHING.md`](../../../../src/alphazero-api/Modules/Courses/CACHING.md)

---

## How to use this document (for the next AI)

You are the **grilling facilitator**, not the implementer.

1. Read this document completely. Treat "Evidence" as verified; treat "Assumptions" as things to
   challenge.
2. Invoke the grilling workflow (`/grilling`) with this document as the plan.
3. Ask **one question at a time**, in the order of the agenda in section 10. Do not batch
   questions and do not accept "it should be fine".
4. For every claim, demand one of: a file/line reference, a measured query count, a test result,
   or documented EF behavior.
5. Record every answer in the decision ledger (section 11). A decision is closed only when the
   user states the option, the rationale, and the consequence.
6. Do **not** write implementation code until all decisions in section 10 are closed.
7. If the user changes a decision later, reopen the dependent decisions explicitly.

The goal is to settle **where eviction lives** and **how coverage is guaranteed**, not to defend
any particular implementation.

---

## 1. Problem statement

The current design derives eviction tags from the **EF change tracker**:

- `UnitOfWork.SaveChangesAsync` calls `IEvictionStrategy.CollectAsync` before the write
  ([`UnitOfWork.cs:49`](../../../../src/alphazero-api/AlphaZero.Shared/Infrastructure/UnitOfWork.cs)).
- `ChangeTrackerEvictionStrategy` scans every Added/Modified/Deleted entry and dispatches on the
  concrete CLR type
  ([`ChangeTrackerEvictionStrategy.cs:22-46`](../../../../src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/ChangeTrackerEvictionStrategy.cs)).
- `CourseEvictionPolicy` maps each entity to tags and, when the parent/tenant is not on the
  entity, **queries the database** to walk
  `CurriculumResource → CurriculumItem → CourseSection → Course`
  ([`CourseEvictionPolicy.cs:34-188`](../../../../src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseEvictionPolicy.cs)).

Two problems follow:

1. **Write-path SQL.** A curriculum edit issues 1–3 extra queries per changed child before the
   actual `SaveChanges` (section 4.4). The data is already loaded and tracked by the command.
2. **Shared-kernel EF coupling.** The whole pipeline exists so a module policy can inspect EF
   entries. Only Courses uses it. Identity, Tenants and VideoUploading invalidate explicitly
   (they call `ICachingService.RemoveByTagAsync` at the consumer boundary).

The proposal under review: **move tag emission into write-side repository decorators**, mirroring
the read-side decorators. Each decorator method receives the aggregate that was actually written,
so tenant and course identity are on the argument — no lookup, no change-tracker scan, no EF in the
shared caching layer.

```text
handler (cache-agnostic)
    │  mutates aggregate, calls repository.Add/Update/Remove
    ▼
CachingCourseRepositoryDecorator          ← knows Course.TenantId + Course.Id
    │  IEvictionProvider.Evict(course tag, catalog tag)
    ▼
inner repository → DbSet.Update
    ▼
UnitOfWork → SaveChanges
    ▼
CacheEvictionFlushBehavior (outermost) → flush after commit
```

---

## 2. Why the decorator boundary is the right one

- **The aggregate is the unit of write.** Every curriculum mutation is persisted through the
  `Course` root (sections, items, resources, plans), so one Course decorator covers the whole
  graph. Eviction becomes aggregate-level, exactly like the cached reads.
- **Scope is on the argument.** `Course`, `CourseOverview`, `CourseAsset` and `Subject` all carry
  `TenantId` (and course id where relevant). Tag construction is O(1) and pure.
- **Symmetric with reads.** `CachedCourseQueryService` / `CachedSubjectQueryService` own cache
  logic on read. Write decorators own it on write. Commands and handlers stay cache-agnostic.
- **The shared kernel gets smaller.** `IEvictionStrategy`, `IEntityEvictionPolicy`,
  `ChangeTrackerEvictionStrategy` and `CourseEvictionPolicy` can be deleted. The ambient queue and
  the flush-after-commit behavior stay — they do not depend on how tags are produced.

---

## 3. Evidence

### 3.1 The write surface is small and already aggregate-shaped

| Repository | Registered as | Write methods | Scope available on the entity |
|---|---|---|---|
| `ICourseRepository` | [`DependencyInjection.cs:45`](../../../../src/alphazero-api/Modules/Courses/Infrastructure/DependencyInjection.cs) | `Add`, `Update`, `Remove` | `Course.TenantId`, `Course.Id` |
| `ICourseOverviewRepository` | `:47` | `Add`, `Update`, `Remove` | `TenantId`, `CourseId` |
| `IRepository<CourseAsset>` | `:48` | `Add`, `Update`, `Remove` | `TenantId`, `CourseId` |
| `ISubjectRepository` | `:49` | `Add`, `Update`, `Remove` | `TenantId` |

`IRepository<TEntity>` declares the three write methods at
[`IRepository.cs:11-13`](../../../../src/alphazero-api/AlphaZero.Shared/Infrastructure/Repositores/IRepository.cs);
all other members are reads and pass through undecorated (repository reads are deliberately not
cached).

`IEnrollmentRepository` and `ICourseAnalyticsRepository` do **not** need eviction: enrollment and
dashboard surfaces are uncached by design (D3 in the review plan).

### 3.2 Curriculum writes always go through the Course root

Sixteen call sites call `_courseRepository.Update(course)` / `.Add(course)`:

- `SectionCommands.cs:52,79`
- `CourseSectionItemCommands.cs:249,283,308`
- `UpdateCourse.cs:41`
- `DeleteCourse.cs:32`
- `AssignAssetToCurriculum.cs:80`
- `UnassignAssetFromCurriculum.cs:40`
- `AddPlan.cs:43`
- `CourseStateCommands.cs:53,67,81,101,115`
- `CreateCourse.cs:55` (`Add`)

Sections, items, resources and plans are mutated through the tracked aggregate and reach the
database through `Update(course)` — so no per-entity decorator is needed.

### 3.3 Four handlers currently bypass every repository write

These mutate a tracked aggregate and rely on implicit `SaveChanges`. They work today **only**
because the change-tracker strategy observes their entries. Under decorator-only eviction they
would emit no tags and silently serve stale caches:

| Handler | Location | Missing call |
|---|---|---|
| `AddSectionCommandHandler` | [`AddSection.cs:32-45`](../../../../src/alphazero-api/Modules/Courses/Application/Courses/Commands/AddSection/AddSection.cs) | `_courseRepository.Update(course)` |
| `UpdatePlanCommandHandler` | [`UpdatePlan.cs:34-47`](../../../../src/alphazero-api/Modules/Courses/Application/Courses/Commands/Plans/UpdatePlan.cs) | `_courseRepository.Update(course)` |
| `RemovePlanCommandHandler` | [`RemovePlan.cs:32-45`](../../../../src/alphazero-api/Modules/Courses/Application/Courses/Commands/Plans/RemovePlan.cs) | `_courseRepository.Update(course)` |
| `ReorderCommandsHandlers` (sections and items) | [`ReorderCommands.cs:28-51`](../../../../src/alphazero-api/Modules/Courses/Application/Courses/Commands/Reorder/ReorderCommands.cs) | `_courseRepository.Update(course)` |

This is the concrete cost of opt-in eviction and is why the change is only safe with the fixes in
section 6 and the guard in section 7.

`CourseActiveItemsDomainEventHandler` also mutates the tracked `Course` (active-mask bits), but it
runs inside `SaveChangesAsync` **after** the originating command queued its tag — e.g. a section
delete calls `Update(course)` before the save. It does not need its own tag, but the guard must not
flag it (it will not: the queue is already populated for that save).

### 3.4 Query cost of the current design (to be measured before removal)

Per changed entry, `CourseEvictionPolicy` issues:

| Changed entity | Queries |
|---|---|
| `Course`, `CourseOverview`, `CourseAsset`, `Subject` | 0 |
| `CourseSection`, `CoursePlan` | 1 |
| `CurriculumItem` | 2 |
| `CurriculumResource` | 3 |

Because `Lookup` is created per entry (`CourseEvictionPolicy.cs:39`), nothing is shared across
entries. A 20-item reorder costs ~40 queries; a section delete with 20 items costs ~41.
Under the decorator design all of these are **0**.

### 3.5 EF facts the current design relies on (and the decorator design does not)

- `ChangeTracker.Entries()` calls `DetectChanges()`; `DbSet<T>.Local` does not. Both become
  irrelevant here.
- `courseRepository.Update(course)` calls `DbSet.Update`, which marks the root and reachable
  not-yet-tracked entities `Modified`, but leaves already-tracked unchanged children `Unchanged`
  (probe result from the earlier session). This is a persistence detail, not an eviction
  requirement.

---

## 4. Tag sets to preserve exactly

The decorators must emit the same tags the policy emits today
([`CourseEvictionPolicy.cs:55-90`](../../../../src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseEvictionPolicy.cs)):

| Write | Tags |
|---|---|
| `Course` add/update/remove | `courses:course:{tenant}:{courseId}` + `courses:catalog:{tenant}` |
| `CourseOverview` add/update/remove | `courses:course:{tenant}:{courseId}` |
| `CourseAsset` add/update/remove | `courses:course:{tenant}:{courseId}` if `CourseId != Guid.Empty`, else `courses:assets:{tenant}` |
| `Subject` add/update/remove | `courses:subjects:{tenant}` + `courses:catalog:{tenant}` + `courses:views:{tenant}` |

Empty tenant or empty course id must emit nothing (same guard as today). Tag shapes come from
[`CourseCacheKeys.cs:54-68`](../../../../src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseCacheKeys.cs)
and must not change.

---

## 5. Proposed implementation

### 5.1 New files (Courses infrastructure)

| File | Purpose |
|---|---|
| `Infrastructure/Caching/CourseCacheTags.cs` | Pure tag builders moved from `CourseEvictionPolicy`: `ForCourse`, `ForCourseOverview`, `ForAsset`, `ForSubject` |
| `Infrastructure/Caching/CachingCourseRepositoryDecorator.cs` | `ICourseRepository` write methods queue course tags; reads delegate |
| `Infrastructure/Caching/CachingCourseOverviewRepositoryDecorator.cs` | Same for `ICourseOverviewRepository` |
| `Infrastructure/Caching/CachingCourseAssetRepositoryDecorator.cs` | Same for `IRepository<CourseAsset>` |
| `Infrastructure/Caching/CachingSubjectRepositoryDecorator.cs` | Same for `ISubjectRepository` |
| `Infrastructure/Caching/CoursesEvictionCoverageInterceptor.cs` | Save-time guard: a cache-feeding Courses entity persisted with no queued tag logs a warning |

Registration uses the existing Scrutor pattern:

```csharp
moduleServices.AddScoped<ICourseRepository, CourseRepository>();
moduleServices.Decorate<ICourseRepository, CachingCourseRepositoryDecorator>();
// ...same for ICourseOverviewRepository, ISubjectRepository,
// and the closed generic IRepository<CourseAsset>.
```

### 5.2 Deletions

- `Modules/Courses/Infrastructure/Caching/CourseEvictionPolicy.cs`
- `AlphaZero.Shared/Infrastructure/Caching/IEntityEvictionPolicy.cs`
- `AlphaZero.Shared/Infrastructure/Caching/ChangeTrackerEvictionStrategy.cs`
- `AlphaZero.Shared/Infrastructure/Caching/IEvictionStrategy.cs`
- The `IEvictionStrategy` registration in `Shared/Infrastructure/DependencyInjection.cs:54`
- The `IEntityEvictionPolicy` registration in `Courses/Infrastructure/DependencyInjection.cs:77`
- The `CollectAsync` call and constructor dependency in `UnitOfWork`

Kept unchanged: `IEvictionProvider`, `ScopedEvictionProvider`, `EvictionFlushScope`,
`CacheEvictionFlushBehavior`. The queue is still flow-local and still flushed only after the
outermost commit; errors and exceptions still discard it.

### 5.3 Handler fixes

Add `Update(course)` to the four handlers in section 3.3.

### 5.4 Coverage guard

Decorator eviction is opt-in. The guard makes a bypass observable instead of silently stale:

- Extend `IEvictionProvider` with `bool IsActive` and `bool HasPendingTags`.
- `CoursesEvictionCoverageInterceptor : SaveChangesInterceptor` runs inside
  `DbContext.SaveChangesAsync` (registered next to `SoftDeleteInterceptor` in
  `AddCoursesGlobalInfrastructure`).
- If a save is inside an active eviction scope, has **no** pending tags, and contains an
  Added/Modified/Deleted entry of a Courses cache-feeding type, it logs a warning naming the entity
  types.
- It never throws, never queries, and does nothing when no dispatch scope is active (direct DB
  seeding in tests stays quiet).

Limitation to state explicitly: the guard detects **zero-tag** saves. A command that covers one
entity and bypasses the repository for another is not detected. That case requires bespoke
bookkeeping and is out of scope; the four-repository surface keeps it small.

---

## 6. Non-negotiable constraints

1. Commands and handlers stay cache-agnostic: no tag construction, no `IEvictionProvider`.
2. Eviction stays a persistence-infrastructure concern: decorators are infrastructure, not
   application.
3. Over-eviction is acceptable; under-eviction is a bug.
4. No cache and no repository reads during eviction. Decorators only read the entity argument.
5. Tags carry the owning entity's tenant; never substitute the ambient request tenant.
6. Soft-deleted and hard-deleted aggregates still evict (the decorator receives the entity).
7. No schema change.
8. `OperationCanceledException` continues to propagate; flush failures after commit are logged and
   swallowed.
9. Cache keys, tag shapes and TTLs do not change.
10. Repository reads are not cached (only query services are).

---

## 7. Acceptance criteria

- A tracked curriculum mutation (section add/rename/delete, item add/update/delete, reorder,
  resource replacement, plan add/update/remove) executes **zero SQL statements** for eviction.
- Every Courses write handler calls a decorated repository write method (audited + guard).
- The emitted tag set per operation is identical to the current policy (unit-asserted).
- `Course`/`CourseOverview`/`CourseAsset`/`Subject` writes each evict via their own decorator.
- A save that persists a cache-feeding entity with no queued tag logs the guard warning.
- No change-tracker scan remains on the save path.
- Existing cache and eviction-pipeline tests pass unchanged in outcome.
- Build has no new warnings; no unused shared caching types remain.

---

## 8. Test plan

### 8.1 Unit (Courses.UnitTests / Infrastructure/Caching)

1. `CachingCourseRepositoryDecorator` — `Add`/`Update`/`Remove` each queue
   `{course tag, catalog tag}`; reads delegate and queue nothing.
2. `CachingCourseOverviewRepositoryDecorator` — course tag only.
3. `CachingCourseAssetRepositoryDecorator` — course tag when `CourseId` is set; `courses:assets`
   fallback when it is empty.
4. `CachingSubjectRepositoryDecorator` — subjects + catalog + views tags.
5. `CoursesEvictionCoverageInterceptor` — warns when active with no tags and a cache-feeding entry;
   stays silent when tags exist, when the scope is inactive, or when only uncached entities are
   saved.
6. Remove/replace `CourseEvictionPolicyContractTests` with the decorator tests above plus a
   registration test asserting each write repository resolves through its caching decorator.

### 8.2 Integration (Courses.Tests.Integration)

- Re-run `CoursesCacheTests` — especially
  `CourseView_ReorderEvictsTree_ColdRefillShowsNewOrder`, which now depends on the explicit
  `Update(course)` fix rather than the change tracker.
- Add a plan mutation test: warm the course view/readiness, `PUT`/`DELETE` a plan, assert the next
  read is fresh.
- Add an add-section eviction test if not already covered by the tree test.
- `EvictionPipelineTests` must keep passing (queue, nesting, discard, cancellation semantics).

### 8.3 Measurement

Record the SQL statement count for a 20-item reorder and a section delete with 20 items before and
after (interceptor or EF logging). Expected: 0 eviction queries after.

---

## 9. Migration and rollback

- The change is internal to the Courses module plus the shared eviction pipeline; no data or
  contract migration.
- Rollback is reverting the commit: the old tracker machinery is deleted, so there is no dual path.
- Ship the four handler fixes and the decorators in the same change; do not delete the tracker
  first.

---

## 10. Grilling agenda

### D1 — Is decorator eviction the chosen design?
**Ask:** Confirm write-side decorators own tag emission and the change-tracker pipeline is
removed, rather than kept as a fallback.  
**Why:** this is the decision that reopens review-plan D4; everything else depends on it.  
**Demand:** explicit yes/no and what is deleted.

### D2 — Are the four bypassing handlers fixed in this change?
**Ask:** `AddSection`, `UpdatePlan`, `RemovePlan`, `ReorderSections`/`ReorderItems` mutate tracked
aggregates without a repository write. Fix all four now?  
**Why:** without these calls, decorator eviction silently misses real user paths that work today.  
**Demand:** confirmation plus a test for at least reorder and plan mutation.

### D3 — What should the coverage guard do?
**Ask:** Warn, throw, or environment-gate? Always-on or tests only?  
**Why:** a silent bypass is stale reads; a throw turns a cache bug into a failed write.  
**Demand:** chosen behavior, log level, and the scope-inactive rule.

### D4 — One decorator per repository, or a generic decorator?
**Ask:** Four small typed decorators (recommended) or one generic decorator plus a tag mapper?  
**Why:** the generic version reintroduces a type switch and loses compile-time coverage.  
**Demand:** the accepted amount of boilerplate.

### D5 — Are the tag sets in section 4 exactly what ships?
**Ask:** Walk the table. Is per-course asset eviction plus the `courses:assets` fallback still
correct? Is `Subject` still tenant-wide?  
**Why:** tag shapes are a correctness contract; over-eviction is acceptable but avoidable
regressions are not.  
**Demand:** an explicit sign-off per row.

### D6 — Do parent links ever move?
**Ask:** Can a section move to another course, an item to another section, or a resource to another
item?  
**Why:** a move would need tags for both the old and new course. The decorator receives only the
root `Course`, so a move implemented outside the root would be invisible.  
**Demand:** grep of setters/move methods; if "not currently", state whether it is permanent.

### D7 — What stays in the shared caching layer?
**Ask:** Keep `IEvictionProvider`, `EvictionFlushScope`, `CacheEvictionFlushBehavior` as-is?  
**Why:** the ambient queue and outermost flush are orthogonal to tag production and already
tested.  
**Demand:** yes/no and any provider API additions (`IsActive`, `HasPendingTags`).

### D8 — What proves no under-eviction?
**Ask:** Which tests and observability gates are required before merge?  
**Why:** the failure mode is silent stale reads, not a crash.  
**Demand:** the unit tests in 8.1, the integration tests in 8.2, and the guard.

### D9 — Is `courseRepository.Update(course)` still required for persistence?
**Ask:** For tracked aggregates, `Update` is not needed to persist changes. Is it now the
eviction marker, and is that acceptable?  
**Why:** the decorator only sees calls that actually happen; `Update` becomes semantically
"persist and evict".  
**Demand:** a statement that every write handler must call it, enforced by the guard and tests.

---

## 11. Decision ledger (fill during the session)

| ID | Decision | Rationale | Consequences / follow-up |
|---|---|---|---|
| D1 | | | |
| D2 | | | |
| D3 | | | |
| D4 | | | |
| D5 | | | |
| D6 | | | |
| D7 | | | |
| D8 | | | |
| D9 | | | |

When the ledger is complete, summarize the chosen path in five bullets: decorator surface, tag
sets, handler fixes, guard behavior, tests. Only then start implementation.

---

## 12. Out of scope

- Changing cache keys, tags or TTLs.
- Redis/L2 or cross-instance invalidation.
- Caching repository reads.
- Removing `courseRepository.Update(course)` from write handlers.
- Denormalizing `CourseId`/`TenantId` onto `CurriculumItem`/`CurriculumResource` (no longer needed
  for eviction; remains a separate data-model question).
- Optimizing read-side cache misses or `HybridCache` serialization.

---

## 13. Assumptions to challenge

- "Every curriculum write goes through `Update(course)`." Four handlers currently do not — fixed
  in this change.
- "All four write repositories are the only persistence paths." Verify no background service or
  consumer uses `DbContext` directly for these entities.
- "`CourseActiveItemsDomainEventHandler` never runs before the originating tag is queued." Verify
  the command calls `Update` before returning; the handler runs inside the later save.
- "A zero-tag guard is enough." Partial-coverage bypasses are not detected; decide whether that
  residual risk is acceptable.
- "The decorator does not need the changed child." True only while parents do not move (D6).
