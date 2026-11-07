# HANDOFF — Access-Code Provisioning Refactor (Library ⇄ Courses)

**Repo:** `/home/azero/Desktop/AlphaZeroLearningAcademy`
**Branch:** `library/p4-analytics`
**Working tree:** clean — everything below is committed.
**Head commits (oldest → newest):**

```
230b67f9 refactor(library,courses): bulk VoidBatch and move provisioning flow to Application
117f6f9d feat(courses,library): metadata-driven strict plan selection for access-code provisioning
6edc499a refactor(courses): drop redundant SaveChangesAsync from IFailedProvisioningRecordRepository
```

## What this workstream was

Three related refactors of the access-code lifecycle end-to-end:

1. **Bulk VoidBatch** — `VoidBatchCommandHandler` (Library) no longer persists voiding one code at a time. It keeps the per-code `Void()` loop purely for domain events, then persists with a single set-based repository update.
2. **Provisioning flow moved to Application layer** — `AccessCodeActivatedConsumer` (Courses) was a 300+-line god consumer doing tenant lookup, raw EF/SQL, upserts, plan resolution and enrollment. It is now a ~68-line tenant-resolving adapter that dispatches one Application-layer command: `ProvisionAccessCodeCommand`.
3. **Metadata-driven strict plan selection** — a redeemed access code can now target a specific plan on a multi-plan course via `AccessCode.Metadata` `{ "plan": "<planId-guid>" }`. Selection is strict: unknown references and ambiguous (multi-plan, no reference) codes are **parked, never guessed**.

## Current architecture (as committed)

### VoidBatch (Library)
- `IAccessCodeRepository.VoidBatch(tenantId, batchId, sourceCodeId, reason)` — set-based `ExecuteUpdateAsync` (`Status=Voided`, `VoidedReason` set, excludes terminal `Voided/Expired`).
- Handler: per-code `Void()` loop for domain events → `VoidBatch(...)` only if `voidedCount > 0` → reports rows affected.
- Non-terminal statuses: `Minted(1), Distributed(2), Redeemed(3), Sold(5)`; terminal: `Voided(4), Expired(6)`.

### Provisioning flow (Courses)
- `ProvisionAccessCodeCommand : ICommand<Success>, ITransactionalCommand` — in `src/alphazero-api/Modules/Courses/Application/Enrollments/Commands/ProvisionAccessCode/ProvisionAccessCode.cs`.
  - **Always returns `Success`** — a parked failure is a business outcome, not an error, so the UnitOfWork transaction commits the park record.
  - Steps: ARN parse (`ResourceArn.ExtractCourseId`) → idempotency check on enrollment → course lookup → **plan selection via `CoursePlanSelector`** → `EnsurePlanPrincipalAsync(courseId, planId, tenantId)` via `ICoursePlanPrincipalService` (MassTransit request/response to Identity; principal is deterministic from (course, plan, tenant) — never stored on the ARN or the code) → nested `EnrollInCourseCommand` via `ISender` (same MediatR scope) → `MarkProvisionedIfExistsAsync`.
  - Command signature: `(Guid AccessCodeId, Guid RedeemingUserId, ResourceArn? TargetResourceArn, Guid TenantId, JsonDocument? Metadata = null)` — Metadata is nullable so ARN-only legacy codes still work.
- `AccessCodeActivatedConsumer` (`src/alphazero-api/Modules/Courses/Infrastructure/Consumers/AccessCodeActivatedConsumer.cs`) resolves tenant (falls back from event since Library publishes empty tenant), then sends the command via `_coursesModule.Send(...)` (AppModule resolves `IMediator` from `CreateScope()`).
- Park/retry contract: `IFailedProvisioningRecordRepository` with tenant-scoped `GetByTenantAndAccessCodeAsync`, atomic `ParkFailureAsync` (ON CONFLICT upsert raw SQL; plain add in InMemory), `MarkProvisionedIfExistsAsync`. Repository, not DbContext, owns this. No `SaveChangesAsync` on the interface — persistence is transactional via the pipeline; keep it that way.

### Plan selection (Courses Domain service)
- `CoursePlanSelector` at `src/alphazero-api/Modules/Courses/Domain/Services/CoursePlanSelector.cs`, registered in Courses `DependencyInjection.cs` alongside other domain services.
- `SelectPlan(course, JsonDocument? metadata, ResourceArn? arn)` precedence:
  1. metadata `"plan"` (also `"planId"` alias) GUID string → strict lookup on `course.Plans`
  2. legacy `/plan/{id}` ARN segment (parsed **directly** — do NOT use `ResourceArn.GetResource("plan")`; it has no "plan" mapping and throws)
  3. no reference: single-plan course → that plan; no plans → park `CoursePlan.NotFound`; multi-plan → park `CoursePlan.Ambiguous`
- Unknown/malformed/malformatted references → `CoursePlan.NotFound` (parsed, never guessed via name fallback or `.First()`).

### Metadata propagation chain (Library → Courses)
- `AccessCode.Metadata` (`JsonDocument`, already persisted by `GenerateBatch`) →
- `AccessCodeRedeemedDomainEvent` gained `JsonDocument? Metadata` (constructor clones via `JsonDocument.Parse(root.GetRawText())` so the aggregate's doc isn't disposed mid-event; both `Redeem` call sites in the state machine pass `code.Metadata`) →
- `AccessCodeActivatedDomainEventHandler` forwards it →
- `AccessCodeActivatedIntegrationEvent` gained optional `JsonDocument? Metadata = null` →
- consumer → `ProvisionAccessCodeCommand`.
- Generation side needs no change: `GenerateBatch` already persists the metadata dictionary (`{ "plan": "<guid>" }` is just data).
- `TargetResourceArn` stays course-only; the `"plan"` metadata key is the per-resource-type provisioning section future resource types can mirror with their own keys.

## Test status (all green at commit 6edc499a)

- `tests/Modules/Courses/Courses.UnitTests` — **439 passed**
- `tests/Modules/Library/Library.UnitTests` — **75 passed**
- Coverage includes: 8 `CoursePlanSelectorTests` (metadata hit, legacy ARN, metadata-vs-ARN precedence, single-plan inference, ambiguous, unknown plan, malformed metadata, no plans), handler tests for metadata-selected plan / ambiguous park / unknown-plan park / principal-failure park, consumer delegation test, and Library metadata-forwarding test in `AccessCodeActivatedDomainEventHandlerTests`.
- **Known pre-existing breakage (NOT this workstream):** `tests/Modules/VideoUploading/VideoUploading.Tests.Integration` has 5 build errors (`CreatePlaybackSessionHandlerUnitTests`, `WatermarkContextDto.UserPhone`) that predate this session. Full-solution builds will show them.

## Verify with

```bash
dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj --nologo -v q
dotnet test tests/Modules/Library/Library.UnitTests/Library.UnitTests.csproj --nologo -v q
```
(Build `src/alphazero-api/AlphaZero.API/AlphaZero.API.csproj` to compile everything except the broken VideoUploading integration tests.)

## Gotchas for anyone continuing

1. **`ResourceArn.GetResource("plan")` throws** — there is no "plan" key in the dispatcher; the selector splits `ResourcePath` itself. If you add a shared `GetPlan()` helper, refactor the selector to use it.
2. **JsonDocument disposal** — event/command metadata is *cloned* (`Parse(GetRawText())`). Keep that pattern if you add fields; don't pass owned docs through long-lived containers.
3. **Park commit semantics** — `ParkFailureAsync` runs inside the command's EF transaction (TransactionalBehavior). Never make the command return an error on park; the park record must commit.
4. **Tenant isolation** — failed-provisioning lookups must stay tenant-scoped (`GetByTenantAndAccessCodeAsync`); tenant query filters apply on both read and write paths.
5. **MediatR auto-registration** — new Courses handlers register automatically via `AddMediatR(AddMediatR... typeof(ICoursesApplicationMarker).Assembly)`; new domain services need explicit DI entries in Courses `DependencyInjection.cs` (that's where `CoursePlanSelector` was registered).
6. **Consumer events in tests** — consumer/consumer tests construct `AccessCodeActivatedIntegrationEvent` without metadata (optional param), so legacy callers keep compiling.

## Possible next steps (none required by the current plan)

- Generator/UI option to stamp `{ "plan": "<planId>" }` metadata when minting batches (`GenerateBatch` already accepts it — this is just UX/API surfacing).
- Optional cleanup: verify whether `AccessCodeRedeemedDomainEvent.Payload` (audit JSON) should also embed the selected plan for forensics.
- The pre-existing VideoUploading integration-test build errors (out of scope for this handoff's owner, but they block full-solution `dotnet build`).
- CI: none was run this session beyond the above two unit suites — run integration suites if your pipeline depends on them.
