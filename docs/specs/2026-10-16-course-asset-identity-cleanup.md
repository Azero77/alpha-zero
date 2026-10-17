# Spec: Courses Module Identity Cleanup — Code Review Follow-Up

**Status:** Ready for implementation
**Branch:** `CourseContentManagement` (base commit `a47c6a16` "refactor Course Asset naming")
**Scope:** `src/alphazero-api/Modules/Courses/` (+ tests)
**You are an autonomous coding agent.** Implement every work item below in order. Each item lists exact files and the required end state. Do not touch modules other than `Courses` except where explicitly stated.

---

## 1. Context

The Courses module was recently refactored so that the **CourseAsset pool is the single source of truth** for media:

| Layer | Identity | Lives in |
|---|---|---|
| 1 — External | `ExternalResourceId` (VideoId / DocumentId / AssessmentId) | External modules (`VideoUploading`, `Documents`, `Assessments`); Courses only knows it via the `ResourceArn` stored on `CourseAsset` |
| 2 — Pool | `CourseAssetId` (`CourseAsset.Id`) | `Courses.CourseAssets` |
| 3 — Placement | `CurriculumResourceId` (`CurriculumResource.Id`) | `Courses.CurriculumResources`, referencing Layer 2 via `CourseAssetId` |

A code review of commit `a47c6a16` (two axes: repo standards + spec conformance) produced the findings below. The domain owner additionally ruled:

> **The resource-id → item lookup is obsolete.** The old codebase assumed **one resource per item**. The current model allows **many resources per item, and the same asset placed in many items across courses**. You cannot practically resolve "the item" from a resource id — the mapping is ambiguous by design. Therefore `AssessmentGradingCompletedConsumer` and `GetItemBitIndexByExternalResourceIdAsync` must be deleted, not fixed.

---

## 2. Work Items

### W1 (Breaking, do first): Delete the obsolete assessment-progress resolution path

**Rationale:** `AssessmentGradingCompletedIntegrationEvent` carries only `AssessmentId` (Layer 1). Under the many-resources-per-item model, resolving it to a `(CourseId, BitIndex)` is ambiguous. The student's client already knows which curriculum item it launched the assessment from; item completion flows through the existing client-driven `CompleteItem` endpoint (`Modules/Courses/Presentation/Enrollements/CompleteItem/CompleteItem.cs`). The consumer duplicates that path with a lookup that can no longer be made deterministically.

Delete:

1. `src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Assessments/AssessmentGradingCompletedConsumer.cs` — the entire file.
   - Consumers are auto-registered by assembly scan in `AlphaZero.API/Program.cs` (`x.AddConsumers(...)`); no registration edits are needed. The `assessment-grading-completed` MassTransit endpoint simply stops being configured.
2. `CourseRepository.GetItemBitIndexByExternalResourceIdAsync` (Infrastructure + its declaration in `ICourseRepository`).
3. `CourseRepository.GetItemBitIndexByCourseAssetIdAsync` (Infrastructure + interface) — its only remaining caller was #2.
4. `CourseRepository.GetCoursesByCourseAssetIdAsync` (Infrastructure + interface) — **zero callers** in the entire solution; speculative.

Then verify no dangling references: `grep -rn "GetItemBitIndexBy\|GetCoursesByCourseAssetIdAsync\|AssessmentGradingCompletedConsumer" src/alphazero-api/` must return only the `Assessments` module's own event definition and publisher (which stay — the event may be consumed elsewhere in the future).

**Do NOT touch** `Modules/Assessments/**` (event, publisher, domain handler).

### W2: Rename the fake-identity fields on `CurriculumResourceDto` (API contract change)

**File:** `src/alphazero-api/Modules/Courses/Application/Courses/Commands/Items/CourseSectionItemCommands.cs`

**Finding (flagged by both review axes):** the DTO still publishes the pool-asset id under external-identity names (`VideoId`, `DocumentId`, `AssessmentId`) — the exact "disguised Bug 1" the refactor was meant to eliminate.

Change the record's typed fields to a single honest one:

```csharp
public record CurriculumResourceDto(
    Guid Id,
    int Order,
    string ResourceType,
    Guid? CourseAssetId,          // Layer 2 — the pool asset id (single source of truth)
    double? DurationSeconds = null,
    string? DocumentTitle = null,
    string? DocumentType = null,
    bool? IsPublic = null,
    string? VariantName = null,
    string? DocUri = null,
    string? TextContent = null,
    int? PassingScore = null)
```

- Update `CurriculumResourceDto.From(...)` accordingly (drop the `VideoId:`/`DocumentId:`/`AssessmentId:` named arguments and their comments).
- `DocUri` already embeds `CourseAssetId`, so the document round-trip (`ParseDocUri`) stays consistent — no change there.
- **Consumers of the HTTP API lose `VideoId`/`DocumentId`/`AssessmentId` and gain `CourseAssetId`.** After the rename, run `grep -rni "videoId\|documentId\|assessmentId" src/alphazero-frontend/` and update any frontend usage of these DTO field names to `courseAssetId` (the playback-ticket endpoint already returns the true external `VideoId` at ticket time, so streaming flows are unaffected).

### W3: Remove the speculative query-service method

**Files:** `ICourseRepository.cs` (interface), `CourseRepository.cs` (impl)

`ICourseResourceQueryService.GetResourceDetailsByCourseAssetIdAsync` has no caller in the solution. Delete the method from both interface and implementation (it can be re-added when a real consumer appears).

### W4: Deduplicate the query body in `CourseResourceQueryService`

**File:** `src/alphazero-api/Modules/Courses/Infrastructure/Repositories/CourseRepository.cs`

After W3 only `GetResourceDetailsAsync` remains; but keep the shared shape honest: extract the `from course … select new CourseResourceDetailsDto(...)` query construction into a private helper that takes the resource filter predicate, so a future second lookup (if ever re-added) cannot drift:

```csharp
private IQueryable<CourseResourceDetailsDto> BuildResourceDetailsQuery(
    Guid courseId,
    System.Linq.Expressions.Expression<Func<CurriculumResource, bool>> resourceFilter)
{
    return from course in _context.Courses
           where course.Id == courseId
           from section in course.Sections
           from item in section.Items
           from resource in item.Resources
           where resourceFilter(resource) && resource.CourseAssetId != null
           join asset in _context.CourseAssets
               on resource.CourseAssetId equals asset.Id
           select new CourseResourceDetailsDto(
               resource.Id, item.Id, course.Id, asset.Id,
               asset.ResourceArn.ExtractResourceId() ?? Guid.Empty,
               resource.ResourceType);
}
```

`GetResourceDetailsAsync` becomes a thin call with `r => r.Id == curriculumResourceId`.

### W5: Playback handler — correct error code and constant alias

**File:** `src/alphazero-api/Modules/Courses/Application/Courses/Commands/Playback/IssuePlaybackTicket.cs`

1. A missing curriculum resource is a **NotFound**, not a Validation:

```csharp
if (details is null)
    return Error.NotFound("CurriculumResource.NotFound", "Curriculum resource not found in course.");
if (details.ResourceType != ResourceType.Video)
    return Error.Validation("CurriculumItem.NoVideo", "Curriculum resource does not contain a valid video.");
```

2. Delete the local `const int ticketTtlSeconds = TicketTtlSeconds;` alias and use `TicketTtlSeconds` directly everywhere (the `ttl` TimeSpan construction and the result).

3. Update `IssuePlaybackTicketCommandHandlerTests.cs`: the not-found test asserts `ErrorType.NotFound` with code `CurriculumResource.NotFound`; the not-a-video test keeps `Validation` / `CurriculumItem.NoVideo`.

### W6: Test hygiene

**File:** `tests/Modules/Courses/Courses.UnitTests/Application/Courses/IssuePlaybackTicketCommandHandlerTests.cs`

1. Remove the unused `private static CourseResourceDetailsDto VideoDetails()` helper if still present.
2. Extract a shared `private CourseResourceDetailsDto SetupValidVideoDetails()` arrange helper that stubs `GetResourceDetailsAsync` and returns the DTO, and use it in every test that needs a valid video placement instead of repeating the inline construction + `.Returns(...)`.

### W7 (Judgement call, implement as stated): Surface parser skips instead of failing silently

**File:** `src/alphazero-api/Modules/Courses/Application/Courses/Commands/Items/CourseSectionItemCommands.cs`

`CurriculumResourceParser.Parse` currently does `if (!courseAssetId.HasValue) { continue; }` — a malformed block vanishes with no signal. Change `Parse` to collect skip reasons and expose them:

- Change the signature to `public static (List<CurriculumResource> Resources, List<string> Warnings) Parse(string mainType, JsonElement content)`; on skip, add `$"Skipped {normalizedType} block at index {order}: missing courseAssetId"`.
- In `CreateCourseSectionItemCommandHandler` / `UpdateCourseSectionItemCommandHandler`, pass warnings to `_logger.LogWarning(...)` so operators see dropped blocks in logs. Do **not** fail the command (existing payloads with stray `videoId` fields must keep updating successfully).

---

## 3. Explicit Non-Goals

- No changes to `Modules/Assessments/**`, `Modules/VideoUploading/**`, `Modules/Documents/**`.
- No new database migration: the deleted repository methods had no schema footprint, and `CurriculumResource.CourseAssetId` mapping is unchanged.
- No `GLOSSARY.md`/ADR authoring (none exists in the repo; do not create one).
- `tests/Modules/VideoUploading/VideoUploading.Tests.Integration/CreatePlaybackSessionHandlerUnitTests.cs` has **pre-existing** compile errors (`FakeCurrentTenantUserRepository` vs `IAuthorizationContextProvider`, `WatermarkContextDto.UserPhone`). Out of scope — leave as-is.

## 4. Verification

```bash
dotnet build AlphaZero.sln          # must compile; only the pre-existing VideoUploading integration test errors may remain
dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj   # 100% pass
grep -rn "GetItemBitIndexBy\|GetCoursesByCourseAssetIdAsync\|orderedAssetOrResourceIds" src/alphazero-api/   # zero hits
```

Report: files changed, test results, and any frontend files updated in W2.
