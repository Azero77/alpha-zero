# Cache tree — every key, tag and family, with who reads and who evicts it (#33)

This is the live inventory of the cache: one entry per **family**, the **keys** inside it, which
**repository / query service** reads through each key, and which write path **evicts** it. It is
derived from source (file:line references) as of 2026-10-29 on `spec/33-caching`. When you add,
move or delete a key, update this file in the same commit.

Related documents:

- [`docs/caching/README.md`](README.md) — how the read/write pipeline works end to end.
- [`docs/CachingArchitecture.md`](../CachingArchitecture.md) — platform-level overview.
- [`Modules/Courses/CACHING.md`](../../src/alphazero-api/Modules/Courses/CACHING.md) — Courses
  tolerances and the full rationale per surface.

---

## 0. How to read this tree

Three eviction styles exist, and every entry below says which one governs it:

| Style | Mechanism | Used by |
|---|---|---|
| **Queue → interceptor flush** | A write decorator queues key sets in `IEvictionProvider`; the two EF Core interceptors in `UnitOfWork.cs` call `RemoveAsync` per key / `RemoveByTagAsync` per tag at the save/commit point | Courses only |
| **Synchronous repository eviction** | A write decorator calls `ICachingService.Evict`/`RemoveAsync` directly at its write site | Identity |
| **Consumer / explicit removal** | A consumer removes key + tag when the owning event arrives | Tenants (branding) |

Plus the implicit fourth column: **TTL-only** — entries with no eviction site age out by expiry.

Key grammar (enforced by `CacheKey`/`CacheScope`):

- `family:part:part…` — family may contain `:`, parts may not; parts are non-empty.
- Tenant-scoped keys go through `CacheScope`, which prepends `{tenantId:N}` as the first part.
  A key built without a tenant throws — cache-less scopes **bypass** instead.
- Only `CacheKey.Create`-style keys (Identity, Tenants, VideoUploading SSM) are deliberately
  **global** (no tenant segment); their owners are globally unique ids.

---

## 1. Courses module — the tenant-scoped tree

**Module:** `src/alphazero-api/Modules/Courses` · **Style: queue → interceptor flush**
**Registry:** `Infrastructure/Caching/CourseCacheKeys.cs` (keys, key-set helpers, policies) +
`CourseCacheTags.cs` (the one tag)

Every key below is really `…:{tenantN}:…` — the tenant segment is prepended by `CacheScope`.
A `TenantId == Guid.Empty` aggregate queues nothing (no placeholder tenant is ever written).

```
{tenantN}
├── courses:catalog:{tenantN}
│     ├── class=staff        (2 min)  Staff listing
│     └── class=student      (2 min)  Student catalog
├── courses:view:{tenantN}:{courseIdN}
│     ├── class=staff        (10 min) Staff tree projection
│     └── class=public       (10 min) Public summary  ◄── carries tag courses:views:{tenantN}
├── courses:overview:{tenantN}:{courseIdN}            (10 min)
├── courses:readiness:{tenantN}:{courseIdN}           (3 min)
├── courses:status:{tenantN}:{courseIdN}              (eviction-only — never read-cached)
├── courses:subjects:{tenantN}                        (15 min)
├── entity:{tenantN}                          (all 5 min, EntityPolicy)
│     ├── entity:course:{tenantN}:{courseIdN}         (eviction-only today)
│     ├── entity:overview:{tenantN}:{courseIdN}       (◄ read-through: GetByCourseIdAsync)
│     ├── entity:asset:{tenantN}:{assetIdN}           (eviction-only today)
│     └── entity:subject:{tenantN}:{subjectIdN}       (eviction-only today)
└── [tag] courses:views:{tenantN}   ← THE one fan-out family (G4.1)
```

### 1.1 Keys — reader and evictors per key

| Key (parts after family) | TTL | Read by | Evicted by (key-set helper) | Carries tag |
|---|---|---|---|---|
| `courses:catalog` → `class=staff` | 2 min | `CachedCourseQueryService.ListCourses` (`:30-36`), default shape only | `EvictCourse`, `EvictCourseOverview`, `EvictCourseAsset` | — |
| `courses:catalog` → `class=student` | 2 min | `CachedCourseQueryService.ListCourseCatalog` (`:50-62`), default shape only | `EvictCourse`, `EvictCourseOverview`, `EvictCourseAsset` | — |
| `courses:view` → `{courseIdN}`, `class=staff` | 10 min | `CachedCourseQueryService.GetCourseView` (staff tree) (`:94-100`) | `EvictCourse` only (the tree reads just `r.CourseAssetId`, never the asset table — G7) | — |
| `courses:view` → `{courseIdN}`, `class=public` | 10 min | `CachedCourseQueryService.GetCourseView` (public summary) (`:77-86`) | `EvictCourse`, `EvictCourseOverview`, `EvictCourseAsset` + the subject-write tag flush | **`courses:views:{tenantN}`** (`:86`) — the only tagged entry |
| `courses:overview` → `{courseIdN}` | 10 min | `CachedCourseQueryService.GetCourseOverview` (`:122-128`), asserted via `ETag` | `EvictCourse`, `EvictCourseOverview` (overview content is not asset-fed, so the asset row omits it) | — |
| `courses:readiness` → `{courseIdN}` | 3 min | `CachedCourseQueryService.GetCourseReadiness` (`:108-114`) | `EvictCourse`, `EvictCourseOverview`, **and** `EvictCourseAsset` — the only key present in all three rows (plans/counts, cover, asset durations all feed readiness) | — |
| `courses:status` → `{courseIdN}` | — n/a | **Never cached** — `GetCourseStatusAsync` is uncached (visibility masking must not widen) | `EvictCourse` only — a defensive no-op (eviction-only key) | — |
| `courses:subjects` | 15 min | `CachedSubjectQueryService.ListSubjects` (`:22-28`), default page only | `EvictSubject` | — |
| `entity:course` → `{courseIdN}` | 5 min | **Never read** — aggregate `Course.GetById` is uncached (G12: tracked-identity bridge) | `EvictCourse` | — |
| `entity:overview` → `{courseIdN}` | 5 min | `CachingCourseOverviewRepositoryDecorator.GetByCourseIdAsync` (`:58-71`) — the **only** repository read-through (G11) | `EvictCourseOverview` (every overview write — this is what keeps the upsert handler's add-or-update decision never stale) | — |
| `entity:asset` → `{assetIdN}` | 5 min | **Never read** — asset reads are write-preceding loads (G12) | `EvictCourseAsset` (keyed by **asset** id) | — |
| `entity:subject` → `{subjectIdN}` | 5 min | **Never read** — subject reads are write-preceding loads (G12) | `EvictSubject` | — |

Notes:

- Bypass rules on every Courses read: no tenant, non-default paging/search/sort/subject filter
  (or `subjectId` on the staff list) → DB directly; callers' values never enter a key.
- The split `class=staff` / `class=public` siblings keep visibility classes from ever sharing an
  entry; the public entry is enrollment-free by construction.

### 1.2 Tags — exactly one family

| Tag | Value | Carried by (read entries) | Queued by (writes) | Effect |
|---|---|---|---|---|
| `courses:views` | `{tenantN}` (`CourseCacheTags.CoursePublicViews`) | `courses:view:…:class=public` only (`CachedCourseQueryService.cs:86`) | `CachingSubjectRepositoryDecorator` (`:76`) — the only `EvictTag` call site in Courses | A subject rename/delete invalidates **every** public view in the tenant in one `RemoveByTagAsync` (G4.1: not attributable to a single course) |

Tag semantics + staleness (G4.1/G4.2): public views are invalidated immediately by the tag; their
worst-case staleness equals their TTL (**10 min**). The two 2-min catalogs are deliberately NOT on
the tag — after a subject rename they may serve a stale subject name for **≤2 min** (accepted,
because the rare rename vs. the hot catalogs weigh that way); the subject list key itself flushes
with the write. Never add a second Courses tag family without a new design decision.

### 1.3 Write-side key-set helpers (the eviction matrix)

All helpers live in `CourseCacheKeys.cs:93-144` and derive from the aggregate argument only
(no lookups, no change-tracker reads). Queue lives on the request-scoped `ScopedEvictionProvider`
(two `HashSet` lanes under a lock); flush happens at EF save/commit points via
`EvictionSaveChangesInterceptor` / `EvictionTransactionInterceptor` in `UnitOfWork.cs`.

| Helper (file:lines) | Triggered by | Keys queued | Tag |
|---|---|---|---|
| `EvictCourse(scope, courseId)` (`:93-103`) | `ICourseRepository.Add/Update/Remove` — create/update/soft-delete/status/reorder, and every section/item/plan/resource write that re-commits the loaded root (incl. `AddSection.cs:41`) | `catalog:staff`, `catalog:student`, `view:staff`, `view:public`, `overview`, `readiness`, `status`, `entity:course` (8) | — |
| `EvictCourseOverview(scope, courseId)` (`:109-117`) | `ICourseOverviewRepository.Add/Update/Remove` — `UpsertCourseOverviewCommand` | `overview`, `readiness`, `view:public`, `catalog:staff`, `catalog:student`, `entity:overview` (6) | — |
| `EvictCourseAsset(scope, courseId, assetId)` (`:123-136`) | `IRepository<CourseAsset>` writes (any TPH subtype; duration sync, transcode-state flips) | `readiness`, `view:public`, `catalog:staff`, `catalog:student`, `entity:asset` (5); `[]` when `courseId == Guid.Empty` (unassigned pool asset queues nothing — G2) | — |
| `EvictSubject(scope, subjectId)` (`:140-144`) | `ISubjectRepository.Add/Update/Remove` | `courses:subjects`, `entity:subject` (2) | **+ `courses:views`** (`CachingSubjectRepositoryDecorator.cs:76`) |

---

## 2. Identity module — the global tree

**Module:** `src/alphazero-api/Modules/Identity` · **Style: synchronous repository eviction**
**Registry:** `Infrastructure/Repositories/IdentityCacheKeys.cs` — deliberately global keys
(device ids and tenant-user ids are globally unique primary keys; condition definitions are
platform-wide rows with no tenant column).

```
(global — no tenant segment)
├── device_pubkey:{deviceIdN}             (24 h)   signed-request middleware path
├── user_assignments:{tenantUserId}       (30 min) authorization path
└── conditions:definition:{name}          (10 min) condition evaluation
```

| Key | Read by | Written/evicted by | Notes |
|---|---|---|---|
| `device_pubkey:{deviceIdN}` (24 h) | `PublicKeyProvider` (`:35-36`) — `GetOrCreateAsync` on every signed request | `SetNewDevicePublicKey` → `SetAsync` (`:48-49`) | No explicit eviction; the 24-h TTL bounds device-key rotation staleness |
| `user_assignments:{tenantUserId}` (30 min) | `CachingTenantUserPrincipalAssignmentRepository.GetAllAssignmentsEager` (`:32-38`) — JSON-safe projection, rehydrated into aggregates on every read | Synchronous `Evict` in the same repository's `Add`/`Remove` (`:57`, `:63`); also `RemoveAsync` in `SetNewDevicePublicKey` (`:57` — `MainDeviceId` is part of the visible assignment view) | Synchronous so a read later in the same request is fresh |
| `conditions:definition:{name}` (10 min) | `ConditionRepository` (`Infrastructure/Models/ConditionDefinition.cs:36-37`) — converter-produced JSON string (polymorphic `IConditionNode`) | **No eviction site** — TTL expiry only (10 min). Conditions are seeded, near-immutable rows; revisit this row if they ever gain a write path | Reader-maintained; the `Reference` case lives in `ConditionNodeJsonConverter` |

---

## 3. Tenants module — branding family

**Module:** `src/alphazero-api/Modules/Tenants` · **Style: consumer-driven removal**

```
(global — no tenant segment)
└── tenant:subdomain:{normalizedSubdomain}   + tag tenant-branding:{normalizedSubdomain}
```

| Key / tag | Read by | Removed by | Notes |
|---|---|---|---|
| `tenant:subdomain:{normalizedSubdomain}` + tag `tenant-branding:{normalizedSubdomain}` | **No reader in `src/` today** — the subdomain-lookup writer/reader is not present at the moment; this pair is *eviction-only* | `TenantBrandingUpdatedConsumer` (`:44-48`) — `RemoveAsync(key)` **and** `RemoveByTagAsync(tag)` for both the MediatR notification and the bus message | Cache dependency is optional in the consumer (older test compositions work without it); failures logged/swallowed, `OperationCanceledException` rethrown. If you reintroduce a branding lookup, cache it under exactly these shapes |

---

## 4. VideoUploading module — SSM family

**Module:** `src/alphazero-api/Modules/VideoUploading` · **Style: TTL-only**

```
(global — no tenant segment)
└── ssm:master-clear-key                   (10 min)
```

| Key | Read by | Evicted by | Notes |
|---|---|---|---|
| `ssm:master-clear-key` (10 min) | `DefaultVideoEncryptionService.GetClearKeyAsync` (`Infrastructure/Services/DefaultVideoEncryptionService.cs:27-50`) — `GetOrCreateAsync` wrapping the SSM `GetParameterAsync` call | None — TTL-only | Removes a Systems-Manager round trip from every playback-ticket/encryption path; the 10-min TTL bounds master-key rotation delay |

---

## 5. Cross-cutting summary

**Eviction styles at a glance:**

- Courses: queued key sets → flush at EF save/commit (interceptors in `UnitOfWork.cs`); failure
  semantics per `docs/caching/README.md` §3 (standalone-failure → discard, rollback → discard,
  ambiguous commit-path failure → flush, ambient `System.Transactions` → throw, cache outage →
  logged/swallowed).
- Identity: synchronous `Evict`/`RemoveAsync` at write sites.
- Tenants: consumer removes key + tag on the update event.
- VideoUploading + Identity device keys + condition definitions: TTL is the only invalidator.

**Tag inventory: exactly two tag strings exist in the entire platform**

| Tag string | Module | Granularity | Carried by | Queued by |
|---|---|---|---|---|
| `courses:views:{tenantN}` | Courses | tenant-wide public-view family | public-view read entry only | subject write decorator only |
| `tenant-branding:{normalizedSubdomain}` | Tenants | per-subdomain | (no reader today) | — (consumer removes with the key) |

(Identity and VideoUploading use no tags; they have small, enumerable key spaces.)

**Staleness bounds when eviction fails or doesn't apply:** every entry dies by its TTL —
2 min (catalogs) / 3 min (readiness) / 5 min (entity keys) / 10 min (views, overview, SSM key,
conditions) / 15 min (subjects) / 24 h (device keys) / 30 min (assignments). Cross-instance
staleness is the same TTL bound (single-instance in-process eviction today; tracked in `TODOS.md`).

**Where the machinery lives:**

| Concern | File |
|---|---|
| Key grammar + tenant binding | `AlphaZero.Shared/Infrastructure/Caching/{CacheKey,CacheScope,CachePolicy}.cs` |
| Cache adapter (only `HybridCache` reference) | `AlphaZero.Shared/Infrastructure/Caching/{ICachingService,HybridCachingService}.cs` |
| Queue contract + lanes | `AlphaZero.Shared/Infrastructure/Caching/{IEvictionProvider,ScopedEvictionProvider}.cs` |
| Flush points + interception | `AlphaZero.Shared/Infrastructure/UnitOfWork.cs` (both interceptors) |
| Ambient-`System.Transactions` guard + `Enlist=false` | `EvictionSaveChangesInterceptor` (ambient throw) + `DatabaseSettings.cs:26-28` |
| Courses registries | `Modules/Courses/Infrastructure/Caching/{CourseCacheKeys,CourseCacheTags}.cs` |
| Courses read/write decorators | `Modules/Courses/Infrastructure/Caching/{CachedCourseQueryService,CachedSubjectQueryService,Caching*RepositoryDecorator}.cs` |
| Identity registry + decorators | `Modules/Identity/Infrastructure/Repositories/{IdentityCacheKeys,CachingTenantUserPrincipalAssignmentRepository,PublicKeyProvider}.cs`, `Infrastructure/Models/ConditionDefinition.cs` |
| Tenants / VideoUploading sites | `Tenants/Infrastructure/Consumers/TenantBrandingUpdatedConsumer.cs`, `VideoUploading/Infrastructure/Services/DefaultVideoEncryptionService.cs` |
