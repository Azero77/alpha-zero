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
