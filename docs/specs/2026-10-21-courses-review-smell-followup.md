# Spec: Courses Wave #32 Review-Smell Follow-Up

**Status:** Ready for implementation
**Branch:** `spec/32-student-ux` (base commit `b3ee9938` — the #32 fix wave head; merge target `CourseContentManagement`)
**Scope:** `src/alphazero-api/Modules/Courses/` (+ Courses test files listed per item)
**Origin:** Standards axis of the `/code-review` two-axis review of wave `eedf0871...HEAD` (2026-10-21). All five findings are Fowler judgement-call smells — **no hard standard violations**. None block the #32 PR; this spec implements the accepted production triage afterwards.

**You are an autonomous coding agent.** Implement every work item below in order. One commit per work item, following the wave's commit-message pattern (`refactor(courses):` / `test(courses):` … `(#32)`). Do not touch modules other than `Courses` except where explicitly stated. `.qoder/`, `spec-32/`, and `TODOS.md` remain untouched and uncommitted.

---

## 1. Context

Wave #32 shipped with a green gate: **Courses.UnitTests 207/207, Courses.Tests.Integration 99/99, Tenants.UnitTests 40/40** (the known pre-existing `VideoUploading.Tests.Integration` compile break at HEAD is a baseline condition, NOT this spec's problem — do not attempt to fix it). The review found five code smells, triaged in-session:

| # | Smell | Location | Triage |
|---|---|---|---|
| 1 | Duplicated retry flow | `CompleteItem.cs` (Handle vs `RetryCompletionOnceAsync`) | refactor, careful (D3 write path) |
| 2 | Scope-wide `ChangeTracker.Clear()` | `EnrollmentRepository.GetFreshByIdAsync` | refactor, careful (D3 write path) |
| 3 | Repeated `@"\"` ILIKE escape literal | `CourseQueryService.cs` ×2 call sites | behavior-zero |
| 4 | Bare positional `false` in EF projection | `CourseQueryService.cs:218` | behavior-zero |
| 5 | Double-`Trim` + test nits | `ListCourseCatalog.cs` validator, `AnonymousCatalogViewTests` | behavior-zero |

**No new behavior is authorized by this spec.** Every end state is a pure refactor from the shipped wave; if any test changes beyond what an item explicitly names, STOP and report.

Order rationale: behavior-zero items first (W1–W3, each independently shippable), then the two D3-path items (W4–W5) which deserve the freshest full-suite verification because they touch the exactly-once completion seam governed by decision D3.

## 2. Work Items

### W1 (behavior-zero): one constant owns the ILIKE escape character

**File:** `src/alphazero-api/Modules/Courses/Infrastructure/Queries/CourseQueryService.cs`

**Finding:** the escape character `@"\"` appears at **two** `EF.Functions.ILike(...)` call sites (title and description) plus the XML doc comments (`EscapeLikePattern` and `ListCourseCatalogAsync` summary). A wrong edit in one site silently desyncs the other — a hazard the file itself already flags as a "keep-in-sync" contract class.

**End state:**
- A `private const string LikeEscapeChar = @"\";` next to `EscapeLikePattern` (current lines ~159–166) is the single source of truth.
- Both call sites (current lines ~105–106) pass `LikeEscapeChar`: `EF.Functions.ILike(c.Title, $"%{keyword}%", LikeEscapeChar)`.
- `EscapeLikePattern` builds the escapes from it: `.Replace(LikeEscapeChar, LikeEscapeChar + LikeEscapeChar)`, `.Replace("%", LikeEscapeChar + "%")`, `.Replace("_", LikeEscapeChar + "_")`.
- The XML doc on `EscapeLikePattern` stays (it records the Npgsql `ESCAPE ''` 2-arg-overload discovery) and now references the constant.

**Verify:** dotnet test `Courses.Tests.Integration` filtered `FullyQualifiedName~Catalog` — all green, **no edits** to `CatalogTests.cs`.

### W2 (behavior-zero): named argument for the base-projection completion flag

**File:** `src/alphazero-api/Modules/Courses/Infrastructure/Queries/CourseQueryService.cs` (current line ~218, inside `GetCourseSummaryForViewAsync`)

**Finding:** the projection passes a bare positional `false` between `i.BitIndex` and `i.Resources`, coupling the hand-written EF projection to `CourseViewItemDto`'s parameter order. The next field added to that record must blindly re-sync this file.

**End state:** the flag becomes a named argument — `IsCompleted: false, // no enrollment concerns; the overlay sets the flag` — with the surrounding comment kept. Named arguments are valid in EF LINQ projections (same `NewExpression` translation); no behavior change.

**Verify:** dotnet test `Courses.Tests.Integration` filtered `FullyQualifiedName~CourseViewTests` — all green (the T7 assertions on `IsCompleted` pin this projection).

### W3 (behavior-zero): trim the sort key once in the catalog validator

**Files:**
- `src/alphazero-api/Modules/Courses/Application/Courses/Queries/ListCourseCatalog/ListCourseCatalog.cs` (validator, current lines ~63–67)
- `tests/Modules/Courses/Courses.Tests.Integration/AnonymousCatalogViewTests.cs` (`SeedPublishedCourse`, current lines ~46 and ~71)

**Finding:** the validator calls `s.Trim()` for each of the two comparisons; `CourseQueryService:114` trims the same value again downstream. Effectively three trims of one string. Additionally, `AnonymousCatalogViewTests.SeedPublishedCourse` calls `SetTenant(tenantId)` at both :46 and :71 with no intervening change — the second call is redundant.

**End state:** a private static predicate keeps the rule readable and trims once:

```csharp
RuleFor(x => x.Sort)
    .Must(s => s is null || IsValidSortKey(s))
    .WithMessage("Sort must be 'newest' or 'title'.");

private static bool IsValidSortKey(string raw) =>
    raw.Trim().Equals(CatalogSortKeys.Newest, StringComparison.OrdinalIgnoreCase)
    || raw.Trim().Equals(CatalogSortKeys.Title, StringComparison.OrdinalIgnoreCase);
```

Delete the second `SetTenant(tenantId);` at `AnonymousCatalogViewTests.cs:71` (the one before the review/approve/publish calls).

**Verify:** dotnet test `Courses.Tests.Integration` filtered `FullyQualifiedName~Catalog_Should_AcceptCaseInsensitiveSortKeys | FullyQualifiedName~Catalog_Should_RejectUnknownSortKey | FullyQualifiedName~Catalog_Should_SortByTitleAndNewest | FullyQualifiedName~AnonymousCatalogViewTests` — all green.

### W4 (D3 write path): collapse the duplicated completion flow into one load helper

**File:** `src/alphazero-api/Modules/Courses/Application/Enrollments/Commands/CompleteItem/CompleteItem.cs` (current lines 31–89)

**Finding:** `RetryCompletionOnceAsync` (:68–83) repeats the main `Handle` flow (:31–44) — load enrollment → `Error.NotFound("Enrollment.NotFound", ...)` guard → load course → `Error.NotFound("Course.NotFound", ...)` guard → `enrollment.CompleteItem(bitIndex, course.SnapshotActiveItemsBitmask())` → error pass-through. The guard pairs are duplicated verbatim at :34/:71 and :41/:74.

**Constraint — the duplication is NOT fully mechanical.** The two flows differ in one material way: the main path loads via `GetByIdAsync`, the retry loads via `GetFreshByIdAsync` (the D3 fresh-re-read). The extraction must keep that distinction explicit at every call site — do not hide it.

**End state:**

```csharp
private async Task<ErrorOr<(Enrollment Enrollment, Course Course)>> LoadForCompletionAsync(
    Task<Enrollment?> loadEnrollment, Guid fallbackEnrollmentId, CancellationToken cancellationToken)
```

…is acceptable, but the preferred shape is a loader-selector parameter so both call sites read honestly:

```csharp
private async Task<ErrorOr<(Enrollment Enrollment, Course Course)>> LoadForCompletionAsync(
    Func<CancellationToken, Task<Enrollment?>> loadEnrollment,
    Guid enrollmentId,
    CancellationToken cancellationToken)
```

Both the guards and the `CompleteItem` apply move inside the helper; `Handle` calls it with `_enrollmentRepository.GetByIdAsync(id, ct)`, the retry with `_enrollmentRepository.GetFreshByIdAsync(id, ct)`. The retry's distinct log label ("on the concurrency retry") and the `MarkAsComplete`/`CompletedOn`-guard comment block (:76–81) stay with their call sites. The save stays OUTSIDE the helper (the retry save is deliberately unguarded; per D3 "retries ONCE", a second conflict surfacing as a failure is accepted behavior — do not "improve" it here).

**Verify:** dotnet test `Courses.UnitTests` filtered on `FullyQualifiedName~CompleteItemCommandHandlerTests` AND dotnet test `Courses.Tests.Integration` filtered on `FullyQualifiedName~CompletionEventTests` — all green with **no test edits** (the retry-path unit tests and race tests are the pin). Commit message: `refactor(courses): extract completion load helper — dedupe Handle/retry guards (#32)`.

### W5 (D3 write path): narrow the fresh-load side effect from tracker-wide to entity-scoped

**Files:**
- `src/alphazero-api/Modules/Courses/Infrastructure/Repositories/EnrollmentRepository.cs` (`GetFreshByIdAsync`, current lines ~28–35)
- `src/alphazero-api/Modules/Courses/Application/Repositories/IEnrollmentRepository.cs` (contract doc)

**Finding:** `GetFreshByIdAsync` calls `_context.ChangeTracker.Clear()` — evicting ALL tracked entities, not just the conflicting enrollment. The method name promises a fresh *enrollment* read; the actual side effect is broader. Safe today (AppDbContext is scoped per request), but fail-dangerous the day a pooled/long-lived context is introduced.

**End state:** replace the tracker-wide clear with entity-targeted eviction of exactly the entities the retry reloads — the enrollment, and the course if/when its repository yields a tracked instance:

```csharp
public async Task<Enrollment?> GetFreshByIdAsync(Guid id, CancellationToken cancellationToken = default)
{
    // D3 retry fresh-read: detach only what we are about to re-read — a tracker-wide
    // Clear() would evict unrelated tracked entities if a caller ever used a pooled
    // context. Keep the contract narrow because callers trust the narrow name.
    var tracked = _context.Set<Enrollment>().Local.FirstOrDefault(e => e.Id == id);
    if (tracked is not null) _context.Entry(tracked).State = EntityState.Detached;

    return await GetByIdAsync(id, cancellationToken);
}
```

Check the COURSE load path before writing code: `grep -n "GetById" src/alphazero-api/Modules/Courses/Infrastructure/Repositories/CourseRepository.cs` — if `GetById` is tracked, apply the same targeted detach for the course in the flow W4's helper drives (group the detach in one place the retry reaches; if the course repository is already `AsNoTracking`, note it in a comment and detach nothing for it). Update the `IEnrollmentRepository.GetFreshByIdAsync` XML doc to state the precise contract: "re-reads the authoritative row, evicting any locally-tracked instance of this enrollment first (entity-scoped; not a tracker-wide reset)".

**Constraint:** do NOT rename the method. Do NOT touch UnitOfWork's publish-then-save ordering (D3/D4/D6/D7/D9/D10 amendments stand). The generation of migration `20261021102420_AddEnrollmentRowVersion` is correct as shipped — not in this spec's blast radius.

**Verify:** the full Wave gate: dotnet test `Courses.UnitTests` (207/207 minimum, no test edits expected), `Courses.Tests.Integration` filtered `FullyQualifiedName~CompletionEventTests` + `FullyQualifiedName~CompleteItemCommandHandlerTests` green, then a full `Courses.Tests.Integration` run before committing. Commit message: `refactor(courses): entity-scoped fresh-load for the concurrency retry (#32)`.

## 3. Ship gate (run once at the end, in addition to per-item verifies)

- dotnet test `Courses.UnitTests` → 207/207 green (baseline: 207 at wave head; W1–W5 authorize zero test edits except the W3 `SetTenant` deletion)
- dotnet test `Courses.Tests.Integration` → 99/99 green
- dotnet test `Tenants.UnitTests` → 40/40 green
- `git log b3ee9938..HEAD --oneline` → exactly the five commits, each containing only its listed files

## 4. Out of scope (explicit)

- Any behavior change to completion semantics (exactly-once stays governed by D3; a second concurrent conflict on the retry save intentionally surfaces as a failure — leave unguarded).
- The `VideoUploading.Tests.Integration` compile break at HEAD (pre-existing, unrelated module).
- The D3 second-save 500 surfacing, the D8 false-positive refutation, and every L2/L5/L7/L8/L9 disposition — all stand as recorded in the eng-review ledger; this spec adds no new decisions.
- `.qoder/`, `spec-32/`, `TODOS.md`.
