# Project TODOs

## [Architecture / Identity] Distributed Cache Invalidation for Principal Assignments
- **What:** Replace in-process `IMemoryCache` in `CachingTenantUserPrincipalAssignmentRepository` with `HybridCache` and distributed invalidation.
- **Why:** `CachingTenantUserPrincipalAssignmentRepository` caches assignments in local memory for 30 minutes. When background MassTransit consumers assign or revoke roles on background worker processes, the API web server processes retain stale assignments until local cache TTL expires.
- **Context:** `AlphaZero.Shared` already configures `services.AddHybridCache()`. Migrating `CachingTenantUserPrincipalAssignmentRepository` ensures L1 in-memory speed combined with L2 Redis cache and bus-driven cache eviction across all nodes.
- **Depends on / blocked by:** Core IAM sub-resource and capability ticketing fixes.
- **Priority:** P2
