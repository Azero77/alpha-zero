# 🏗️ Engineering Plan: Redemption Audit Logs

> **Branch:** `main` | **Module:** `Library` | **Reviewer:** plan-eng-review

---

## Feature Summary

Create a searchable, immutable log of all code redemptions filterable by `LibraryId` and `TenantId` so school managers can verify that library partners are not stealing or misusing codes.

**PRD anchor:** §3 Offline-First Economy — "Immutable logs for all code redemptions" | §4 Library Accountant role — "Audits library financials."

---

## Architecture Decision

### D1 — Where does `RedemptionAuditLog` live?

**Decision: New standalone entity in the Library module** (`Domain/RedemptionAuditLog.cs`).

Rationale:
- `AccessCode` already carries `RedeemedAt` / `RedeemedByUserId`. A separate entity gives **append-only immutability** by design (no `Update` path through the UoW).
- It mirrors the pattern the codebase already follows: separate entities per aggregate responsibility (see `Library.cs` vs `AccessCode.cs`).
- No cross-module DB join; querying stays within `Library.AppDbContext`.

---

## Data Model

### New entity: `RedemptionAuditLog`

```csharp
// Domain/RedemptionAuditLog.cs
public class RedemptionAuditLog : Entity
{
    public Guid TenantId          { get; private set; }
    public Guid? LibraryId        { get; private set; }   // null = admin code
    public Guid AccessCodeId      { get; private set; }
    public Guid RedeemedByUserId  { get; private set; }
    public string StrategyId      { get; private set; }   // denormalized for query speed
    public ResourceArn TargetResourceArn { get; private set; }
    public DateTime RedeemedAt    { get; private set; }
    public string? IpAddress      { get; private set; }   // optional enrichment
    public string? DeviceFingerprint { get; private set; }// optional enrichment

    private RedemptionAuditLog() { }

    public static RedemptionAuditLog Record(
        Guid tenantId,
        Guid? libraryId,
        Guid accessCodeId,
        Guid redeemedByUserId,
        string strategyId,
        ResourceArn targetResourceArn,
        DateTime redeemedAt,
        string? ipAddress = null,
        string? deviceFingerprint = null) => new() { ... };
}
```

**Key design choices:**
- `Entity` base (not `AggregateRoot`): audit logs have no domain events, no mutation.
- `LibraryId` is nullable to handle admin-minted codes (`GenerateAdminCode` path).
- Denormalize `StrategyId` and `TargetResourceArn` — avoids joining back to `AccessCode` at query time.
- Optional `IpAddress` / `DeviceFingerprint` fields: already in the request context (device enforcement system). Useful for fraud detection but not required for MVP.

### EF Configuration (`Configurations/RedemptionAuditLogConfiguration.cs`)

```csharp
builder.HasKey(x => x.Id);
builder.Property(x => x.Id).ValueGeneratedNever();
builder.Property(x => x.TenantId).IsRequired();
builder.Property(x => x.AccessCodeId).IsRequired();
builder.Property(x => x.RedeemedByUserId).IsRequired();
builder.Property(x => x.StrategyId).IsRequired().HasMaxLength(64);
builder.Property(x => x.TargetResourceArn)
    .HasConversion(v => v.Value, v => ResourceArn.Create(v).Value)
    .IsRequired().HasMaxLength(512);
builder.Property(x => x.IpAddress).HasMaxLength(64);
builder.Property(x => x.DeviceFingerprint).HasMaxLength(256);

// Composite index for the primary query pattern
builder.HasIndex(x => new { x.TenantId, x.LibraryId, x.RedeemedAt });
builder.HasIndex(x => x.AccessCodeId).IsUnique(); // one log per redemption
```

> [!IMPORTANT]
> The `HasIndex(AccessCodeId).IsUnique()` constraint enforces true immutability at DB level: one redemption event, one log row, forever.

---

## Application Layer

### New Query: `GetRedemptionLogsQuery`

```
Application/
  RedemptionAuditLogs/
    GetRedemptionLogs/
      GetRedemptionLogsQuery.cs
      GetRedemptionLogsQueryHandler.cs
      GetRedemptionLogsQueryResponse.cs
```

**Query shape:**
```csharp
public record GetRedemptionLogsQuery(
    Guid? LibraryId,        // null = all libraries in tenant
    DateOnly? From,
    DateOnly? To,
    int Page = 1,
    int PageSize = 50
) : IRequest<ErrorOr<PagedResult<RedemptionAuditLogDto>>>;
```

**Handler:**
- Queries `IRedemptionAuditLogRepository.GetPagedAsync(...)`.
- Tenant scope is **automatic** via `ITenantProvider` + global EF filter — handler does **not** need to filter by TenantId manually.
- LibraryId filter: optional `WHERE LibraryId = @id` predicate.
- Date range filter on `RedeemedAt`.
- Returns paginated DTO (never the entity).

### Domain Repository Interface

```csharp
// Domain/IRedemptionAuditLogRepository.cs
public interface IRedemptionAuditLogRepository
{
    Task AddAsync(RedemptionAuditLog log, CancellationToken ct = default);
    Task<PagedResult<RedemptionAuditLog>> GetPagedAsync(
        Guid? libraryId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
```

> [!NOTE]
> `AddAsync` is the **only** mutation. No `Update`, no `Delete`. Immutability enforced by interface design.

---

## Write Path: Hooking Into Redemption

**Two options — decided here:**

### ✅ Option A (chosen): Write log inside `RedeemCodeCommandHandler` (synchronous, same UoW)

```csharp
// After accessCode.Redeem(currentUser.UserId) succeeds:
var log = RedemptionAuditLog.Record(
    accessCode.TenantId,
    accessCode.LibraryId,
    accessCode.Id,
    currentUser.UserId,
    accessCode.StrategyId,
    accessCode.TargetResourceArn,
    accessCode.RedeemedAt!.Value);

await _auditLogRepository.AddAsync(log, cancellationToken);
```

**Why not Option B (MassTransit in-memory event)?** 
- The log is mission-critical: a redemption without a log is unacceptable to the Library Accountant.
- An in-memory event (MassTransit mediator) is still synchronous in-process, but introduces a consumer indirection for no reliability gain in this case. 
- The existing `UnitOfWorkDecoratorCommandHandler` behaviour wraps the entire `Handle()` call in one transaction — log write is automatically atomic with the `AccessCode` update. If either fails, both roll back.

---

## Read Path: Endpoint

```
Presentation/Endpoints/RedemptionAuditLogs/
  GetRedemptionLogsEndpoint.cs
```

**Route:** `GET /api/libraries/{libraryId}/audit-logs`
**Also:** `GET /api/libraries/audit-logs` (tenant-wide, LibraryId omitted → all)

**Authorization:** `LibraryAccountant` + `LibraryManager` + `TenantAdmin` roles.

**Query parameters:**
| Param | Type | Description |
|---|---|---|
| `libraryId` | `Guid?` | From route or query |
| `from` | `DateOnly?` | Inclusive start date |
| `to` | `DateOnly?` | Inclusive end date |
| `page` | `int` | Default 1 |
| `pageSize` | `int` | Default 50, max 200 |

**Response DTO:**
```json
{
  "items": [
    {
      "id": "uuid",
      "accessCodeId": "uuid",
      "libraryId": "uuid or null",
      "redeemedByUserId": "uuid",
      "strategyId": "course-enrollment",
      "targetResourceArn": "arn:az:course:...",
      "redeemedAt": "2026-06-10T16:00:00Z"
    }
  ],
  "totalCount": 1200,
  "page": 1,
  "pageSize": 50
}
```

---

## Infrastructure

### Repository Implementation

```csharp
// Infrastructure/Repositories/RedemptionAuditLogRepository.cs
public class RedemptionAuditLogRepository : IRedemptionAuditLogRepository
{
    private readonly AppDbContext _context;

    public async Task AddAsync(RedemptionAuditLog log, CancellationToken ct)
        => await _context.RedemptionAuditLogs.AddAsync(log, ct);

    public async Task<PagedResult<RedemptionAuditLog>> GetPagedAsync(
        Guid? libraryId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.RedemptionAuditLogs.AsNoTracking();

        if (libraryId.HasValue)
            query = query.Where(x => x.LibraryId == libraryId);

        if (from.HasValue)
            query = query.Where(x => x.RedeemedAt >= from.Value.ToDateTime(TimeOnly.MinValue));

        if (to.HasValue)
            query = query.Where(x => x.RedeemedAt <= to.Value.ToDateTime(TimeOnly.MaxValue));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.RedeemedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RedemptionAuditLog>(items, total, page, pageSize);
    }
}
```

### DbContext Update

Add to `AppDbContext.cs`:
```csharp
public DbSet<RedemptionAuditLog> RedemptionAuditLogs => Set<RedemptionAuditLog>();
```

### EF Migration

```bash
dotnet ef migrations add AddRedemptionAuditLog \
  --project src/Modules/Library/Infrastructure \
  --startup-project src/Api
```

### DI Registration

In `DependencyInjection.cs` → `AddLibraryPrivateInfrastructure`:
```csharp
services.AddScoped<IRedemptionAuditLogRepository, RedemptionAuditLogRepository>();
```

---

## File Changeset

| File | Action |
|---|---|
| `Domain/RedemptionAuditLog.cs` | ✨ New |
| `Domain/IRedemptionAuditLogRepository.cs` | ✨ New |
| `Application/RedemptionAuditLogs/GetRedemptionLogs/GetRedemptionLogsQuery.cs` | ✨ New |
| `Application/RedemptionAuditLogs/GetRedemptionLogs/GetRedemptionLogsQueryHandler.cs` | ✨ New |
| `Application/RedemptionAuditLogs/GetRedemptionLogs/GetRedemptionLogsQueryResponse.cs` | ✨ New |
| `Application/RedeemCode/RedeemCodeCommandHandler.cs` | ✏️ Modified |
| `Infrastructure/Repositories/RedemptionAuditLogRepository.cs` | ✨ New |
| `Infrastructure/Persistance/AppDbContext.cs` | ✏️ Modified |
| `Infrastructure/Persistance/Configurations/RedemptionAuditLogConfiguration.cs` | ✨ New |
| `Infrastructure/DependencyInjection.cs` | ✏️ Modified |
| `Infrastructure/Migrations/AddRedemptionAuditLog.cs` | ✨ New (generated) |
| `Presentation/Endpoints/RedemptionAuditLogs/GetRedemptionLogsEndpoint.cs` | ✨ New |

---

## Edge Cases & Guard Rails

| Case | Handling |
|---|---|
| Redemption fails mid-flight (strategy throws) | `UnitOfWorkDecorator` rolls back → no log written. Consistent. |
| Admin-minted code (no LibraryId) | `LibraryId = null` in log; query with `libraryId = null` returns admin codes. |
| Page size abuse | Clamp `pageSize` to max 200 in endpoint validation |
| Tenant isolation | EF global filter on `TenantId` auto-applies; query handler does not need to filter manually |
| Future: log tampering | DB-level: `RedemptionAuditLog` table has no EF `Update` path; consider `GRANT SELECT, INSERT` only on this table in prod |

---

## Tests Required

1. **Unit:** `RedemptionAuditLogTests` — `Record()` factory sets all fields correctly.
2. **Integration:** `RedeemCodeCommandHandlerTests` — after successful redemption, `RedemptionAuditLog` row exists in DB.
3. **Integration:** `GetRedemptionLogsQueryHandlerTests` — filters by LibraryId, date range, pagination.
4. **Integration:** Tenant isolation — log from Tenant A not visible when querying as Tenant B.

---

## Open Questions (for you to decide)

> **Q1 — Should we capture `IpAddress` / `DeviceFingerprint` in the audit log?**
>
> These are available in the HTTP context and the device enforcement system already resolves them. Useful for fraud investigations but requires minor plumbing to pass them through the command.

> **Q2 — Who can see tenant-wide logs (no LibraryId filter)?**
>
> Proposal: `TenantAdmin` + `LibraryAccountant`. `LibraryManager` sees only their own `LibraryId`. Is this aligned with your Roles.md intent?

> **Q3 — Export to CSV?**
>
> PRD doesn't call for it but the Library Accountant use case strongly implies it. Scope for this iteration or defer?
