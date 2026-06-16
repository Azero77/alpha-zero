# Caching Architecture

This document outlines the caching architecture used within the AlphaZero Learning Academy, specifically focusing on the Identity module's authorization assignments.

## Design Philosophy
Our application targets a **Single-Server Deployment**. Because of this, introducing external distributed caches (like Redis) adds unnecessary complexity, serialization overhead (the "serialization tax"), and network latency. We prioritize a lean, high-performance, purely in-memory architecture.

We use ASP.NET Core's `IMemoryCache` for near-instantaneous `O(1)` access to frequently requested authorization objects, skipping database lookups entirely for hot paths.

## Identity Module: Policy & Assignment Caching

### Zero-DTO Architecture
We cache the native **Domain Entities** directly. We do not use intermediary Data Transfer Objects (DTOs) for caching. Since `IMemoryCache` lives in the same process as the application runtime, caching reference types stores a pointer to the memory location of the object.
- **Benefit:** Zero serialization cost.
- **Benefit:** Reduces code duplication (no manual mapping layer).
- **Benefit:** Provides the actual domain methods (like `IsRequestedPathContainedInResource()`) instantly on the cached instances.

### How it Works (`CachingTenantUserPrincipalAssignmentRepository`)
1. **Eager Loading:** When a user's assignments are requested, the application queries the database via Entity Framework Core using `.AsNoTracking()`. It eagerly loads all related records, but handles the `Principal` domain entities manually (because EF Core ignores the `Principal` type in our DbContext configuration to maintain pure domain models).
2. **The Cache Key:** Data is stored under `user_assignments:{TenantUserId}`.
3. **TTL (Time to Live):** Cached assignments expire after 30 minutes (`AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)`).
4. **In-Memory Filtering:** When the application needs to evaluate a specific `resourceArn`, it retrieves the full list of assignments for the user from `IMemoryCache` (which takes ~0.05ms) and runs the LINQ `.Where` and `.OrderByDescending` logic purely in memory.

### Cache Invalidation
Whenever a user's authorization footprint changes, their specific cache key is invalidated instantly via `_cache.Remove("user_assignments:{TenantUserId}")`. This guarantees Zero Downtime and No Stale Permissions.

This occurs in the following MediatR Command Handlers:
- `AssignPrincipalToUserCommandHandler`: When a new role or permission is assigned.
- `RemovePrincipalFromUserCommandHandler`: When a role or permission is revoked.
- `RegisterDeviceCommandHandler`: When a user registers a new device, their `MainDeviceId` changes. Because the `TenantUser` is nested inside the cached assignment, this invalidates the old cached `TenantUser` object to ensure device-locking authorization evaluates correctly against the new device fingerprint.

## Other Cached Items
### Device Public Keys
We cache the ECDSA public keys for registered devices for 24 hours. Because these are simple primitive types (strings/byte arrays) and are evaluated on every single request in the middleware, they are stored using ASP.NET Core's `HybridCache` to provide resilience and fast retrieval.

- **Cache Key:** `device_pubkey:{DeviceId}`
- **TTL:** 24 Hours
- **Location:** `RegisterDeviceCommandHandler` sets it; `DeviceSignatureVerifier` retrieves it.
