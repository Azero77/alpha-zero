# Project TODOs

## [Architecture / Identity] Distributed Cache Invalidation for Principal Assignments
- **What:** Replace in-process `IMemoryCache` in `CachingTenantUserPrincipalAssignmentRepository` with `HybridCache` and distributed invalidation.
- **Why:** `CachingTenantUserPrincipalAssignmentRepository` caches assignments in local memory for 30 minutes. When background MassTransit consumers assign or revoke roles on background worker processes, the API web server processes retain stale assignments until local cache TTL expires.
- **Context:** `AlphaZero.Shared` already configures `services.AddHybridCache()`. Migrating `CachingTenantUserPrincipalAssignmentRepository` ensures L1 in-memory speed combined with L2 Redis cache and bus-driven cache eviction across all nodes.
- **Depends on / blocked by:** Core IAM sub-resource and capability ticketing fixes.
- **Priority:** P2

## [Performance / Courses] Catalog predicate index check (from /plan-eng-review D10, 2026-10-21)
- **What:** Measure the student-catalog query plan (`GET /courses/catalog`: `Courses.Status = Published` + leading-wildcard ILIKE on title/description) with `EXPLAIN ANALYZE`, then, only if scanning is confirmed at realistic volume, add a `(TenantId, Status)` index (and consider `pg_trgm` GIN for search).
- **Why:** Only `(TenantId)` and soft-delete indexes exist today (enrollment: `EnrollmentConfiguration`); add nothing unmeasured.
- **Pros:** protects the headline student surface from a full-scan cliff; evidence-driven.
- **Cons:** schema/migration churn if added prematurely; initial probe cost.
- **Context:** Start from `src/alphazero-api/Modules/Courses/Infrastructure/Queries/CourseQueryService.cs` `ListCourseCatalogAsync` (predicate at :95-104). Run EXPLAIN on a data-shaped DB, not empty fixtures. Decision record: D10 (L9, approval to add gated on measurement).
- **Depends on / blocked by:** realistic data volume in any environment (probe needs rows).
- **Priority:** P3

## [Architecture / Caching] Distributed cache invalidation across instances (from /plan-eng-review D6, 2026-10-24)
- **What:** Register a distributed L2 cache (Redis) for `HybridCache` and make tag eviction propagate across API instances.
- **Why:** Only the in-process L1 cache is registered, so eviction is per-process. With more than one API instance, the others keep serving stale entries until their TTL expires (2–15 minutes for the Courses surfaces).
- **Pros:** removes the scale-out staleness limit; lets bus-driven invalidation reach every node.
- **Cons:** new infrastructure and configuration; Redis sizing and operational cost; no correctness change on a single server.
- **Context:** `AlphaZero.Shared/Infrastructure/DependencyInjection.cs:68-78` registers HybridCache without a distributed cache. `docs/CachingArchitecture.md` states the deployment is single-server, and issue #33 deferred production sizing. Start by registering `AddStackExchangeRedisCache` and verifying `RemoveByTagAsync` propagation across two instances.
- **Depends on / blocked by:** a decision to run more than one API instance.
- **Priority:** P2
