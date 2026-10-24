# Issue #33 caching — code review and solution plan

Generated 2026-10-23 from a read-only review of the `spec/33-caching` worktree. This file is
self-contained input for discussing solutions in other AI chats. No code was modified and no
tests were executed to produce it.

How to use it: each finding has evidence, impact, test blind spot, solution options,
a recommendation, acceptance criteria and open questions. The decision register in §5 lists
the choices that need to be settled before implementation.

---

## 1. Review context

| Item | Value |
|---|---|
| Worktree | `/home/azero/.local/share/opencode/worktree/adfa55/spec-33-caching` |
| Branch | `spec/33-caching` |
| HEAD | `a1ebb79e` (merge of `origin/CourseContentManagement` into the branch) |
| Pre-#33 base | `5947b942` (parent of `ac1c7827`) |
| Spec | GitHub issue #33, repo `Azero77/alpha-zero` |
| Conventions | `docs/agents/per-issue-runbook.md` §3 row #33, `docs/CachingArchitecture.md` |

The four #33 commits under review:

- `ac1c7827` — cache catalog / view / overview / readiness / subject listing via HybridCache decorators
- `7ac5c8e0` — tag-based eviction behavior after the UnitOfWork commit
- `ddcd53e1` — cache integration tests
- `ea751253` — `Modules/Courses/CACHING.md`

The branch also carries #29 and #32 work. The merge `a1ebb79e` brought #32's student-catalog
extensions into the same caching files, so **review the HEAD state of the caching files**, not
only the four-commit delta.

Fetch the spec:

```bash
gh issue view 33 --json number,title,body,comments
```

Review scope command used:

```bash
git diff 5947b942...HEAD -- \
  src/alphazero-api/Modules/Courses/Infrastructure/Caching \
  src/alphazero-api/Modules/Courses/CACHING.md \
  src/alphazero-api/Modules/Courses/Infrastructure/DependencyInjection.cs \
  tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs \
  tests/Modules/Courses/Courses.Tests.Integration/SoftDeleteTests.cs
```

Files that matter at HEAD:

- `Modules/Courses/Infrastructure/Caching/CachedCourseQueryService.cs`
- `Modules/Courses/Infrastructure/Caching/CachedSubjectQueryService.cs`
- `Modules/Courses/Infrastructure/Caching/CourseCacheEvictionBehavior.cs`
- `Modules/Courses/Infrastructure/Caching/CourseCacheKeys.cs`
- `Modules/Courses/CACHING.md`
- `Modules/Courses/Infrastructure/DependencyInjection.cs`
- `tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs`

What this review did **not** cover: the #29/#32 chain, a full build/test run, production Redis
configuration, or performance benchmarking.

Design constraints added after the first review pass (user decisions):

- The platform should expose a shared `ICachingService`; module code should not use
  `HybridCache` directly (§4).
- Eviction is a persistence responsibility. Commands and handlers stay cache-agnostic;
  `IEvictionProvider.Evict(tag)` is called only by an `IEvictionStrategy` that the UnitOfWork
  invokes (§4.7). No command marker interfaces, no request-type switch.
- Cached read keys must not include caller-controlled `search`/`sort`/`page`/`perPage` (§4.8).

---

## 2. Verified facts (re-derive only if the branch moves)

These are the assumptions every solution below depends on.

| # | Fact | Evidence |
|---|---|---|
| F1 | `HybridCache` is injected directly by Courses (2 decorators + 1 behavior) and by Identity, Tenants and VideoUploading. There is no shared cache abstraction. | `grep -rn "HybridCache" src/alphazero-api/Modules` |
| F2 | No distributed cache is registered. `AddHybridCache` is called without `AddStackExchangeRedisCache`, so caching is L1/in-process only despite issue #33 naming Redis. | `AlphaZero.Shared/Infrastructure/DependencyInjection.cs:68`; no `AddStackExchangeRedisCache` in `src/` |
| F3 | The `AddHybridCache` options lambda is a no-op: it reassigns its parameter (`o = new HybridCacheOptions()`) instead of mutating it. Intended 10m/5m defaults are never applied. Per-surface options currently mask this. | `AlphaZero.Shared/Infrastructure/DependencyInjection.cs:68-79` |
| F4 | `ITenantProvider` only reads an HTTP request (`tid` claim, then `X-TenantId` header). MassTransit consumers have no `HttpContext`, so `GetTenant()` returns null in background command flows. | `AlphaZero.Shared/Infrastructure/Tenats/ITenantProvider.cs:28-58` |
| F5 | The EF global tenant filter is `e.TenantId == context.TenantId`. With a null tenant that filters to no rows. Consumer-driven Courses handlers may therefore fail to load tenant-owned entities in production. This is pre-existing and masked by the test host's static tenant provider. | `AlphaZero.Shared/Infrastructure/Database/ModelBuilderExtensions.cs:55-76`; `Modules/Courses/Infrastructure/Persistance/AppDbContext.cs:35` |
| F6 | The three asset/metadata events consumed by Courses do not carry `TenantId`: `VideoPublishedIntegrationEvent(VideoId, RelativeUrl)`, `VideoProcessingFailedEvent(VideoId, Reason)`, `VideoMetadataChangedIntegrationEvent(VideoId, Title, Description)`. | `Modules/VideoUploading/IntegrationEvents/Events.cs:46-62,69-72` |
| F7 | `UpsertCourseOverviewCommand` implements `IRequest<ErrorOr<Success>>`, not `ICommand`, and the eviction behavior gates on `ICommand`. Its eviction case can never run, and (pre-existing) the UnitOfWork never saves it. | `Modules/Courses/Application/Courses/Commands/UpsertOverview/UpsertCourseOverview.cs:10-14`; `CourseCacheEvictionBehavior.cs:51` |
| F8 | The student catalog DTO carries volatile fields: `SectionCount`, `ItemCount`, `TotalDurationSeconds`, `Plans`, `CoverImageDocumentId`, timestamps, `SubjectName`. Section/item/plan/asset/metadata/overview mutations can change them. | `Application/Courses/Queries/ListCourseCatalog/ListCourseCatalog.cs:28-41`; `Infrastructure/Queries/CourseQueryService.cs:95-150` |
| F9 | The student view summary also embeds `SubjectName`, and is cached for 10 minutes under the per-course tag. Subject rename/delete evicts subjects + catalog, not the per-course family. | `CourseQueryService.cs:186-198`; `CachedCourseQueryService.cs:77-105`; `CourseCacheEvictionBehavior.cs:143-151` |
| F10 | Readiness depends on `CourseAsset.State` (`Pending`/`Available`/`Failed`) and metadata sync updates `VideoCourseAsset.Duration`, which feeds readiness, catalog duration and view duration. | `CourseQueryService.cs:289-309`; `SyncResourceMetadata.cs:93-122` |
| F11 | Integration coverage exists for catalog, view, overview, readiness, subjects, tenant isolation and dashboard exclusion, but not the full mutation matrix. | `CoursesCacheTests.cs:44-300` |
| F12 | The `SoftDeleteTests` DB-read workaround is deliberate and commented (it bypasses command-driven eviction intentionally). It is not a defect. | `tests/.../SoftDeleteTests.cs` diff |

---

## 3. Findings and solution options

Severity is reviewer judgement, not a CI result.

| ID | Severity | Axis | Summary |
|---|---|---|---|
| H1 | High | Spec + Correctness | Asset/metadata eviction is a no-op in background consumers; readiness and duration stay stale for the full TTL |
| H2 | High | Spec + Correctness | Subject rename/delete leaves `SubjectName` stale in the student view for 10 minutes |
| H3 | High (design) | Architecture | Direct `HybridCache` coupling and copy-pasted policy across modules; user wants a shared `ICachingService` |
| M1 | Medium | Spec + Correctness | Catalog counts/duration/plans are not evicted by section/item/plan/asset mutations |
| M2 | Medium | Spec + Correctness | Overview-upsert eviction is inert because the command is not `ICommand` |
| M3 | Medium | Spec (tests) | Freshness matrix is only partially covered |
| M4 | Medium | Spec (tests) | Tenant isolation is asserted on 3 of 6 cached surfaces; the student catalog is not tested |
| M5 | Medium | Design | Eviction behavior is a 20-case switch; every new command edits the same class |
| L1 | Low | Design | `Guid?` tenant + `NoTenant="none"` dead branch; inconsistent null-tenant guards |
| L2 | Low | Docs | `CACHING.md` wording and Redis claims are inaccurate; runbook row is stale |
| L3 | Low–Medium | Scope | Admin/staff listing `/courses` is cached while the issue says "admin listing" is out of scope |
| L4 | Low | Tests | Unused helper and a no-op assertion in `CoursesCacheTests` |
| P1 | High (platform) | Out of #33 | `AddHybridCache` options lambda does nothing |
| P2 | Medium (platform) | Out of #33 | No Redis/L2 registered, contrary to the issue premise |

---

### H1 — Asset/metadata eviction never reaches tenant entries

**Evidence**

- `CourseCacheEvictionBehavior.cs:153-158` handles `MarkCourseAssetAvailableCommand`,
  `MarkCourseAssetFailedCommand` and `SyncResourceMetadataCommand` by removing
  `CourseCacheKeys.CourseAssetsTag(tenantProvider.GetTenant())`.
- Those commands are dispatched from MassTransit consumers with no `HttpContext`
  (`VideoEventHandlers.cs:59-125`, `MetadataChangedConsumerBase.cs:38`).
- `HttpTenantProvider.GetTenant()` returns null outside an HTTP request (fact F4), so the tag
  becomes `courses:assets:none` (`CourseCacheKeys.cs:71,86`).
- Readiness entries are tagged with the real tenant, so `courses:assets:none` matches nothing.
- Readiness reads `CourseAsset.State` (`CourseQueryService.cs:289-309`), so after transcoding
  it can keep saying "still transcoding" for the full 3-minute TTL. Metadata sync also changes
  duration, which feeds the 2-minute catalog and 10-minute view.

**Why tests miss it**

`CoursesCacheTests` never dispatches an asset/metadata command through a consumer-like scope;
there is no HTTP context to lose, and the test host's tenant provider is static.

**Options**

1. **Tenant-agnostic eviction tag (smallest safe fix).**
   Give the asset family a single global tag, e.g. `courses:assets`, attach it to readiness
   (and, if freshness for duration matters, catalog/view), and evict it from asset commands.
   No tenant resolution is needed. Keys remain tenant-isolated; only invalidation is coarser
   across tenants. Cost: asset events evict other tenants' readiness entries, increasing
   misses. Acceptable if asset events are infrequent.
2. **Resolve tenant/course before dispatch.**
   Extend the asset commands with `TenantId` and the owning `CourseId` (one asset can be
   referenced by several resources, so resolve all owning courses), then evict the per-course
   and catalog families. Cleanest freshness, but requires either:
   - changing `VideoPublishedIntegrationEvent` / `VideoProcessingFailedEvent` /
     `VideoMetadataChangedIntegrationEvent` to carry `TenantId` (cross-module contract change
     and deploy-order risk), or
   - a trusted cross-tenant lookup by asset id (`IgnoreQueryFilters`) inside the Courses
     consumer before dispatch.
3. **Ambient tenant for consumers (platform fix).**
   Add a MassTransit consume filter that reads a tenant header into a scoped tenant context,
   and make `ITenantProvider` fall back to it. This fixes not only eviction but also fact F5
   (tenant-filtered DB reads in consumers). Larger change; belongs in a platform issue.
4. **Drop the promise.**
   Remove the asset-triggered eviction case and document that readiness/duration can be stale
   for their TTL. Least work, but contradicts story 3 and `CACHING.md:25`.

**Recommendation**

Resolved structurally by §4.7: the eviction policy reads `TenantId` from the changed
`CourseAsset` entity, so the tenant is correct with or without an HTTP context and no
cross-module event change is needed. If the strategy design is not adopted, fall back to option 1
(global asset tag) as the immediate fix plus a consumer-scope test. Do not ship option 4
without changing the spec.

**Acceptance**

- In a scope with no HTTP context, an asset-available command makes a previously cold readiness
  entry return `Available` on the next read (not after the TTL).
- A metadata-sync duration change is visible in catalog/view after the next read.
- The tenant isolation tests still pass (no key crosses tenants).

**Open questions**

- How often do asset/metadata events fire per tenant? If they are rare, over-invalidation is cheap.
- Are asset events delivered on the external SQS bus (no HTTP context) in every deployment?
- Can the integration events accept a `TenantId` field without breaking in-flight messages?

---

### H2 — Subject rename/delete leaves the student view stale for 10 minutes

**Evidence**

- `GetCourseSummaryForViewAsync` projects `Subject.Name` (`CourseQueryService.cs:198`).
- `CachedCourseQueryService.cs:77-91` caches that summary under `CourseTag(tenant, courseId)`
  with a 10-minute TTL.
- `UpdateSubjectCommand` / `DeleteSubjectCommand` evict `SubjectsTag` and `CatalogTag` only
  (`CourseCacheEvictionBehavior.cs:143-151`).
- The implementation already treats `SubjectName` as a catalog dependency (catalog is evicted),
  so the view omission is inconsistent.

**Why tests miss it**

There is no subject-rename/delete freshness test for the view; the subject test only checks
the subject listing.

**Options**

1. **Tenant-wide view family tag.** Attach `courses:view:{tenant}` to public/student view
   entries; subject update/delete evicts that tag. Simple, correct, coarser: every subject
   change drops all cached views in the tenant. Subject changes are rare, so the cost is low.
2. **Per-subject tag.** Requires knowing the subject id at fill time (the view key only has
   course id). You could look up the course's subject in the command and evict each affected
   course tag, or add a subject→courses lookup. More precise, more code.
3. **Accept and document.** Add the 10-minute tolerance to `CACHING.md`. Conflicts with story 3
   ("edits ... reflected ... within a bounded, short window") for a visible field.

**Recommendation**

Option 1, implemented as the `Subject` policy in §4.7 (`CourseViewsTag(tenant)` on public/student
view entries). It matches the "whole-family eviction" model, needs no extra query, and subject
mutations are rare. Document that subject changes invalidate the tenant's views.

**Acceptance**

- Rename a subject, then read the cached student view inside the TTL: `SubjectName` is updated.
- The same for delete (the view should reflect the new empty/updated value, not the old name).
- Cross-tenant isolation still holds.

**Open questions**

- Should the staff tree view (`CourseDto`) also carry the tag? It currently exposes `SubjectId`
  but not `SubjectName`, so not strictly needed today. If the shape gains the name, include it.

---

### H3 — Direct `HybridCache` coupling and duplicated policy

This is the architectural request behind the review: use an `ICachingService` in all modules,
not just Courses.

**Current state**

- `HybridCache` is injected directly in `CachedCourseQueryService`, `CachedSubjectQueryService`,
  `CourseCacheEvictionBehavior`, Identity's `PublicKeyProvider`/`ConditionRepository`, Tenants'
  `TenantBrandingUpdatedConsumer`, VideoUploading's `DefaultVideoEncryptionService`.
- Each site repeats tenant guard, key string, TTL/options and tags.
- `CachedCourseQueryService` has seven near-identical `GetOrCreateAsync` bodies.
- The eviction behavior is a 20-case switch with ~15 identical bodies.

**Options**

- **A. Shared `ICachingService` + typed policy (recommended if the platform direction is "all
  modules").** Modules stop referencing `Microsoft.Extensions.Caching.Hybrid`. Policy (tenant
  scoping, TTL, tags, stampede-safe loader) lives in one place. See §4 for a design sketch; the
  write side of the same seam is the persistence-side strategy in §4.7.
- **B. Shared `CachePolicy`/key helpers, keep direct `HybridCache`.** Smaller change; removes
  string/TTL drift but leaves third-party coupling. Closest to the current spec.
- **C. Per-module helper only.** Fixes Courses duplication, not the cross-module request.

**Recommendation**

Adopt A eventually, but **not inside #33** unless the spec is amended first. Issue #33's
Implementation Decisions explicitly say "No bespoke cache service, no manual dictionary
caches, no second invalidation framework." Landing #33 and extracting the service as a
follow-up avoids invalidating four commits and the cache test suite for a non-behavioural gain.

**Acceptance for A**

- Courses contains no `using Microsoft.Extensions.Caching.Hybrid`.
- The shared service is the only place that constructs `HybridCacheEntryOptions`.
- Cache behaviour tests are unchanged except for the DI registration.
- Sibling modules still pass their tests.

**Open questions**

- Is the abstraction worth it if only Courses ever caches? The user says all modules will.
- Should the service expose `HybridCache`-specific flags (compression, local TTL)? Avoid until
  a real need appears; otherwise it becomes a leaky mirror of HybridCache.
- Who owns the spec amendment: a new issue, or an edit to #33's Implementation Decisions?

---

### M1 — Student catalog volatile fields are not evicted by the right commands

**Evidence**

- The student catalog DTO includes `SectionCount`, `ItemCount`, `TotalDurationSeconds`,
  `Plans`, `CoverImageDocumentId`, timestamps and `SubjectName`
  (`ListCourseCatalog.cs:28-41`; projection at `CourseQueryService.cs:95-150`).
- Section/item/plan mutations evict `CourseTag` only (`CourseCacheEvictionBehavior.cs:76-113`).
- `CatalogTag` is evicted by course create/update/delete, state transitions and subject
  mutations, but not by the commands above.
- Result: counts/duration/plans can be wrong for the 2-minute catalog TTL. Metadata sync
  (duration) is an additional trigger, subject to H1.

**Why tests miss it**

The catalog freshness test covers item-create (which evicts only `CourseTag`), so it would not
catch a change that only affects the student catalog's volatile fields.

**Options**

1. **Evict `CatalogTag` from every command that changes a projected field.** Add it to
   section add/delete, item create/delete, plan add/remove, asset assign/unassign and metadata
   sync (and overview upsert once M2 is fixed). Introduce a contract test that every command
   changing a catalog field has the catalog scope.
2. **Narrow the cached DTO.** Remove volatile fields from the catalog (not viable: #32's
   response shape is a contract).
3. **Shorten the catalog TTL.** Reduces the window but does not fix the missing wiring; also
   increases DB load.

**Recommendation**

Option 1. §4.7 gets this for free: every child entity policy queues `CatalogTag` because those
fields are projected in the catalog, and the `CourseOverview` policy does too (cover image and
timestamps). The mapping contract test prevents recurrence.

**Acceptance**

- Add a section/item/plan to a published course; the next catalog read (within the TTL) shows
  the new counts/plans.
- Metadata sync changing duration is visible within the TTL (depends on H1).
- The contract test fails if a new catalog-affecting command is added without the eviction.

**Open questions**

- Is `Plans` always active plans? If `AddPlanCommand` can add an inactive plan, adjust the test.
- Do we want `CourseTag` + `CatalogTag` double eviction for section/item mutations, or can the
  catalog derive counts from a cheaper projection?

---

### M2 — Overview-upsert eviction is inert

**Evidence**

- `UpsertCourseOverviewCommand` is `IRequest` (`UpsertCourseOverview.cs:14`), so:
  - the eviction behavior's `request is not ICommand` guard skips it (`CourseCacheEvictionBehavior.cs:51`);
  - the UnitOfWork behavior never saves it (pre-existing bug: overview edits are silently lost).
- `CACHING.md:64-68` documents the gap instead of fixing it.
- The overview test asserts ETag cache-serve and course-family eviction via other commands,
  not the upsert trigger.

**Why tests miss it**

The integration test cannot observe a save that never happens.

**Options**

1. **Fix the command to `ICommand<Success>` in a dedicated issue.** One-line type change plus
   tests; fixes a real data-loss bug. But it is a write-path behavior change, which #33's
   Out of Scope excludes.
2. **Keep #33's documented gap and remove the dead eviction case** until the command is fixed.
   Honest, but the spec's overview freshness row remains unmet.
3. **Leave as-is.** The dead case activates automatically when someone changes the command;
   zero value until then, and the doc explains it.

**Recommendation**

Option 1 as a separate small issue (likely under #29/#30 ownership). Under §4.7 the
`CourseOverview` policy already exists and activates as soon as the command becomes an
`ICommand` (the UoW then saves it); no dead switch case remains. Do not silently expand #33's
scope.

**Acceptance**

- Upserting an overview persists the content (verify via GET and DB).
- The next cached overview read returns the new content/ETag inside the TTL.
- The eviction case is exercised by a test.

**Open questions**

- Does any caller depend on the current no-save behavior (e.g., dry-run upsert)? Unlikely but verify.
- Should the overview command be part of #29's CRUD slice retroactively?

---

### M3 — Freshness matrix is only partially covered

**Evidence**

`CoursesCacheTests.cs:44-238` covers:

- catalog create + item-create + plan-add
- view item-create
- overview course-family eviction
- readiness plan-add
- subject create

Not covered: course rename/delete/archive/publish, section rename/delete, item update/delete,
reorder sections/items, asset assign/unassign/dismiss, subject rename/delete, overview upsert,
asset-available/failed, metadata sync.

**Options**

1. **Table-driven integration tests.** One test per cached surface that runs the documented
   triggers and asserts the response changes inside the TTL. Use the existing HTTP helpers.
2. **Only test the risky paths.** Add the missing mutators that are cheap to set up; skip
   combinations that share the same eviction case.
3. **Rely on the contract test from M1/M5** for mapping and keep a smaller behavior suite.

**Recommendation**

Option 1 for the surfaces with user-visible fields, plus the M1/M5 contract tests to prevent
mapping drift. Behavior-only assertions, no cache-internals checks.

**Acceptance**

- Every row of `CACHING.md`'s trigger table has at least one behavioral test.
- Tests fail if the corresponding eviction case is removed.

**Open questions**

- Which mutators are prohibitively expensive to set up in the integration host?
- Do we need a test-only clock to exercise TTL expiry, or is eviction-only coverage enough?

---

### M4 — Tenant isolation coverage is incomplete

**Evidence**

- `CoursesCacheTests.cs:242-288` exercises `/courses` (permission-gated listing), overview and
  subjects.
- `/courses/catalog` (the anonymous student catalog), `/courses/{id}/view` and
  `/courses/{id}/readiness` are not cross-tenant probed.
- The test name says "CacheSurfaces" but the student catalog is the main #32 consumer.

**Options**

1. **Extend the existing test** with the student catalog, view and readiness (same tenant A/B
   pattern, same key shape minus tenant).
2. **Add a parameterized test** over surface readers.
3. **Rely on the key builder's tenant segment.** Not enough: the spec says behaviorally assert it.

**Recommendation**

Option 1. It is cheap and directly closes the spec line.

**Acceptance**

- Tenant B requesting the same course id/filter shape as tenant A gets 404/empty for catalog,
  view and readiness.
- Tenant A's entries still resolve after B's probe.

**Open questions**

- Should the anonymous catalog/view be tested with `X-TenantId` only (no claim) to mirror
  production traffic? Yes, if the host supports it.

---

### M5 — Eviction mapping is a growing switch

**Evidence**

`CourseCacheEvictionBehavior.cs:58-159` has ~20 cases; ~15 bodies are
`await EvictCourseAsync(x.CourseId, ...)`. Every new command edits this class (Divergent
Change / Repeated Switches). The missing catalog evictions (M1) and the asset tenant bug (H1)
are symptoms of the same central mapping.

**Constraint (decided)**

Command handlers and the Application layer must stay cache-agnostic. No command may implement a
cache marker, carry an eviction scope, or reference `IEvictionProvider`/`ICachingService`. The
`Evict(tag)` call therefore cannot originate in a command, a handler, or a behavior that
switches on request types.

**Why a repository decorator alone is not enough**

A decorator over `Update/Add/Remove` is the natural place to queue tags and was the first
proposal. Verified against the Courses handlers, it misses mutations that rely only on EF
tracking:

- `ReorderCommandsHandlers` (`ReorderCommands.cs:28-51`) mutates section/item order and never
  calls `Update`.
- `UpdatePlanCommandHandler` / `RemovePlanCommandHandler` (`UpdatePlan.cs:34-47`,
  `RemovePlan.cs:32-45`) mutate tracked plans and never call `Update`.
- `CompleteItemCommandHandler` and `IssuePlaybackTicket` mutate tracked state the same way
  (their surfaces are uncached today, but the pattern will recur).
- A repository decorator also cannot see one save touching several aggregates (an asset and
  its course), nor changes produced by domain-event handlers during `SaveChanges`.

The curriculum/section/item/plan mutation handlers all load the tracked `Course` root; asset
handlers mutate `CourseAsset` directly, which carries both `TenantId` and `CourseId`. In both
shapes the changes are visible to EF; the only seam that sees every persisted change, including
children of a tracked aggregate, is the UnitOfWork's `ChangeTracker` immediately before
`SaveChangesAsync`.

**Options**

1. **Persistence-layer strategy + entity policies + outermost flush (recommended).**
   `UnitOfWork<TContext>` gains one injected `IEvictionStrategy` and one call
   (`CollectAsync(context)`); the strategy translates changed entity entries into tags and
   queues them on `IEvictionProvider`. A mapping-free outermost behavior flushes the queue
   after the transaction commits. No Application code knows caching exists, and the UoW stays a
   thin orchestrator. Full design in §4.7.
2. **Repository decorator queueing tags.** Keeps the queue next to writes, but misses the
   tracking-only handlers above; fixing that requires explicit `Update` calls in every handler
   (an Application-layer change) or a second strategy anyway. Acceptable only as an
   optimization on top of option 1, never as the mechanism.
3. **Marker interfaces on commands + contract test.** Local and discoverable, but it puts
   caching semantics on commands; rejected by the constraint.
4. **Handlers return an eviction plan / emit a domain event.** A second invalidation mechanism,
   which #33's spec forbids, and still Application-layer awareness.
5. **Keep the switch and extract helpers.** Reduces lines, not the coupling.

**Recommendation**

Option 1. It is the only option that both (a) sees tracking-only mutations and (b) keeps the
Application layer cache-agnostic. The current `CourseCacheEvictionBehavior` is deleted, not
extended. The entity policies also fix H1 (tenant from `CourseAsset`), H2 (subject view tag) and
M1 (catalog tag for child entities) in the same change.

**Acceptance**

- No type in `Modules/Courses/Application` declares, constructs or calls `ICachingService`,
  `IEvictionProvider`, `CacheTag` or `Microsoft.Extensions.Caching.Hybrid`; a NetArchTest/
  Mono.Cecil or source-scan architecture test enforces it.
- Reorder and plan-update commands evict the course and catalog families without calling
  `Update` (regression tests).
- Every entity type in the Courses model that feeds a cached projection has a policy or is in an
  explicit allowlist; an unmapped type fails the contract test.
- Existing behavior tests pass with the flush behavior in place.

**Open questions**

- Does any module write through a `DbContext` without `IUnitOfWork`? Such a write needs its own
  strategy invocation.
- Should strategy execution be limited to commands? Rule: any save that can change a cached
  projection must go through the instrumented UoW.

---

### L1 — Key primitives and null-tenant guards

**Evidence**

- `CourseCacheKeys` takes `Guid? tenantId` everywhere and has a `NoTenant = "none"` fallback
  (`CourseCacheKeys.cs:28,86`). No-tenant reads bypass the cache, so the fallback is dead.
- Inline eviction cases call `tenantProvider.GetTenant()` without a null check
  (`CourseCacheEvictionBehavior.cs:62,150,157`), while helpers do check (`:170-195`).
- Keys and tags are raw strings; typos are only caught by behavior tests.

**Options**

1. **Typed `CacheKey`/`CacheTag` + non-null tenant.** If H3 is adopted, use record structs and a
   tenant-scoped key builder. If not, at least make builders take `Guid` and let the decorator
   pass through when null.
2. **Keep strings, remove the dead fallback and centralize the guard** in a small
   `CacheScope`/`TryGetTenant` helper.
3. **Leave as-is.** Low risk, but the dead branch invites future misuse.

**Recommendation**

Option 2 now; option 1 if H3 is approved. Never let a null tenant reach a key builder.

**Acceptance**

- No `NoTenant` sentinel remains.
- Every cached method has one tenant guard at the top; helpers take a non-null `Guid`.
- Key/tag construction for a tenant is impossible without a tenant id (by type, ideally).

---

### L2 — Documentation accuracy

**Evidence**

- `CACHING.md:11` says "Every key and every eviction tag **starts with** the owning tenant id";
  keys start with `courses:...`.
- `CACHING.md:4` says "in-process L1 (+ Redis L2 where configured)", but no distributed cache
  is registered (fact F2). Hedged, but misleading next to issue #33's "Redis + in-memory".
- `CACHING.md:25` promises asset-triggered readiness eviction that does not fire (H1).
- `CACHING.md:64-68` documents the overview gap (M2) but the behavior still contains the dead case.
- `docs/agents/per-issue-runbook.md` §3 row #33 says "catalog / view / subject-list", while the
  locked issue body explicitly includes readiness and the plan comment adds overview.

**Recommendation**

Update all five after the fixes land. Use wording like "every key/tag includes the tenant id as
its scope component". State "L1-only until a distributed cache is configured". Keep known gaps
only while they are real.

---

### L3 — Admin/staff listing caching scope

**Evidence**

- `CachedCourseQueryService.cs:36-48` caches `ListCoursesAsync`, which backs `GET /courses`
  (`ListCourses.cs:28,43`, permission `courses:View`).
- Issue #33 Out of Scope: "Caching the admin listing, pool, analytics, or minor admin queries."
- The implementer's plan comment explicitly listed `GET /courses` as the "Catalog list", and
  `CACHING.md:20` calls it that.

**Options**

1. **Declare it in scope** as the staff catalog list and update the issue wording.
2. **Remove caching from `ListCoursesAsync`** if it is truly an admin listing.
3. **Keep it and document the exception** with a rationale (same catalog family, permission-gated).

**Recommendation**

Option 1 or 3 after confirming the product intent. Do not leave the issue and code disagreeing.

**Open questions**

- Is `/courses` used by the admin UI only, or by instructors/students too?
- Does the `courses:View` permission include student roles?

---

### L4 — Test dead code

- `CoursesCacheTests.cs:325` defines an unused `c(string)` helper.
- `CoursesCacheTests.cs:286` re-asserts `catalogB` (already asserted), so it proves nothing new.

Remove both while touching the test file. No behavioral impact.

---

### P1 — `AddHybridCache` options lambda is a no-op (platform)

**Evidence**

```csharp
services.AddHybridCache(o =>
{
    o = new HybridCacheOptions() { DefaultEntryOptions = new() { ... } };
});
```

The assignment replaces the local parameter; the configured defaults are discarded.

**Impact**

- Any entry without explicit options uses HybridCache's built-in defaults, not the intended
  10-minute/5-minute values.
- Courses sets per-surface options, so it is masked; future modules may be surprised.
- Identity's 24-hour entries set options explicitly; still fine.

**Recommendation**

Fix the mutation (`o.DefaultEntryOptions = ...`), add a test/assertion if the options are
resolved, and do it in a shared/platform PR (not #33). Verify HybridCache's actual default
expiration before relying on "no options means default 10m".

---

### P2 — No distributed cache registered (platform)

**Evidence**

`AddHybridCache` without `AddStackExchangeRedisCache`; no Redis registration in `src/`. The
issue's premise says "Redis + in-memory" and the plan comment says "Redis L2 where configured".

**Options**

1. Wire Redis (infrastructure + configuration + deploy work) — out of #33.
2. Document L1-only for this release and update the issue/docs.
3. Defer until multi-instance deployment is real.

**Recommendation**

Option 2 for #33, with a platform issue for Redis. The cache behavior tests are valid on L1;
do not add a Redis container until L2 exists.

---

## 4. Architecture decision: `ICachingService`

### 4.1 Principle

A shared service is worth it only if it owns policy. A 1:1 wrapper over `HybridCache` would be
a Middle Man and would not remove the duplication that caused H1/M1/H2. The seam should own:

1. Tenant-scoped key and tag construction (impossible to build a key without a tenant).
2. TTL + tag policy (`CachePolicy`), so callers never see `HybridCacheEntryOptions`.
3. Stampede-safe read-through by delegating to `HybridCache.GetOrCreateAsync` (do not
   reimplement single-flight).
4. The only reference to `Microsoft.Extensions.Caching.Hybrid` outside Shared.

### 4.2 Sketch

```csharp
namespace AlphaZero.Shared.Infrastructure.Caching;

public readonly record struct CacheKey(string Value);
public readonly record struct CacheTag(string Value);

public sealed record CachePolicy(TimeSpan TimeToLive, IReadOnlyList<CacheTag> Tags)
{
    public CachePolicy(TimeSpan ttl, params CacheTag[] tags) : this(ttl, (IReadOnlyList<CacheTag>)tags) { }
}

public interface ICachingService
{
    Task<T> GetOrCreateAsync<T>(
        CacheKey key,
        Func<CancellationToken, ValueTask<T>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
    Task RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken = default);
}

public sealed class HybridCachingService(HybridCache cache) : ICachingService
{
    public Task<T> GetOrCreateAsync<T>(
        CacheKey key,
        Func<CancellationToken, ValueTask<T>> factory,
        CachePolicy policy,
        CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(
            key.Value,
            factory,
            new HybridCacheEntryOptions { Expiration = policy.TimeToLive },
            policy.Tags.Select(t => t.Value).ToArray(),
            cancellationToken).AsTask();

    public Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(key.Value, cancellationToken).AsTask();

    public Task RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken = default)
        => cache.RemoveByTagAsync(tag.Value, cancellationToken).AsTask();
}
```

A tenant-scoped key builder (either a `CacheScope` value or an injected `ICacheKeyFactory`):

```csharp
public readonly record struct CacheScope(Guid TenantId)
{
    // family is module-owned, e.g. "courses:catalog" or "identity:assignments"
    public CacheKey Key(string family, params (string Name, string Value)[] parts) =>
        new($"{family}:{TenantId}" + string.Concat(parts.Select(p => $":{p.Name}={p.Value}")));

    public CacheTag Tag(string family, params (string Name, string Value)[] parts) =>
        new($"{family}:{TenantId}" + string.Concat(parts.Select(p => $":{p.Name}={p.Value}")));
}
```

Notes:

- Use a module prefix (`courses`, `identity`, ...) so families never collide.
- Keep the policy minimal; add `LocalCacheExpiration`/flags only when a real need appears.
- No-tenant flows pass through at the decorator (`if (tenant is null) return inner...`); the
  service never invents a tenant and the `NoTenant` sentinel disappears.
- A fake `ICachingService` can be used in unit tests; behavior/integration tests keep using the
  real `HybridCache` through the same DI.

### 4.3 Migration order

1. Add the abstraction + implementation in `AlphaZero.Shared`; register it in shared DI.
2. Move Courses to it: two decorators + eviction behavior + key builders.
3. Identity: `PublicKeyProvider`, `ConditionRepository`.
4. Tenants: `TenantBrandingUpdatedConsumer`.
5. VideoUploading: `DefaultVideoEncryptionService`.
6. `CachingRepository` (evaluate whether it should delegate or remain a repository decorator).
7. Remove the `Microsoft.Extensions.Caching.Hybrid` package reference from module projects;
   keep it in Shared.

Do Courses first (it exercises tags, TTLs and eviction), then the simpler single-key consumers.

### 4.4 Spec conflict

Issue #33 says: "No bespoke cache service, no manual dictionary caches, no second invalidation
framework" and "Only shared infrastructure". An `ICachingService` is arguably a facade over the
shared infrastructure, not a second framework, but a reviewer can read it as bespoke. Before
implementing, amend either:

- issue #33's Implementation Decisions (if the service is part of #33), or
- create a follow-up issue (recommended) that cites this document and updates
  `docs/CachingArchitecture.md`.

Suggested amendment text: "All modules read/write the shared HybridCache through the
`AlphaZero.Shared` `ICachingService` seam; module code must not depend on
`Microsoft.Extensions.Caching.Hybrid` directly. The seam owns tenant-scoped keys, TTL/tag
policy and delegation to HybridCache's stampede protection. It is not a second invalidation
framework: eviction remains tag-based and in-process."

### 4.5 Testing strategy

- Behavior tests: unchanged (HTTP/integration assertions) — the service is an implementation
  detail behind DI.
- Unit tests: fake `ICachingService` to assert a decorator calls `GetOrCreateAsync` with the
  expected key/tags/TTL, and the eviction behavior calls `RemoveByTagAsync`.
- Contract tests: key builder never accepts a null tenant; every cached surface has a policy;
  every command maps to an eviction scope (M5).
- Do not assert raw key strings in behavior tests (spec's Testing Decisions).

### 4.6 Open questions for the other chats

1. Does `ICachingService` belong in #33 or in a follow-up? (Recommendation: follow-up.)
2. Should the service expose `HybridCache` options or only TTL + tags? (Recommendation: only
   TTL + tags initially.)
3. Should `ICachingService` also replace Identity's `IMemoryCache` usage, or only `HybridCache`?
   (Recommendation: define the target first; Identity's zero-DTO entity caching has different
   semantics.)
4. Who owns `CacheScope`/key conventions per module — Shared or each module? (Recommendation:
   Shared owns the type and tenant rule; modules own their family names.)
5. Is a fake service enough, or do we need a real in-memory integration host for the seam?
   (Recommendation: both, as in §4.5.)

### 4.7 Eviction ownership: persistence-layer strategy

**Constraint**

The Application layer (commands, handlers, validators) must be agnostic of caching. It must not
reference `ICachingService`, `IEvictionProvider`, `CacheTag`, TTLs or `HybridCache`, and no
command may implement a cache marker interface. Cache awareness is confined to infrastructure.

**Call graph**

```text
command handler (cache-agnostic)
        │  mutates tracked entities
        ▼
UnitOfWork.SaveChangesAsync              (AlphaZero.Shared/Infrastructure)
        │  strategy.CollectAsync(context)   ← UoW knows only IEvictionStrategy
        ▼
ChangeTrackerEvictionStrategy            (shared, mapping-free scan)
        │  per changed entry → policy.GetTagsAsync(...)   ← entity-level strategies
        ▼
IEvictionProvider.Evict(tag)             ← only Evict call site
        │
DbContext.SaveChangesAsync
        │
TransactionalBehavior.CommitAsync        (only for ITransactionalCommand)
        │
CacheEvictionFlushBehavior (outermost) → provider.FlushAsync()  ← only after commit
```

`FlushAsync` is deliberately outside the UoW: the UoW is inner to `TransactionalBehavior`, so
flushing there could invalidate before the transaction commits. The flush behavior is registered
as the first `AddOpenBehavior` (outermost), so its `next()` returns only after the commit — or
after the implicit save transaction for non-transactional commands.

**Components (Strategy Pattern, two scopes)**

```csharp
// Top-level Strategy: the only eviction dependency the UoW knows.
// Side-effect-free: read the ChangeTracker and queue tags; never save or publish.
public interface IEvictionStrategy
{
    ValueTask CollectAsync(DbContext db, CancellationToken cancellationToken);
}

// Entity-level Strategy: module-owned, one per entity type that feeds a cached projection.
public interface IEntityEvictionPolicy
{
    Type EntityType { get; }
    ValueTask<IReadOnlyList<CacheTag>> GetTagsAsync(
        EntityEntry entry, DbContext db, CancellationToken cancellationToken);
}

// Shared implementation: scans Added/Modified/Deleted entries and dispatches by mapped CLR
// type. Mapping-free; all knowledge lives in the policies.
public sealed class ChangeTrackerEvictionStrategy(
    IEnumerable<IEntityEvictionPolicy> policies,
    IEvictionProvider provider) : IEvictionStrategy;

// Optional typed base for policies (avoids casts in module code).
public abstract class EntityEvictionPolicy<TEntity> : IEntityEvictionPolicy
    where TEntity : class;

// Queue + flush, unchanged from the earlier sketch.
public interface IEvictionProvider
{
    void Evict(CacheTag tag);
    Task FlushAsync(CancellationToken cancellationToken);
    void Discard();
}
```

The entire UoW change is two lines:

```csharp
public class UnitOfWork<TContext>(
    TContext context, IPublisher publisher, IEvictionStrategy eviction) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        // existing domain-event loop …
        await eviction.CollectAsync(_context, cancellationToken);
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
```

- `ChangeTrackerEvictionStrategy` builds a `Dictionary<Type, IEntityEvictionPolicy>` once per
  scope and dispatches on `entry.Metadata.ClrType` (not `entry.Entity.GetType()`, which returns
  a proxy type under lazy-loading proxies). One policy per entity type; a policy that needs
  several contributions returns them in one list. Materialize `Entries()` to a list before
  dispatch, because a policy may query the context and attach new entities.
- Policies may query the context to resolve a parent; those queries use `IgnoreQueryFilters()`
  and `AsNoTracking()`.
- `BaseRepository.Update` marks the whole loaded graph `Modified`, so some unchanged children
  may queue the same family tags. `HashSet<CacheTag>` dedupe makes this harmless, and
  over-eviction is always safe: correctness only depends on never missing a tag.
- `ScopedEvictionProvider` holds a `HashSet<CacheTag>` (dedupe) and delegates removal to
  `ICachingService.RemoveByTagAsync` on flush (or `HybridCache` directly while D1 = follow-up).
  It is scoped: one HTTP request or one bus message per queue.
- `CacheEvictionFlushBehavior<TRequest,TResponse>` contains **no command or entity mapping**: on
  success it flushes, on an `ErrorOr` error it discards, and on an exception it lets the
  exception propagate without flushing (the scope dies). A scoped depth counter makes only the
  outermost command flush, so a nested `Send` from a domain-event handler cannot flush before the
  outer commit.
- A flush failure is logged and swallowed. The command already committed; entries then expire by
  TTL. Caching must never turn a successful write into a failed request.
- Registration: `IEvictionStrategy` (`ChangeTrackerEvictionStrategy`) and `IEvictionProvider`
  are scoped in Shared; each module registers its policies
  (`AddScoped<IEntityEvictionPolicy, CourseEvictionPolicy>()`, …). A module with no policies
  resolves an empty set and the strategy is a no-op; no module-specific UoW subclass is needed.

**Strategy scope: changes, not commands**

If `IEvictionMethod` means "one strategy per command", it recreates the marker-interface problem
and puts caching semantics back into the Application layer. The strategy must answer **"what did
this save change?"**, not "which command ran?" — entity-level policies can do that without the
command ever knowing a cache exists. The top-level strategy is deliberately a one-method interface
so the UoW stays a thin orchestrator and the scan can be swapped (e.g. a future outbox-based
implementation) without touching persistence code. A module that needs a different scan registers
its own `IEvictionStrategy`; the UoW contract is unchanged.

**Why tags, not keys**

The write side does not know which read variants are cached, and the cached projections are
built from several tables. Tag invalidation is the existing model and the only one that can
express "every entry derived from this course". An `AddKey` API would be partial and fragile;
`Evict(CacheTag)` is the minimum correct contract. The user-proposed `AddKey`/`Delete` shape is
preserved in spirit: queue during the unit of work, delete after success.

**Why not `IMediator.Send` or a raw delegate**

- **`IMediator.Send(GetEvictionTags(...))` from the UoW.** Rejected. It puts a dispatch on the
  save hot path, creates re-entrancy (`UoW → mediator → handler → UoW`) and the risk of nested
  saves, and hides the resolution behind a pipeline. If the request/handler lives in Application
  it also violates the agnosticism constraint; if it lives in Infrastructure it is a delegate
  with extra ceremony. There is no module boundary to cross inside a single `DbContext`.
- **A delegate** (`Func<DbContext, CancellationToken, ValueTask<IReadOnlyList<CacheTag>>>`
  registered per module). Viable and the smallest possible seam, but it cannot be composed per
  entity, is harder to discover and mock, and every module reimplements the scan. Use it only if
  the team wants zero new interfaces; the one-method `IEvictionStrategy` is effectively a named,
  testable delegate.
- **One `IEvictionMethod` per command.** Rejected for the same reason as marker interfaces: the
  command becomes the cache contract again. The right scope is the change set, with entity-level
  policies underneath.

**Tenant resolution**

The tenant comes from the changed entity graph, never from `ITenantProvider`:

- `Course`, `Subject`, `CourseAsset` and `CourseOverview` expose `TenantId` directly
  (`TenantOwnedAggregate`/`TenantOwnedEntity`).
- Child entities (`CourseSection`, `CoursePlan`, `CurriculumItem`, `CurriculumResource`) carry
  `CourseId` or a FK chain to it. The policy resolves the course with `IgnoreQueryFilters()` when
  the root is not already tracked. The curriculum/section/item/plan handlers do load the root
  (verified), so the common path is a dictionary lookup over the tracked `Course` entries; asset
  policies use the asset's own `TenantId`/`CourseId` and never need the root.
- This fixes H1 at the root: a consumer-dispatched asset command produces the asset's real
  tenant tag with no `HttpContext` and no cross-module event change.

**Courses policy table**

| Changed entity (Added/Modified/Deleted) | Tags queued |
|---|---|
| `Course` | `CourseTag(tenant, courseId)`, `CatalogTag(tenant)` |
| `CourseSection`, `CoursePlan` | `CourseTag`, `CatalogTag` |
| `CurriculumItem`, `CurriculumResource` | `CourseTag`, `CatalogTag` (via item → section → course) |
| `CourseOverview` | `CourseTag`, `CatalogTag` (cover image and timestamps are catalog fields) |
| `CourseAsset` | `AssetsTag(tenant)`, `CourseTag`, `CatalogTag` (state/duration feed readiness, view, catalog) |
| `Subject` | `SubjectsTag(tenant)`, `CatalogTag(tenant)`, `CourseViewsTag(tenant)` (H2) |
| `Enrollment` | none — allowlisted; enrollment reads/dashboard are uncached |

`CourseViewsTag` is a new tenant-wide tag carried by the public/student view entries only. The
staff tree view does not project `SubjectName`, so it does not need it; it still carries
`CourseTag`.

**Contract tests**

- Architecture test: no `Courses.Application` type declares, constructs or calls a caching type
  (NetArchTest/Mono.Cecil member-reference scan, or a source scan for the forbidden namespaces;
  an assembly-reference check alone cannot prove this because the whole Shared assembly is
  referenced).
- Mapping test: every entity type in the Courses model that can feed a cached projection is
  either handled by a policy or listed in an explicit allowlist with a reason. A new entity fails
  the test until someone classifies it.
- Behavioral tests (no raw key assertions): mutate through each policy family, then read through
  the cached endpoint in the same tenant and assert the new value. Reorder and plan commands are
  the regression cases for tracking-only mutations.
- Consumer test: dispatch the asset-available command in a scope without an HTTP request and
  assert the tenant's readiness is refreshed (H1).

**Failure modes**

- Handler returns an `ErrorOr` error / validator rejects → no save, no tags, `Discard()`.
- `SaveChanges` throws → no flush; the scope dies.
- Commit throws after save → the outer behavior never flushes; the cache still holds the old,
  committed state, which is correct.
- Flush fails (cache/Redis down) → log; entries expire by TTL; the command stays successful.
- Process dies after commit but before flush → entries stay stale until TTL. Accepted: the
  eviction contract is at-most-TTL, never stronger.
- Residual read-through race (pre-existing, accepted): a read whose DB snapshot predates the
  commit can refill the cache after the flush. Bounded by TTL. Fixing it requires versioned keys
  or outbox-driven invalidation, which is out of scope.

### 4.8 Catalog cache cardinality (read-side policy)

**Problem**

`CachedCourseQueryService` keys `ListCoursesAsync`/`ListCourseCatalogAsync` on every
`search`, `subjectId`, `sort`, `page`, `perPage` combination, and `CachedSubjectQueryService`
keys the subject listing on `page/perPage`. `search` and the page parameters are
caller-controlled and effectively unbounded: a crawler (or an attacker) can mint keys faster
than their 2-minute TTL expires. `HybridCache` has no entry-count or size budget configured
here, so this is both memory growth and cache-pollution/DoS. It also rarely helps: deep pages
and arbitrary search terms have low repeat rates.

**Options**

1. **Default-page-only (recommended).** Cache an endpoint result only when all parameters are at
   the server defaults: `page == 1`, `perPage == 10`, no `search`, default sort (`newest`), and
   no subject filter. The key collapses to one per tenant (`courses:catalog:{tenantId}` and
   `courses:subjects:{tenantId}`). Every other request bypasses the cache and reads the DB.
   Search/sort/page are no longer key dimensions. Optionally add one key per subject
   (`...:subject={id}`) — bounded by the tenant's subject count, not by callers.
2. **Per-tenant snapshot.** Cache all published catalog rows per tenant (optionally per subject),
   then apply search/sort/page in memory. One key per tenant, no pollution, but moves filtering
   into application code, grows with the tenant, and duplicates SQL semantics (ILIKE, ordering,
   total count). Worth it only if the default page is not the dominant traffic.
3. **Cache all variants with a bounded size.** HybridCache exposes no per-entry size/count
   budget here; implementing an LRU/quota is a second cache framework. Rejected.
4. **Do not cache the catalog.** Simplest; loses the main read win. Keep as the conservative
   fallback if option 1's hit rate is low.

**Recommendation**

Option 1, applied identically to the staff list (`ListCoursesAsync`), the student catalog
(`ListCourseCatalogAsync`) and the subject listing (`ListSubjectsAsync`): only the parameterless
default page is cached. Do not put `search`/`sort`/`page`/`perPage` in a key, ever. If search
later needs to be fast, use Postgres trigram/FTS rather than caching search results.

**Acceptance**

- `GET /courses/catalog?search=x`, `page=2`, `perPage=50` and non-default sort bypass the cache
  (unit test with a fake `ICachingService`, or an integration seam that counts fills).
- The default first page is still cached and is evicted by `CatalogTag`.
- Key-builder contract test: the catalog/subject key builders accept no search/page/perPage/sort
  inputs.
- `CACHING.md` key families and the surface table are updated.

**Spec note**

#33's surface table promises a cached catalog but does not require every query variant to be a
hit. Default-page-only narrows the cached subset without changing any response; record the
narrowing in `CACHING.md` (and in the issue/ADR if one exists). If the acceptance is read as
"every catalog request must be cached", option 2 (per-tenant snapshot) is the only bounded
implementation; arbitrary-variant caching is not.

**Open questions**

- Are non-default page sizes common in the UI (e.g. `perPage=24`)? If yes, allow one bounded set
  of sizes rather than only the default.
- Should subject-filtered first pages be cached (bounded and useful) or bypassed (simplest)?

---

## 5. Decision register

Settle these before coding. Each row has a recommended default; the "changes if" column is the
condition that should reopen it.

| # | Decision | Recommended default | Changes if |
|---|---|---|---|
| D1 | `ICachingService` now vs follow-up | Follow-up issue; land #33 first | The team treats the abstraction as a release requirement, or #33 is not yet merged |
| D2 | Background tenant resolution | Derive the tenant from the changed entity (`CourseAsset.TenantId`) in the eviction policy; no platform change | A cached projection has no tenant-bearing entity in its write path |
| D3 | Subject-change invalidation | Tenant-wide view family tag | Per-subject precision becomes cheap (subject→courses lookup already exists) |
| D4 | Overview-upsert fix | Separate issue, keep the documented gap | #29/#30 owners accept the `ICommand` fix inside #33 |
| D5 | `/courses` listing scope | In-scope staff catalog; update issue wording | Product confirms it is admin-only, then remove caching |
| D6 | Redis/L2 | Document L1-only; platform issue | Multi-instance deployment is imminent |
| D7 | Eviction ownership | Persistence-layer `IEvictionStrategy` (change-set scan) + entity policies; UoW gains one call; `CourseCacheEvictionBehavior` deleted; commands stay cache-agnostic | A write path bypasses the instrumented UoW or change tracking |
| D8 | Key representation | Typed `CacheKey`/`CacheTag` if D1 is yes; otherwise remove the null sentinel | No shared service is adopted and strings stay |
| D9 | Test host Redis | No container; behavior tests on L1 | Redis is wired and L2 semantics matter |
| D10 | Runbook/doc updates | Yes, after fixes | Never (docs are the spec surface) |
| D11 | Catalog key cardinality | Cache only the default first page (`page=1`, `perPage=10`, no search, default sort); never key on caller parameters | The UI needs a small bounded set of page sizes, or a snapshot is proven necessary |
| D12 | Eviction flush point | Mapping-free outermost behavior, after commit, depth-gated | The platform provides a reliable post-commit hook |
| D13 | Eviction granularity | Tags, not keys (queue during the UoW, delete after success) | Versioned keys make precise per-key eviction reliable |

---

## 6. Proposed execution sequence

### Phase 0 — decisions (no code)

- Answer D1–D13 with the owners.
- If D1 = yes, draft the follow-up issue and the spec amendment (§4.4).
- If D4 = separate, create that issue and link it from `CACHING.md`.
- Confirm H1's production consumer scope (external bus, no HTTP context) with a test or logs.

### Phase 1 — persistence-layer eviction + correctness (test-first)

This phase replaces M5 and fixes H1/H2/M1 in one pass; doing the correctness fixes inside the
old switch would write them twice.

1. Add the infrastructure primitives in Shared: `CacheTag`, `IEvictionProvider`,
   `ScopedEvictionProvider`, `IEvictionStrategy`/`ChangeTrackerEvictionStrategy`,
   `IEntityEvictionPolicy`, and the mapping-free `CacheEvictionFlushBehavior`; wire the single
   `CollectAsync` call in the UoW. An empty policy set is the no-op path for modules without
   caching.
2. Move every existing mapping from `CourseCacheEvictionBehavior` into entity policies
   (`Course`, section, item, resource, plan, overview, subject, asset); delete the behavior.
   Run the existing integration suite unchanged.
3. **H1**: asset policy derives the tenant from `CourseAsset`; add the consumer-scope test.
4. **H2**: add `CourseViewsTag` to public/student view entries and the `Subject` policy; add the
   subject rename/delete view test.
5. **M1**: child/plan/asset policies queue `CatalogTag`; add the catalog freshness tests.
6. Add the tracking-only regression tests (reorder sections/items, update/remove plan).
7. Add the architecture test (Application has no caching references) and the entity mapping
   contract test.
8. **M4**: extend tenant isolation to catalog/view/readiness if not already covered.

Commit per slice with `fix(courses): ... (#33)` / `refactor(courses): ... (#33)` messages. The
provider + policy move can be reviewed as its own commit before the behavior fixes.

### Phase 2 — read-side cardinality and coverage

9. **D11**: gate the two course decorators and the subject decorator to the default first page;
   add the key-builder/bypass tests; update `CACHING.md` key families.
10. **M3**: complete the freshness matrix table.
11. **M2** (if in scope): change the overview command to `ICommand`; add the upsert test.
12. **L1/L4**: remove the dead tenant sentinel and test dead code while touching those files.

### Phase 3 — architecture (only if D1 = yes)

13. Add `ICachingService` + `CachePolicy` + typed keys in Shared; make `ScopedEvictionProvider`
    flush through it.
14. Migrate Courses, then Identity, Tenants, VideoUploading, `CachingRepository`.
15. Remove the HybridCache package reference from module projects.
16. Update `docs/CachingArchitecture.md` and `CACHING.md`.

### Phase 4 — platform (separate PR)

17. **P1**: fix the `AddHybridCache` lambda.
18. **P2**: wire Redis or document L1-only (D6).
19. **L2/L3**: docs and scope wording.

### Test commands

```bash
dotnet build AlphaZero.sln --nologo -v q

dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj --nologo -v q

dotnet test tests/Modules/Courses/Courses.Tests.Integration/Courses.Tests.Integration.csproj --nologo -v q
```

Run the integration suite alone (not concurrently with the unit build) to avoid the known
MSB4018 file-lock issue recorded in `spec-32/code-review.md`.

---

## 7. Whole-plan acceptance checklist

- [ ] Every cached surface in `CACHING.md` has an eviction trigger and a behavioral test.
- [ ] No cached key or tag can be built without a tenant.
- [ ] Asset/metadata changes are visible after the next read in a consumer-like scope.
- [ ] Subject rename/delete is visible in the student view inside its TTL.
- [ ] Student catalog counts/duration/plans are fresh after their mutations.
- [ ] Reorder and plan mutations evict without relying on an explicit `Update` call.
- [ ] All six cached surfaces have a cross-tenant isolation test.
- [ ] `Modules/Courses/Application` has no caching references (architecture test).
- [ ] Every entity type that feeds a cached projection has an eviction policy or an allowlist reason.
- [ ] No cached read key contains `search`, `sort`, `page` or `perPage`; non-default requests bypass.
- [ ] Eviction flushes after the outermost commit; no command-type mapping remains in a behavior.
- [ ] Docs: no claim of Redis L2 unless a distributed cache is registered.
- [ ] `UpsertCourseOverviewCommand` either persists and evicts, or the gap is explicitly accepted.
- [ ] The `ICachingService` decision is recorded in the issue/ADR before implementation.
- [ ] Unit + integration suites green.

---

## 8. Non-goals

- Re-reviewing #29/#32 code beyond the caching interactions above.
- Redis deployment, production sizing or multi-instance validation.
- Benchmarking cache hit rates or latency; this review is correctness/design only.
- Changing the student catalog/view response shapes.
- Changing authentication, permissions or tenant resolution for HTTP requests.

---

## Scope record — complexity gate

Feature answers: D1=A (`plan-eng-review-icaching-timing` — `ICachingService` seam plus migrate Courses, Identity, Tenants, VideoUploading and `CachingRepository` in this work); D2=B (`plan-eng-review-out-of-scope` — overview command persistence/eviction fix only; the no-op `AddHybridCache` lambda and Redis stay out); D3=A (`plan-eng-review-courses-listing-scope` — `/courses` stays cached; amend issue #33 and `CACHING.md` wording).
Structure: B (`plan-eng-review-structure` — smaller arrangement: keep the UnitOfWork → strategy → provider → flush contracts and typed keys/tags; one Courses eviction policy class with an internal concrete-type map instead of per-entity policy classes; ~11 new types, ~15 files).
Accepted scope: shared `ICachingService` with all four modules migrated; `UpsertCourseOverviewCommand` made persistable and eviction-tested; staff `/courses` caching retained with issue #33 / `CACHING.md` amendments; persistence-layer eviction with a single Courses policy class, tag-based flush after the outermost commit.
Pending remedies: none (R1 resolved by D5=A: ambient tenant context plus consume/publish filters).

### Approved scope amendments (applied to the plan)

1. **D1=A** — §5 D1 and the §4 preface no longer describe `ICachingService` as a follow-up: this work ships the shared service and migrates Courses, Identity, Tenants, VideoUploading and `CachingRepository`. The "while D1 = follow-up" fallback at plan:904 is removed.
2. **D2=B** — Scope stays limited to making `UpsertCourseOverviewCommand` persist (`ICommand<Success>`) with a persistence/eviction test. Redis registration remains out of scope. **Superseded for the lambda by D7=C:** the no-op `AddHybridCache` options lambda (`AlphaZero.Shared/Infrastructure/DependencyInjection.cs:68-78`) is fixed in this PR, applying the intended 10-minute distributed / 5-minute local defaults with a configuration test.
3. **D3=A** — `/courses` stays cached. Issue #33 and `CACHING.md` are amended: staff catalog caching is intentional; default-page-only keys supersede the issue's filter-shape design; `docs/CachingArchitecture.md` is corrected to match the actual in-memory deployment; "keys start with the tenant id" becomes "keys start with `courses:` and carry the tenant segment".
4. **D4=B** — One Courses eviction policy class with an internal concrete-type map replaces per-entity policy classes. Retained contracts: `IEvictionStrategy`, `IEvictionProvider`, tag flush after the outermost commit.
5. **D4=B consequence (S2)** — `IEntityEvictionPolicy` becomes `bool Handles(Type mappedType)` (or `IReadOnlyCollection<Type>`), dispatching on concrete mapped types (`VideoCourseAsset`, `DocumentCourseAsset`, `AssessmentCourseAsset`, the four `*CurriculumResource` types) with an assignable-base fallback for future subclasses.
6. **D5=A** — Ambient tenant context plus MassTransit consume/publish filters; `TenantId` added to the three video lifecycle events; eviction tags use the resolved tenant; the consumer-scope test runs without the static test fallback.
7. **Numbers** — Replace "~20-case switch" with 26 `case` lines / ~29 labels; "seven `GetOrCreateAsync`" with six; "6 cached surfaces" with seven (staff list, student catalog, staff tree, public view, overview, readiness, subjects); "3 of 6" with 3 of 7.
8. **S7** — Record the UoW-seam limitation explicitly (saves that bypass `IUnitOfWork` are not evicted) and add a guard test/allowlist instead of implying completeness.
9. **S6** — The policy contract test enumerates mapped concrete entity types and excludes owned/shadow/complex types.

Scope Challenge result: `scope accepted as-is` (no reduction; additions approved through D1–D5).

---

## Decision ledger

### R1: consumer-scope tenant write path for asset/metadata commands

Finding: S1 [P1, confidence 9] `Modules/Courses/Application/Courses/Commands/Assets/MarkCourseAssetAvailable.cs:26-28` reads `await _assetRepository.GetById(request.AssetId, ct)` then returns `Error.NotFound` when null; `BaseRepository.GetById` is `_context.Set<TEntity>().FirstOrDefaultAsync(d => d.Id == id)` (`AlphaZero.Shared/Infrastructure/Repositores/IRepository.cs:117-121`), so the tenant filter `e.TenantId == context.TenantId` (`AlphaZero.Shared/Infrastructure/Database/ModelBuilderExtensions.cs:66`) with `AppDbContext.TenantId => _tenantProvider.GetTenant()` (`Modules/Courses/Infrastructure/Persistance/AppDbContext.cs:35`) hides every row when `HttpTenantProvider` returns null outside HTTP (`AlphaZero.Shared/Infrastructure/Tenats/ITenantProvider.cs:43-57`). The eviction tag is also built from `tenantProvider.GetTenant()` (`CourseCacheEvictionBehavior.cs:156-158`), producing `courses:assets:none` while readiness entries are tagged with the real tenant. S11 [P1, confidence 8] the same loss affects the upstream video callback: `SQSVideoPublishedConsumer.cs:37` sends `CompleteVideoPublishingCommand` with `msg.TenantId` but the handler resolves the tenant-owned `Video` via `GetByIdAsync` → `FindAsync` (`Modules/VideoUploading/Infrastructure/Repositories/VideoRepository.cs:42-45`, `Video.cs:8`), so in a bus scope the lookup is filtered out and the publish event may never be produced.
Plan baseline: H1 accepts that consumer-scope asset changes are invisible; the plan tests tenant propagation only through the HTTP read path and the integration host keeps `TestTenantProvider`'s static fallback (`tests/Modules/Courses/Courses.Tests.Integration/Abstractions/ApiFactory.cs:104-116`), which would make a consumer-scope test pass while production stays broken.
Runtime evidence: no production tenant setter or consume filter exists (`grep ITenantProvider`, `grep ConsumeFilter`); `TestTenantProvider.StaticTenantId` is an AsyncLocal/static fallback in the test host only.
Comparison grid:
| Choice | Current | A) Ambient tenant context + bus filters | B) Courses-only filtered lookup bypass | C) Disable readiness caching + defer |
|---|---|---|---|---|
| Tenant visible in a bus scope | none | from message `TenantId` via consume filter; publish filter stamps it | only inside the Courses consumer handler | none |
| Event contracts | three video lifecycle events carry no `TenantId` | add `TenantId` to those events (or stamp an envelope header) | unchanged | unchanged |
| Upstream video publish callback | tenant-owned `Video` lookup filtered out | fixed by the ambient context | stays broken | stays broken |
| Readiness cache after an asset change | stale until TTL | correct after the consumer command commits | correct only if the event is produced | readiness is not cached (DB every read) |
| Blast radius | — | shared tenant infrastructure, VideoUploading producer, Courses consumers | one repository method plus the Courses consumer path | one read path / cache guard |
| Tests | none | consume-filter test plus end-to-end asset test in a real null-tenant scope | Courses consumer test with a null tenant | readiness bypass test |
Question D5:
How should the consumer-scope tenant loss be fixed `plan-eng-review-consumer-tenant`?
D5 — Repair tenant context in bus scopes so asset/metadata eviction can work
Project/branch/task: `spec/33-caching`; the caching plan's readiness freshness depends on commands that currently cannot see tenant-owned rows in a bus scope.
ELI10: The caching plan promises that a video finishing processing refreshes the course readiness cache. But background messages run outside an HTTP request, so the app cannot tell which tenant they belong to, and every tenant-owned row is hidden. The update fails, the eviction tag is built with no tenant, and a test host's fake tenant makes the test look green while production stays broken. We must choose how far to repair that tenant plumbing.
Stakes if we pick wrong: readiness (and every cached projection touched by bus-driven writes) keeps serving stale data for its full TTL, and the plan's "visible after the next read in a consumer-like scope" acceptance item cannot honestly be checked.
Recommendation: A because the tenant is already present on the queue messages; propagating it into an ambient context fixes both the Courses consumers and the video publish callback with one platform-level mechanism.
Note: options differ in kind, not coverage — no completeness score.
Pros / cons:
A) Ambient tenant context plus consume/publish filters (recommended)
  ✅ Fixes every bus-scope write in both Courses and VideoUploading, and makes the acceptance test meaningful.
  ❌ Touches shared tenant infrastructure plus three integration-event contracts and their producer.
B) Courses-only filtered lookup bypass
  ✅ Small: one repository method and the Courses consumer path; no contract or producer change.
  ❌ The upstream publish callback still cannot load its tenant-owned video, so the event may never be produced and the readiness fix is unobservable in production.
C) Disable readiness caching and defer tenant propagation
  ✅ Guarantees no stale readiness data and keeps this PR focused on HTTP-path caching.
  ❌ Drops one of the cached surfaces and leaves the underlying bus-scope tenant bug for a later issue.

State: approved
Actual answer: A) Ambient tenant context plus consume/publish filters (D5, `plan-eng-review-consumer-tenant`)
Accepted scope: Add a scoped ambient tenant context consulted by `ITenantProvider`; add MassTransit consume/publish filters that set it from the message `TenantId`; add `TenantId` to `VideoPublishedIntegrationEvent`, `VideoProcessingFailedEvent` and `VideoMetadataChangedIntegrationEvent` (producer updated); fix `CourseCacheEvictionBehavior`/policy tags to use the resolved tenant; add a consume-filter test plus an end-to-end asset-change test that runs in a genuinely null-tenant bus scope (not the static test fallback).
History: none

Approval readiness: PASS — D1=A (`plan-eng-review-icaching-timing`), D2=B (`plan-eng-review-out-of-scope`), D3=A (`plan-eng-review-courses-listing-scope`), D4=B (`plan-eng-review-structure`), D5=A (`plan-eng-review-consumer-tenant`), D6=A (`plan-eng-review-todo-l2`), D7=C (`plan-eng-review-todo-hybridcache-options`). Every accepted remedy maps to an explicit answer; the Redis/L2 deferral is recorded in `TODOS.md`; no accepted remedy is pending.

---

REALITY: premise-table pass F1–F12 each verified against source; unverified=0 src/alphazero-api/Modules/Courses/Infrastructure/Caching/CachedCourseQueryService.cs:33
REALITY: already-done pass no commits on the named caching/UoW/DI files on origin/main since 2026-10-20; no proposed symbols present; HEAD a1ebb79e src/alphazero-api/AlphaZero.Shared/Infrastructure/DependencyInjection.cs:68
REALITY: surface-check pass `gh`, `dotnet`, `AlphaZero.sln`, both test projects and all seven routes exist src/alphazero-api/Modules/Courses/Presentation/Courses/List/ListCourses.cs:1
REALITY: binding-decisions pass decision search returned no branch decisions; §1 user decisions treated as binding and D1–D5 recorded spec-33/code-review-plan.md:1
REALITY: numbers pass 26 case lines src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseCacheEvictionBehavior.cs:1; 29 case labels src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseCacheEvictionBehavior.cs:1; 15 Evict bodies src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseCacheEvictionBehavior.cs:1; 6 GetOrCreate bodies src/alphazero-api/Modules/Courses/Infrastructure/Caching/CachedCourseQueryService.cs:1; 7 key families src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseCacheKeys.cs:32-58; 3 of 7 tenant surfaces tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs:1
REALITY: deferred-asks finding issue #33 filter-shape key design superseded by default-page-only and its Redis premise conflicts with the architecture doc; amendment approved by D3=A src/alphazero-api/Modules/Courses/Infrastructure/Caching/CourseCacheKeys.cs:32
REALITY: pr-count pass no .gstack/ship-policy.json; default one PR per wave, platform work folded into it by D1=A a1ebb79e

---

# Engineering Review — spec-33/code-review-plan.md

## 1. Architecture review

**Data flow (proposed)**

```
MediatR command
  └─ CacheEvictionFlushBehavior (outermost)          depth counter
       └─ TransactionalBehavior (ITransactionalCommand only)
            └─ UnitOfWorkDecoratorCommandHandler
                 └─ Handler mutates tracked entities
                      └─ UnitOfWork.SaveChangesAsync
                           ├─ domain-event loop (nested Send)
                           ├─ IEvictionStrategy.CollectAsync
                           │    └─ ChangeTracker scan → policy.Handles(type) → tags
                           │         └─ IEvictionProvider.Evict(tag)  (HashSet, scoped)
                           └─ DbContext.SaveChangesAsync  (COMMIT)
       └─ on success → IEvictionProvider.FlushAsync   (after commit)
          on ErrorOr error / exception → Discard (no flush)
```

Bus path:

```
Queue message (TenantId)
  └─ consume filter → ambient tenant context → ITenantProvider
       └─ Courses consumer → Send(command)
            └─ same MediatR chain; tenant filter now resolves; tags use the real tenant
```

**Findings**

- **A1 [P1] (confidence: 9/10)** `Modules/Courses/Application/Courses/Commands/Assets/MarkCourseAssetAvailable.cs:26-28` — the consumer-scope command cannot load a tenant-owned asset because the tenant filter resolves to null outside HTTP; the eviction tag also falls back to `courses:assets:none` (`CourseCacheEvictionBehavior.cs:156-158`). The same loss breaks the upstream `CompleteVideoPublishingCommand` lookup (`SQSVideoPublishedConsumer.cs:37`, `VideoRepository.cs:42-45`). Accepted remedy: D5=A (ambient tenant context plus consume/publish filters).
- **A2 [P2] (confidence: 8/10)** `AlphaZero.Shared/Infrastructure/DependencyInjection.cs:68-78` — only the in-process L1 cache is registered (no `AddStackExchangeRedisCache` anywhere), so eviction is per-process and staleness is bounded only by TTL (2–15 min) if the deployment scales out. `docs/CachingArchitecture.md` states the deployment is single-server, while issue #33 assumes Redis; D2=B keeps Redis out of scope, so the plan must record the single-instance assumption and the TTL bound. Accepted doc amendment: D3=A.
- **A3 [P3] (confidence: 7/10)** `spec-33/code-review-plan.md:898-899` — policies may query the context during `CollectAsync`, which runs before `SaveChanges`; derived tags must come from `EntityEntry` state and foreign keys, not from re-querying values that are not yet committed. Add this constraint to the policy contract.

**Dispositions:** A1 accepted (D5=A); A2 accepted as documentation (D3=A); A3 accepted as a plan constraint.

## 2. Code quality review

**Shared-code evaluation — `ICachingService` (D1=A)**

- **Callers proven (authored source):** `Modules/Courses/Infrastructure/Caching/CachedCourseQueryService.cs:42,62,85,99,113,127`; `CachedSubjectQueryService.cs:25`; `Modules/Identity/.../ConditionDefinition.cs:30`; `PublicKeyProvider.cs:31,43`; `CachingTenantUserPrincipalAssignmentRepository.cs:28`; `Modules/VideoUploading/.../DefaultVideoEncryptionService.cs:32`; `Modules/Tenants/.../TenantBrandingUpdatedConsumer.cs:39-40`; shared `CachingRepository.cs:23-27`. Thirteen call sites across five modules.
- **Reuse before extracting:** `HybridCache` already provides `GetOrCreateAsync`/`RemoveByTagAsync`/`RemoveAsync`; the seam adds typed keys/tags/policy and a tenant-aware scope guard, not a new store. No new dependency.
- **Keep the helper small:** contract `GetOrCreateAsync<T>(CacheKey, CachePolicy, factory)`, `RemoveByTagAsync(CacheTag)`, `RemoveAsync(CacheKey)`; `CacheScope` owns the null-tenant bypass; callers migrated one surface at a time.
- **Change size [estimated]:** implementation removed ~70–90 lines (per-call options/key wiring), added ~60–80 lines (interface, typed values, implementation), net saving ~0–20 lines; total change including tests and caller integration grows by ~250–400 lines. The benefit is reliability (tenant-safe keys, one bypass rule), not line savings.
- **Blast radius:** a bug in the shared service affects every cached surface; keep it a thin wrapper with no own state and cover it with the contract tests below.

**Findings**

- **Q1 [P2] (confidence: 9/10)** `spec-33/code-review-plan.md:852-857,893-897` — `IEntityEvictionPolicy.EntityType` plus dictionary dispatch on `entry.Metadata.ClrType` cannot express the approved one-policy-with-a-concrete-type-map arrangement. EF reports `VideoCourseAsset`/`DocumentCourseAsset`/`AssessmentCourseAsset` (`CourseMediaAsset.cs:8,81,115`) and the four `*CurriculumResource` types (`CurriculumResource.cs:7,32`), so a policy declared for the abstract base would never match. Accepted contract correction: `Handles(Type)` / `IReadOnlyCollection<Type>`, keyed by concrete mapped types.
- **Q2 [P2] (confidence: 8/10)** `CachedCourseQueryService.cs:24-30` and `CachedSubjectQueryService.cs:18-22` — the null-tenant bypass guard is duplicated in all seven cached read methods. `ICachingService`/`CacheScope` should own it once; otherwise the "no key without a tenant" invariant is enforced in seven places and the `none` key remains reachable.
- **Q3 [P2] (confidence: 8/10)** `AlphaZero.Shared/Infrastructure/Repositores/CachingRepository.cs:23-27` — the class is never registered (no callers) and its key `$"{typeof(TEntity).Name}:{id}"` has no tenant segment. D1=A lists it for migration; delete it instead of carrying a tenant-unsafe dead path into the shared API.
- **Q4 [P3] (confidence: 7/10)** `CourseCacheEvictionBehavior.cs:160-166` — `catch (Exception ex)` also swallows `OperationCanceledException`. The replacement flush should swallow cache-backend failures only and let cancellation propagate.
- **Q5 [P3] (confidence: 8/10)** `Modules/Identity/.../CachingTenantUserPrincipalAssignmentRepository.cs:26` caches `user_assignments:{tenantUserId}` while `ConditionDefinition.cs:30` caches `ConditionDefinition_{name}` — neither carries an explicit tenant segment. Investigate in the D1=A migration whether the key is globally unique or tenant-derived; if not, add the tenant segment.

**Dispositions:** Q1 accepted (D4=B consequence); Q2/Q3/Q4/Q5 accepted into the migration tasks (no new question; they follow from D1=A and D4=B).

## 3. Test review

**Framework detection:** xUnit (`xunit` + `xunit.runner.visualstudio` in `tests/Modules/Courses/Courses.Tests.Integration/Courses.Tests.Integration.csproj:20-21`), Testcontainers Postgres plus WireMock in `ApiFactory.cs`; no `## Testing` section in `CLAUDE.md`, so detection is from project markers.

**Coverage diagram**

```
CODE PATHS                                                     USER FLOWS
[+] Modules/Courses/Infrastructure/Caching                     [+] Student catalog
  ├── CachedCourseQueryService (6 methods)                       ├── [★★ TESTED] default page — CoursesCacheTests
  │   ├── [GAP]        null-tenant bypass (no key written)       ├── [GAP] [→E2E] subject rename → new name in catalog
  │   ├── [★★ TESTED]  cache hit == miss (7 surfaces)            ├── [GAP]        non-default search/page bypass
  │   └── [GAP]        tenant segment on every key
  ├── CachedSubjectQueryService                                  [+] Staff course edits
  │   ├── [★★ TESTED]  list cached — CoursesCacheTests           ├── [★★ TESTED] tree view refresh — CoursesCacheTests
  │   └── [GAP]        GetSubjectById uncached (by design?)      ├── [GAP]        overview write visible
  └── CourseCacheEvictionBehavior (to be deleted)                └── [GAP]        reorder/plan mutation refresh
      └── [GAP]        asset/metadata consumer scope

[+] Proposed eviction pipeline (D1=A, D4=B)                     [+] Media lifecycle
  ├── [GAP] CacheScope / ICachingService bypass                  ├── [GAP] [→E2E] video published → readiness fresh
  ├── [GAP] UoW CollectAsync before commit                       ├── [GAP] [→E2E] asset failed → readiness fresh
  ├── [GAP] dispatch on concrete TPH types                       └── [GAP]        event replay / duplicate delivery
  ├── [GAP] policy map for course/subject/asset/curriculum
  ├── [GAP] flush after commit; discard on error                 [+] Tenant isolation
  ├── [GAP] nested Send depth counter                            ├── [★★ TESTED] 3 of 7 surfaces
  └── [GAP] ambient tenant consume filter                        └── [GAP]        4 remaining surfaces

[+] Overview command
  └── [GAP] UpsertCourseOverviewCommand persists + evicts

COVERAGE: 3/11 code paths (27%)  |  User flows: 1/7 (14%)
QUALITY: ★★:7 existing surface tests  |  GAPS: 16 (4 E2E)
```

**Required tests (all `[→E2E]` where a bus or second tenant is involved)**

| # | Test | Level | Assertion | Value card |
|---|---|---|---|---|
| T1 | Asset readiness via consumer scope | E2E | Publish `VideoPublishedIntegrationEvent` (with `TenantId`) → asset available → next readiness read reflects it | protects=consumer-scope tenant + tag; fails_when=tenant is null or tag is `none`; why_new=test host fallback hides it; seam=none; no_claim=SQS transport (in-memory bus) |
| T2 | Tenant isolation on all seven surfaces | Integration | Tenant B sees no Tenant A entry for catalog, view, overview, readiness, tree, subjects, staff list | protects=cross-tenant bleed; fails_when=key/tag omits tenant; why_new=only 3 covered; seam=none; no_claim=none beyond protects |
| T3 | Policy contract | Unit | Every mapped concrete entity type feeding a cached projection is `Handles`-matched; owned/shadow/complex types excluded | protects=silent policy miss; fails_when=a type is unmapped or keyed to an abstract type; why_new=none exists; seam=none; no_claim=future types |
| T4 | Nested flush depth | Unit | Domain-event handler Send inside an outer command flushes once, after the outer commit | protects=eviction after commit; fails_when=early or missing flush; why_new=none exists; seam=none; no_claim=none beyond protects |
| T5 | Flush failure does not fail the write | Unit | Cache `RemoveByTagAsync` throws → command returns success, warning logged | protects=write availability; fails_when=exception escapes; why_new=none exists; seam=none; no_claim=backend failure modes |
| T6 | Overview persistence | Integration | Update overview → row persisted → next read shows it → tag evicted | protects=overview write path; fails_when=command not `ICommand` or no eviction; why_new=none exists; seam=none; no_claim=none beyond protects |
| T7 | Non-default catalog bypass | Integration | `page=2`/`perPage=50`/`search=x` never write a cache key | protects=bounded key cardinality; fails_when=a caller-controlled value enters a key; why_new=none exists; seam=none; no_claim=none beyond protects |
| T8 | Subject rename/delete | Integration | Catalog row shows the new subject name after the command | protects=catalog tag eviction; fails_when=catalog tag not queued; why_new=none exists; seam=none; no_claim=none beyond protects |
| T9 | Reorder/plan update without `Update` | Integration | Tracked mutation evicts course + catalog | protects=change-tracker-based eviction; fails_when=policy only runs on explicit `Update`; why_new=none exists; seam=none; no_claim=none beyond protects |

**Test Plan Artifact:** `/home/azero/.gstack/projects/Azero77-alpha-zero/azero-spec-33-caching-eng-review-test-plan-20261024-185324.md`.

**Tests to retire:** the unused `c(string json)` helper and the duplicated `catalogB` assertion in `CoursesCacheTests.cs` (lines ~286, ~325). The tenant-isolation fact is retained and extended.

**Dispositions:** T1–T9 accepted as required proof of approved behavior (no unresolved test choice). T3/T7 follow from D4=B and §1; T1 follows from D5=A.

## 4. Performance review

- **P1 [P2] (confidence: 8/10)** `CachedCourseQueryService.cs:113-121` — readiness entries carry both `CourseTag` and `CourseAssetsTag`; the coarse `CourseAssetsTag` evicts readiness for every course in a tenant on each asset change. Scale: one asset update evicts N readiness entries, N = active courses per tenant. Use the asset's `CourseId` to queue `CourseTag` and keep the coarse tag only when the course cannot be resolved.
- **P2 [P3] (confidence: 7/10)** `AlphaZero.Shared/Infrastructure/DependencyInjection.cs:68-78` — no L1 size limit or payload cap is configured. With default-page-only keys, cardinality is bounded by active courses/tenants and TTLs of 2–15 min, so this is a watch item rather than a defect.
- **P3 [P3] (confidence: 7/10)** Proposed `ChangeTrackerEvictionStrategy` adds one `Entries()` materialization, a `HashSet<CacheTag>` and one `RemoveByTagAsync` per distinct tag after commit. Cost is proportional to tracked entities per save and is negligible against the DB round trip.
- **P4 [P3] (confidence: 6/10)** Readiness recomputation is cached for 3 minutes; after T1 it is correct and avoids recomputation on every read. No benchmark was run; do not claim a latency improvement.

**Dispositions:** P1 accepted into the policy task; P2–P4 recorded, no change.

## NOT in scope

- Redis/L2 registration and multi-instance invalidation — issue #33 defers production sizing; D2=B keeps the no-op `AddHybridCache` lambda unchanged.
- Changing student catalog/view response shapes or pagination contracts.
- Authentication/authorization changes for HTTP requests (the staff `/courses` permission stays as-is, D3=A).
- Fixing unrelated Identity/VideoUploading cache semantics beyond the mechanical `ICachingService` migration.
- Benchmarking hit rates or latency.

## What already exists

- `HybridCache` read-through in Courses, Identity, Tenants and VideoUploading — the `ICachingService` seam wraps it; it does not replace it.
- `CourseCacheKeys` typed key/tag builders (7 families) — reused as the basis for `CacheKey`/`CacheTag`.
- `CourseCacheEvictionBehavior`’s eviction scopes (course, course+catalog, subjects, assets) — its knowledge moves into policies; the class is deleted.
- Unit-of-work decorator and `TransactionalBehavior` — the flush behavior slots into the existing MediatR order without changing handlers.
- `TestTenantProvider`/`HttpTenantProvider` split in the integration host — reused, with the static fallback removed for the new consumer test.

## Failure modes

| Path | Realistic failure | Test / handling | User impact |
|---|---|---|---|
| Bus → asset command | Tenant null in the consume scope; asset lookup returns null | D5=A fix + T1; today only a warning log | Silent stale readiness until TTL — **critical gap** until T1 lands |
| ChangeTracker → policy | A concrete TPH type has no matching policy; no tag queued | Contract test T3; TTL fallback | Silent stale surface until TTL — **critical gap** until T3 lands |
| Flush after commit | Cache backend throws | Logged and swallowed; T5 | Write succeeds; stale until TTL — handled, needs the test |
| Overview command | Command is not `ICommand`, never saves | D2=B + T6 | Silent no-op — fixed by this plan |
| Multi-instance | L1 eviction is per process | Documented; D6 TODO | Stale until TTL — accepted limitation |

**Critical gaps:** 2 (consumer-scope tenant loss; unmapped policy type).

## Worktree parallelization strategy

| Step | Modules touched | Depends on |
|---|---|---|
| T1 ambient tenant + bus filters | Shared/Tenants, Shared messaging, VideoUploading, Courses consumers | — |
| T2 `ICachingService` | Shared/Caching, Courses, Identity, Tenants, VideoUploading | T1 (tenant-aware scope contract) |
| T3 eviction pipeline | Shared/Infrastructure, Courses | T2 (typed tags) |
| T4 overview persistence | Courses/Application | T3 |
| T5 lambda fix | Shared/Infrastructure | — |
| T6/T8 tests | tests | T1–T5 |
| T7 docs | docs + CACHING.md | decisions only |

**Parallel lanes:** Lane A: T1 → T2 → T3 → T4 → T6 (shared modules, sequential). Lane B: T5 (independent). Lane C: T7 (independent). **Execution order:** launch B + C alongside A; merge A; then T6/T8. **Conflict flags:** T1 and T2 both touch shared tenant/caching registration — sequence them.

Files needing inline diagrams when implemented: `AlphaZero.Shared/Infrastructure/UnitOfWork.cs`, `ChangeTrackerEvictionStrategy`, `CacheEvictionFlushBehavior`, `UnitOfWork` + nested `Send` depth handling.

## Implementation Tasks
Synthesized from this review's findings. Each task derives from a specific
finding above. Run with Claude Code or Codex; checkbox as you ship.

- [ ] **T1 (P1, human: ~1 day / CC: ~30 min)** — Shared/Tenants + bus — propagate tenant into bus scopes
  - Surfaced by: Architecture A1 (S1/S11), D5=A
  - Files: shared tenant context, MassTransit consume/publish filters, `Modules/VideoUploading/IntegrationEvents/Events.cs`, Courses and VideoUploading consumers
  - Verify: consume-filter test plus asset-readiness E2E in a session with no static tenant fallback
- [ ] **T2 (P1, human: ~3 days / CC: ~1 h)** — Shared/Caching — add `ICachingService`, typed keys/tags/policy, tenant-aware `CacheScope`; migrate five modules
  - Surfaced by: Code Quality Q2/Q5, D1=A
  - Files: `AlphaZero.Shared/Infrastructure`, `Modules/*/Infrastructure/Caching|Repositories|Services`
  - Verify: `dotnet build AlphaZero.sln`; key-builder and null-tenant unit tests
- [ ] **T3 (P1, human: ~3 days / CC: ~45 min)** — Shared/Infrastructure + Courses — persistence eviction with a concrete-type policy map
  - Surfaced by: Architecture A3, Code Quality Q1, D4=B
  - Files: `UnitOfWork.cs`, new eviction types, one Courses policy, `Modules/Courses/Infrastructure/Caching`, Courses DI
  - Verify: policy-contract and nested-depth unit tests; delete `CourseCacheEvictionBehavior`
- [ ] **T4 (P1, human: ~4 h / CC: ~20 min)** — Courses/Application — persist and evict the overview command
  - Surfaced by: D2=B, F7
  - Files: `UpsertCourseOverview.cs`
  - Verify: integration test T6
- [ ] **T5 (P2, human: ~30 min / CC: ~5 min)** — Shared/Infrastructure — fix the no-op `AddHybridCache` lambda
  - Surfaced by: F3, D7=C
  - Files: `DependencyInjection.cs:68-78`
  - Verify: unit test asserting the configured default expirations
- [ ] **T6 (P1, human: ~2 days / CC: ~45 min)** — Tests — critical-gap coverage
  - Surfaced by: Test review T1/T2/T3
  - Files: `tests/Modules/Courses/Courses.Tests.Integration/CoursesCacheTests.cs`, `Courses.UnitTests`
  - Verify: `dotnet test tests/Modules/Courses/Courses.Tests.Integration/Courses.Tests.Integration.csproj`
- [ ] **T7 (P2, human: ~2 h / CC: ~10 min)** — Docs — amend issue #33, `CACHING.md`, `docs/CachingArchitecture.md`
  - Surfaced by: Architecture A2, D3=A
  - Verify: no remaining Redis-L2 or tenant-prefix claim
- [ ] **T8 (P2, human: ~4 h / CC: ~20 min)** — Tests — remaining pipeline and user-flow coverage (T4–T9)
  - Surfaced by: Test review gaps
  - Verify: subject rename, reorder, bypass, nested flush, flush failure, overview
- [ ] **T9 (P3, human: ~2 h / CC: ~10 min)** — Courses/Caching — per-course asset eviction tag
  - Surfaced by: Performance P1
- [ ] **T10 (P3, human: ~1 h / CC: ~5 min)** — Shared/Caching — do not swallow cancellation
  - Surfaced by: Code Quality Q4
- [ ] **T11 (P2, human: ~1 h / CC: ~10 min)** — Shared/Infrastructure — delete `CachingRepository`
  - Surfaced by: Code Quality Q3
- [ ] **T12 (P3, human: ~2 h / CC: ~15 min)** — Identity — audit cache keys for a tenant segment
  - Surfaced by: Code Quality Q5

## Unresolved decisions

None.

## Suppressed findings

None. No finding was held back at confidence < 5.

## Completion summary

- Step 0: Scope Challenge — scope accepted as-is
- Reality rows: 7/7 applicable rows emitted (`PLAN_REALITY` verdict complete), 0 unverified premises
- Architecture Review: 3 issues found
- Code Quality Review: 5 issues found
- Test Review: diagram produced, 16 gaps identified
- Performance Review: 4 issues found
- NOT in scope: written
- What already exists: written
- TODOS.md updates: 1 item proposed and accepted (D6=A); D7=C moved the lambda fix into this PR
- Failure modes: 2 critical gaps flagged
- Unresolved decisions: 0 in this review
- Outside voice: provider codex unavailable (CLI not installed); native fallback did not complete within five minutes — unavailable coverage
- Parallelization: 3 lanes, 2 parallel / 3 sequential
- Lake Score: 3/3 = answers picking a 10/10 option / answers scored for Completeness (D1, D2, D3); D4–D7 differed in kind or timing

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | — |
| Outside Review | codex (plan-eng-review) | Independent 2nd opinion | 1 | UNAVAILABLE | no findings — missing outside coverage |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | ISSUES OPEN | 28 issues, 2 critical gaps |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | — | — |
| DX Review | `/plan-devex-review` | Developer experience gaps | 0 | — | — |

- **OUTSIDE COVERAGE:** codex, plan-review, unavailable — Codex CLI not installed; native fallback did not complete within five minutes. No outside findings; coverage is missing, not clean.
- **VERDICT:** NOT CLEARED — Eng Review has open issues; eng review required.

NO UNRESOLVED DECISIONS
