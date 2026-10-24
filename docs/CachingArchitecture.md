# Caching Architecture

This document describes the platform-wide caching abstraction and how each module uses it.

## Design philosophy

The application targets a **single-server deployment**. External distributed caches (Redis) would
add serialization overhead, network latency and operational complexity without a multi-instance
benefit. Caching is therefore a purely in-process L1 cache backed by ASP.NET Core `HybridCache`.
There is no Redis registration anywhere in the API.

Cross-instance invalidation is a known limitation: with more than one API instance, an eviction
on one instance does not reach the others, so their L1 entries stay stale until the TTL expires.
Moving to a distributed L2 / backplane is tracked in `TODOS.md`.

## The shared abstraction

`AlphaZero.Shared/Infrastructure/Caching` owns the only reference to
`Microsoft.Extensions.Caching.Hybrid` in the solution. Modules depend on:

- `ICachingService` — `GetOrCreateAsync`, `SetAsync`, `RemoveAsync`, `RemoveByTagAsync`.
- `CacheKey` / `CacheTag` — typed identifiers built with `Create(family, parts…)`; families are
  developer-authored and may contain `:` for grouping, while parts may not.
- `CachePolicy` — the entry TTL, supplied per call site.
- `CacheScope` — binds an operation to a tenant and prepends its `N`-format id to every key and
  tag. A scope with no tenant is disabled; call sites must bypass the cache rather than write a
  placeholder tenant.

`HybridCachingService` translates these types to `HybridCache` calls. L1 and L2 expirations are
both set from `CachePolicy`, so a later distributed L2, if ever added, inherits the same TTLs.
`HybridCache` serialises values even for L1, so cached payloads must be JSON-shaped: modules cache
DTOs/records and rehydrate aggregates on read, rather than caching domain entities directly.

`AddSharedInfrastructure` configures a defensive default through `AddHybridCache` (10-minute
absolute / 5-minute local). Every call site passes an explicit `CachePolicy`; the platform default
only guards code that forgets one.

## Tenant identity outside HTTP requests

Message consumers run with no `HttpContext`, so tenant-filtered EF queries would otherwise return
nothing. The platform provides:

- `IAmbientTenantContext` (singleton, `AsyncLocal`-backed) with `Push(tenantId)` and restore on
  dispose. `HttpTenantProvider` consults the ambient value after the `tid` claim and
  `X-TenantId` header, so HTTP precedence is unchanged.
- `TenantConsumeFilter<T>` — restores the tenant for a consumed message from its public
  `TenantId` property or the `TenantId` envelope header.
- `TenantPublishFilter<T>` — stamps the ambient tenant onto published envelopes.

The video lifecycle integration events and `VideoMetadataUpdatedDomainEvent` carry `TenantId`
explicitly so asset-driven invalidation works from bus scopes.

## Courses module

Courses uses tenant-scoped, typed keys with default-shape-only caching and a
persistence-driven eviction pipeline. See
[`Modules/Courses/CACHING.md`](../src/alphazero-api/Modules/Courses/CACHING.md) for the full
surface/TTL/tag matrix.

In short: `UnitOfWork.SaveChangesAsync` collects tags from the EF change tracker via the module's
`IEntityEvictionPolicy` before committing; the outermost MediatR behavior flushes them after the
commit. Commands remain cache-agnostic.

## Identity module: policy and assignment caching

Identity identifiers are globally unique primary keys, so these entries are intentionally global
(no tenant segment) — see `IdentityCacheKeys`.

### Principal assignments

- **Key:** `user_assignments:{tenantUserId}`.
- **TTL:** 30 minutes.
- **Shape:** a JSON-safe projection of `TenantUserPrincipalAssignment` plus its `Principal`
  (including managed/inline policies and condition JSON), rehydrated into domain entities on every
  read. Assignments are eagerly loaded with `.AsNoTracking()` and filtered in memory when a
  `resourceArn` is supplied.
- **Invalidation:** `Add`/`Remove` on the caching repository, and `SetNewDevicePublicKey`
  (because `MainDeviceId` is part of the cached assignment view), evict the user's key
  immediately.

### Device public keys

- **Key:** `device_pubkey:{deviceId:N}`.
- **TTL:** 24 hours.
- **Location:** written by `CachePublicKeyProvider.SetNewDevicePublicKey`, read on every signed
  request by the middleware path.

### Condition definitions

- **Key:** `conditions:definition:{name}`.
- **TTL:** 10 minutes.
- Condition rows are platform-wide and have no tenant column.
- `IConditionNode` is polymorphic, so the entry stores its converter-produced JSON and
  deserialises through the same options.

Identity no longer uses `IMemoryCache`; all of the above goes through `ICachingService`.

## Tenants module

`TenantBrandingUpdatedConsumer` invalidates the branding lookup when branding changes:

- **Key:** `tenant:subdomain:{normalizedSubdomain}`.
- **Tag:** `tenant-branding:{normalizedSubdomain}`.

The consumer runs for both the MediatR notification (same process) and the bus message (cross
process); the cache is optional in its constructor so older test compositions without caching
still work.

## VideoUploading module

The SSM master ClearKey is cached through `ICachingService`:

- **Key:** `ssm:master-clear-key`.
- **TTL:** 10 minutes.

This removes a Systems Manager call from every playback-ticket/encryption path while bounding
rotation delay to the TTL.

## Operational guidance

- TTLs are the upper bound on staleness for any path that fails to evict. Choose them per surface:
  catalogs are short (2 min), per-course reads longer (10 min), readiness shortest (3 min) because
  asset pipelines flip it.
- Eviction is best-effort after commit: a cache outage degrades freshness, never correctness.
  Cache exceptions during eviction are logged and swallowed; cancellation is not.
- Never build keys from caller-controlled paging, search or sort values. Cache only default
  request shapes and bypass otherwise.
