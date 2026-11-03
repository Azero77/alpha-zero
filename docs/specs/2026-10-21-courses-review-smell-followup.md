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

---

# DX PLAN REVIEW — 2026-10-22 (plan-devex-review)

This section is a review deliverable: it records evidence, approved decisions, and review
amendments to the plan body above. Original W1-W5 end states stand unless an amendment below
changes them; implementation design is NOT finalized here — open items are listed verbatim in
`Unresolved Decisions` at the end, and mirrored in the terminal review report.

## Decision Ledger (this review)

| # | Source/evidence | Current value | Proposed value | Approval + scope | Others fixed/pending |
|---|---|---|---|---|---|
| D2 | Skill applicability gate over plan+files | — | Product type: API/Service (both API-consumer + maintainer surfaces) | User pick: A (question log 2026-10-22) | — |
| D3 | Wave #32 test files + branch name | persona assumed student-UX frontend | Persona: cross-module backend dev | User pick: C | Persona recorded in working notes only until 0C answered |
| D4 | Empathy narrative (file+line-cited) | — | Corrections: integration events are outbox messages; consumers run only after the transaction lands (confirmed in code: `AddEntityFrameworkOutbox<AppDbContext>`, CoursesModule.cs:52) | User correction, folded into narrative | — |
| D5 | 0C benchmark table (15-25 min est. vs 2-3 min platform docs) | contract questions answerable only by spelunking ≥7 files | TTHW target: Champion (< 2 min) from the contract surface alone | User pick: A — upgrades may need a repo-wide doc convention; convention itself stays an observation (recorded, no work in this spec) | benchmark boundaries fixed; measurement policy pending (Pass 8) |
| D6 | 0D vehicle options (Hall of Fame Pass 1 lens) | magic = existing verify commands only | Vehicle: Contract-on-read — the three prediction answers live on interface XML docs + one anchor comment in W4 | User pick: A — prose drift deferred by code review; executable-test pin remains an optional approved-later add-on | test budget untouched (zero test edits bar stands) |
| D7 | Mode defaults + plan maturity | — | Mode: DX POLISH (full rigor inside spec's walls; no scope additions) | User pick: A | Convention work explicitly deferred |

Pre-review ledger items from earlier rounds (eng-review ledger D2/D3/D4/D6/D7/D8/D9/D10) remain dispositions of record; this review re-litigates none of them.

## Target Developer Persona

```
TARGET DEVELOPER PERSONA
Who:       Backend dev in another module (Tenants/Identity class) integrating with or
           debugging against Courses' repository + UnitOfWork contracts
Context:   Consumes Courses integration events (Identity consumes EnrollmentCreated:
           EnrollmentCreatedIntegrationEventConsumer.cs); reads Courses' write path to
           predict side effects; inherits shared BaseRepository/IRepository semantics
Tolerance: Zero tolerance for undocumented tracker/eviction side effects; reads XML
           contracts and interfaces first; abandons trust after one surprise mutation
Expects:   GetFreshByIdAsync = "fresh enrollment, nothing else touched"; stable error
           codes; publish/outbox ordering documented; "fresh" to mean what the name says
```

## Developer Empathy Narrative (verified + user-corrected 2026-10-22)

"I'm the backend dev on Identity who keeps one eye on Courses because we consume their events.
My consumer resolves the tenant and maps `EnrollmentCreated` into an assign-principal command
[observed: Identity/Consumers/EnrollmentCreatedIntegrationEventConsumer.cs]. A ticket lands:
'student finished all items but the gamification credit is missing.' My first stop is Courses'
CompleteItem, not my code.

T+0:20 I'm reading CompleteItem.cs. It's honest: load, guard, apply, save-with-retry-ONCE
[observed: lines 31-89]. But two things slow me down. First: the retry contract says
GetFreshByIdAsync 'reloads an enrollment discarding in-memory tracked state' [observed:
IEnrollmentRepository XML doc] — and the implementation calls ChangeTracker.Clear() on the
whole tracker [observed: EnrollmentRepository.cs:30]. The method name promised a fresh
enrollment; the body evicted the module's entire tracker [predicted confusion]. I believe it
cannot touch Identity's entities because each module registers its own AppDbContext, and
integration events reach other modules only after the transaction lands via the outbox
[observed: per-module AddDbContext; AddEntityFrameworkOutbox<AppDbContext>, CoursesModule.cs:52;
confirmed by the domain owner]. But today I had to derive BOTH of those facts from code
spelunking — neither is on the contract I read. Second: when the first save fails, its domain
events were already published in the publish-then-save loop [observed: UnitOfWork.cs:21-45];
will the retry fire ItemCompleted twice? The damping answer (CompletedOn guard suppresses the
duplicate) is buried mid-function [observed: CompleteItem.cs:76-81; Enrollment.cs:87].

I trust contract docs. Today the docs don't carry either answer." 

Corrections accepted 2026-10-22: integration events are outbox-gated (user + code cited
above); the in-process MediatR loop only crosses to Courses' own DomainEventHandler, whose
MassTransit publish lands in the module's EF outbox — cross-module handling is post-commit.

## Competitive DX Benchmark

Clock: start = persona opens the relevant contract; end = correctly predicts (a) eviction
scope of GetFreshByIdAsync, (b) retry's event semantics, (c) cross-module tracker impact
— without reading method bodies. Human reading time; estimates labeled as such.

| Tool | Boundaries (start → result) | Time + evidence type | DX choice | Source |
|---|---|---|---|---|
| EF Core ChangeTracker docs | "What does Clear evict?" → one page: stops tracking ALL tracked entities, pooling caveat included | ~2 min; observed doc page | contract answers at the API's name; side effects warned at same place | learn.microsoft.com/ef/core/change-tracking (fetched via websearch) |
| MassTransit outbox docs | "When do my consumers run?" → stored with app data, delivered after commit/restart | ~3 min; observed doc page | delivery timing documented on the concept page | masstransit.massient.com/concepts/outbox (fetched via websearch) |
| Stripe event/error contract docs (category anchor) | "What happens on retry/duplicate?" → page per code | ~2 min; in-distribution knowledge, NOT fetched | every code documents its own page | docs.stripe.com |
| This repo (persona's journey) | prediction via contract surface | **~15-25 min ESTIMATED** (span of ≥7 files: IEnrollmentRepository, EnrollmentRepository, CompleteItem, UnitOfWork, CoursesModule DI, Program.cs multibus, Identity consumer) — not stopwatched | contract text exists but lives in method bodies; eviction scope + delivery timing + context ownership undocumented at the seam | observed files as cited throughout this section |

Gap method: a 10 for THIS product = all three predictions correct from the interface XML
docs alone, ≤ 2 min, plus a documented one-page convention describing per-module context
ownership (convention EXPLICITLY DEFERRED — see NOT in scope).

## Magical Moment Specification

Moment: the cross-module dev opens ONE interface file and answers eviction scope, retry
event semantics, and cross-module tracker impact correctly in under 2 minutes — "oh wow, this
is real" equivalent for contract reading (Stripe pre-fills your keys; here the contract
gives you the answer before the question).

Delivery vehicle (approved D6): **Contract-on-read** — upgrades to the XML docs the compiler
already parses + one anchor comment in W4. Implementation requirements (plan level,
folded into W-item amendments below):

1. `IEnrollmentRepository.GetFreshByIdAsync` XML doc (W5's mandated edit) must carry three
   sentences: (a) eviction scope — "evicts exactly the locally-tracked instance of this
   enrollment (entity-scoped; not a tracker-wide reset); the returned instance is a NEW
   re-read, not the reused tracked instance"; (b) context ownership — "contracts run on this
   module's own scoped AppDbContext; other modules register their own contexts, so this
   eviction cannot cross module boundaries today; revisit this sentence before introducing a
   pooled/long-lived context"; (c) non-negotiable precision — the doc's claims must match the
   implementation's actual detach list (updated again if D8 adds a course-side contract).
2. W4's helper comment block stays with its call site and GAINS one anchor sentence on
   delivery timing: "domain events raised on the apply are published in-process by
   UnitOfWork before the save; the integration events they translate ride the module's
   EntityFramework outbox and reach other modules only after the transaction commits."
3. The course-side fresh-load contract (ICourseRepository) is DEPENDENT on the open D8
   decision and is not specified here.

## Developer Journey Map

```
STAGE           | DEVELOPER DOES                        | FRICTION POINTS (evidence)                            | STATUS
----------------|---------------------------------------|-------------------------------------------------------|--------
1. Discover     | opens IEnrollmentRepository contracts | eviction scope absent from doc (EnrollmentRepository.cs:30) | fixed (W5 amendment)
2. Install      |  settles trust: contexts + outbox     | context ownership + delivery timing tribal knowledge (CoursesModule.cs:52, Program.cs:266) | fixed (W4 anchor sentence amendment)
3. Hello World  | first correct prediction (< 2 min)    | answers spread across ≥7 files                         | fixed (D5/D6: 3 sentences on contract surface)
4. Real Usage   | consumes events; builds on retry seam | course-load freshness shape open (CourseRepository.GetById inherited tracked, IRepository.cs:117) | deferred (D8 pending)
5. Debug        | ticket forensics on credit/race       | endpoint summary drift (CompleteItem.cs:27-28 names phantom 'Course.Item', omits Course.NotFound + Progress.ItemInactive); second-conflict 500 has no stable code (D3-accepted) | deferred (D9 pending; 500 semantics out of scope per D3)
6. Upgrade      | applies spec W1-W5 behind ship gate   | none — blast-radius fences + per-item verifies already in plan | ok
```

## First-Time Developer Confusion Report (first-time contract reader, timestamps simulated)

```
FIRST-TIME DEVELOPER REPORT
Persona:    cross-module backend dev (D3 answer)
Attempting: predict Courses completion-flow behavior from contract surface

CONFUSION LOG:
T+0:00  Opens IEnrollmentRepository.cs. Reads GetFreshByIdAsync summary. Sonar-Q: "discarding
        any in-memory tracked state" — which state? Whose? No answer on the contract surface.
T+0:30  Greps implementation. Finds ChangeTracker.Clear() (EnrollmentRepository.cs:30). Two-
        minute detour to derive that this cannot touch other modules (per-module contexts) —
        knowledge the contract should have carried. (addressed: amendment 1b)
T+1:00  Asks "when do other modules see CourseCompleted?" Reads UnitOfWork.cs:21-45,
        misreads the 'one transaction' comment (publish loop runs BEFORE the save), then must
        discover AddEntityFrameworkOutbox wiring to find the truth. (addressed: amendment 2)
T+2:00  Asks "does the retry double-publish ItemCompleted?" — answer exists only in
        CompleteItem.cs:76-81 + Enrollment.cs:87 comments. Accepted as read-deep contract;
        W4 keeps exactly this comment with its call site (spec already mandates it).
T+3:00  Verdict: partial success — 3 of 5 questions answered post-amendment without
        implementation reading; 2 remain implementation-defined pending D8/D9.
```

## Review Amendments to the Plan Body (approved scope only)

These are the plan-text deltas this review authorizes:

1. **W5 amendment (contract doc content).** The W5 end state's XML-doc rewrite must carry the
   three Contract-on-read sentences from the Magical Moment Specification:
   entity-scoped eviction sentence including the new-instance-swap clarification; the
   per-module context ownership sentence naming the fail-safe to future pooled contexts; and
   the precision requirement against the implementation's final detach list.
2. **W4 amendment (helper comment anchor).** W4's kept comment block gains the one-sentence
   outbox delivery-timing anchor above.
3. **W3 amendment (snippet fidelity).** The spec's own `IsValidSortKey` snippet calls
   `raw.Trim()` twice — contradicting the finding it implements ("trim once"). The predicate
   must trim once into a local (`var key = s.Trim(); key.Equals(CatalogSortKeys.Newest, ...) || key.Equals(CatalogSortKeys.Title, ...)`),
   preserving the `s is null` guard shape in the `Must` rule. Behavior-zero; restores the item's declared intent (review work, no approval needed beyond this record).
4. **W1 amendment (no functional delta).** Keep XML doc as-is (it already records the Npgsql
   discovery); the constant reference replaces the literal in doc prose where it names the
   escape character.
5. **Persona + target wire-in.** The ship gate (section 3) gains one line: "the three
   contract questions above are answerable from the updated XML docs without opening
   implementation files" — as the Champion-target acceptance audit for a future
   /devex-review boomerang.

Everything else in W1-W5 stands exactly as written. W4's helper SHAPES beyond the loader
selector, and the course-side contract, are explicitly NOT decided here (D8 pending).

## NOT in scope (DX items considered and explicitly deferred)

- Repo-wide repository-doc convention ("what *fresh* means here" template for every module)
  — Champion-grade idea surfaced by D5; bigger boundary than this spec owns. Observation for
  a future spec.
- Executable contract-test pin for entity-scoped eviction (D6 option B) — violates the spec's
  zero-test-edits bar until separately approved.
- Endpoint summary drift fix (CompleteItem.cs:27-28) — pending D9 (see unresolved).
- Retry second-conflict surfacing (500 ProblemDetails contract) — explicitly out of scope per
  spec section 4 and D3.
- UnitOfWork.cs:43 "one transaction" comment correction — outside this spec's declared file
  blast radius; recorded as observation only (do not fix here).
- Telemetry/measurement instrumentation (Pass 8) — policy needs its own decision.

## What already exists (reuse list)

- Wave #32's XML docs already encode the keep-in-sync pattern (CourseQueryService summary
  lines 91-93: "shared only as a documented keep-in-sync contract") — the W5 contract
  sentences follow this established pattern.
- Pending EF Core + MassTransit platform docs as one-page anchors to LINK from contracts
  (cost: zero new infrastructure).
- BaseIntegrationTest + TestAuthHandler anonymous opt-out harness — the verify commands'
  harness; untouched.
- CatalogSortKeys constants + EscapeLikePattern doc — the naming vocabulary W1/W3 build on.
- The per-item verify + ship gate structure (test counts pinned 207/99/40).
- Prior D-disposition ledger (eng-review 2026-10-20) — the review's decision baseline.

## Implementation Tasks

Synthesized from this review's findings. Each task derives from a specific finding above.
Run with Claude Code or Codex; checkbox as you ship.

- [ ] **T1 (P1, human: ~1h / CC: ~10min)** — Courses contracts — fold the three Contract-on-read sentences into W5's XML doc + W4's helper anchor comment
  - Surfaced by: Pass 1 (TTHW 15-25 min est. vs Champion < 2 min target) + 0D vehicle decision
  - Files: src/alphazero-api/Modules/Courses/Application/Repositories/IEnrollmentRepository.cs, src/alphazero-api/Modules/Courses/Application/Enrollments/Commands/CompleteItem/CompleteItem.cs
  - Verify: dotnet test Courses.UnitTests Courses.Tests.Integration (no test edits expected); read the XML doc and confirm all three claims stated
- [ ] **T2 (P2, human: ~20min / CC: ~5min)** — ListCourseCatalog validator — apply the W3 trim-once snippet correction (local `key`)
  - Surfaced by: Pass 2 (spec snippet contradicts its own finding)
  - Files: src/alphazero-api/Modules/Courses/Application/Courses/Queries/ListCourseCatalog/ListCourseCatalog.cs
  - Verify: dotnet test Courses.Tests.Integration filtered `FullyQualifiedName~Catalog` — green
- [ ] **T3 (P3, depends on D8)** — CompleteItem helper — final loader shape + course-side fresh-load contract per the D8 answer
  - Surfaced by: Journey stage Real Usage (tracked course load; IRepository.cs:117)
  - Files: CompleteItem.cs, EnrollmentRepository.cs, + (per D8) IEnrollmentRepository.cs / ICourseRepository.cs / CourseRepository.cs
  - Verify: Courses.UnitTests `CompleteItemCommandHandlerTests` + Courses.Tests.Integration `CompletionEventTests` green
- [ ] **T4 (P3, depends on D9)** — CompleteItem endpoint summary — repair 404/409 code lists ('Course.Item' phantom removal; Course.NotFound + Progress.ItemInactive additions)
  - Surfaced by: Journey stage Debug (summary drift; CompleteItem.cs:27-28)
  - Files: src/alphazero-api/Modules/Courses/Presentation/Enrollments/CompleteItem/CompleteItem.cs
  - Verify: summary diff review only (declarative FastEndpoints metadata)
- [ ] **T5 (P3, depends on measurement-policy decision)** — DX measurement — run the 3-question contract audit against the updated XML docs and record actual first-open time
  - Surfaced by: Pass 8 (no instrument for the Champion target)
  - Files: this spec's ship gate (audit line) + future /devex-review boomerang
  - Verify: audit record in the review log

## Candidate TODOs (awaiting user confirmation — not silently added)

A. "Contract-test pin for entity-scoped eviction" (D6 option B) — adoption pin, cost: one
   flaky-risky integration test + test-budget exception. Depended-on: D8 resolution.
B. "Repository doc convention for all modules" (D5 Champion fallout) — adoption + retention,
   cost: cross-module sweep. Depended-on: W5 implementation proving the pattern.
C. "ProblemDetails enrichment: include entity ids in NotFound messages" (Pass 3 CAP6:fix)
   — battle-tested error UX upgrade; cost: API response text change (behavior-visible), so
   needs a behavior-window decision, NOT this spec.

## DX Review Scorecard (post-interview, post-amendment)

```
+====================================================================+
|              DX PLAN REVIEW — SCORECARD                             |
+====================================================================+
| Dimension            | Score  | Prior  | Trend  |
|----------------------|--------|--------|--------|
| Getting Started      | 7/10   |   —    | new ↑  |
| API/CLI/SDK          | 6/10   |   —    | new ↑  |
| Error Messages       | 4/10¹  |   —    | —      |
| Documentation        | 7/10   |   —    | new ↑  |
| Upgrade Path         | 8/10   |   —    | new    |
| Dev Environment      | 7/10   |   —    | new    |
| Community            | 5/10²  |   —    | —      |
| DX Measurement       | 5/10   |   —    | new ↑  |
+--------------------------------------------------------------------+
| TTHW                 | ~20 min EST | ~20 min EST | target < 2 min ↑ |
| Competitive Rank     | Needs Work → on-trajectory to Competitive   |
| Magical Moment       | designed via Contract-on-read (D6)          |
| Product Type         | API/Service (refactor spec)                 |
| Mode                 | POLISH                                      |
| Overall DX           | 6/10 → 6 (post-amend, weighted avg)         |
+====================================================================+
| DX PRINCIPLE COVERAGE                                               |
| Zero Friction      | covered (contract surface onboarding)         |
| Learn by Doing     | covered (highlighted docs to link)            |
| Fight Uncertainty  | covered (3 contract question amendments)      |
| Opinionated + Escape Hatches | gap (loader-override shapes open: D8)|
| Code in Context    | covered (W4 keep-comments with call sites)    |
| Magical Moments    | covered (Contract-on-read vehicle)            |
| Agents Are Users   | partial (agent-ready error contract applied;  |
|                    |  fixes pending behavior decisions)            |
+====================================================================+
¹ CAP6:fix / CAP6:context pending — cannot implement without behavior decision.
² Structurally N/A (internal product); evaluated, no action possible in this spec.
```

All-passes-8+? No. Below-6 flags: Error Messages 4/10 (critical DX debt: ids in
messages + summary drift + 500 code) — adoption impact: every Not-found incident repeats the
file-spelunk. TTHW > 10 min flag: BLOCKING until boomerang audit measures under champion: treat as adoption-debt, not ship-block (zero-behavior spec).

Immplementation checklist: → replaced with correct heading below

Implementation checklist:

```
DX IMPLEMENTATION CHECKLIST
===========================
[ ] Time to correct prediction < 2 min (target; unmeasured until T5 audit)
[~] "Installation is one command" — N/A for contract reading; verify commands are one-liners ✔
[ ] First open of contract produces the three predictions (pending T1)
[~] Magical moment delivered via Contract-on-read (approved; await code)
[~] Every error message meets agent-ready contract — CAPPED: fix/context items pending decisions
[~] API naming is guessable — W1/W2 guarantee it; D8 could make the course fresh-load name
[X] upgrade path documented with migration guidance — ship gate + zero-behavior ✔  (W5 mocks)
[~] TypeScript types included — N/A (C# product)
[~] Works in CI/CD without special config ✔ (dotnet test gates)
[X] Free tier N/A
[X] Changelog N/A
[~] Search works in documentation — repo grep suffices; no doc-site
[X] Community N/A
```
[~] = partially satisfied / pending listed decision. This checklist is the review's plan-state record, not a literal ship gate.

## Unresolved Decisions

Listed to respect "never silently default". These are decisions this review DID NOT ask
because the user directed the review to plan-record mode only:

- **D8 — retry mask freshness (W4/W5 shape).** Evidence: course reload inside the retry uses
  the inherited tracked `GetById` (IRepository.cs:117); the course aggregate is never mutated
  or saved by this seam; the spec instructs "apply the same targeted detach for the course in
  the flow W4's helper drives". Candidate shapes recorded in source order: (α) two fresh
  loaders + `GetFreshByIdAsync` on both interfaces, entity-scoped detach in both
  implementations (spec-faithful); (β) never track in the seam: an untracked single-row
  `ActiveItemsBitmask` snapshot read on the course interface (deletes the detach class, not
  patches it); (γ) spec-literal single-loader + accepted stale-mask window documented as
  behavior. One approval covers the bundle (they are one commitment).
- **D9 — CompleteItem endpoint summary drift** (CompleteItem.cs:27-28; phantom 'Course.Item',
  missing Course.NotFound / Progress.ItemInactive). Reported by repo-mode solo; disposition
  pending: fix as W6, or record to the ledger only.
- **TODO confirmations A/B/C above** (one each per TODOS.md policy) — pending user answers.
- **Measurement policy** (Pass 8): TTHW audit trigger (T5) — whether run now or at boomerang.

---

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | — |
| Outside Review | codex-plan-review (auto, after plan reviews) | Independent 2nd opinion | 2 | unavailable | provider attempt 2026-10-20 skipped (source:none); 2026-10-21 unavailable — no outside coverage established |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 2 | issues_open | 6 issues, 1 critical gap (ledger dispositions recorded; D3 source of record) |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | — | — |
| DX Review | `/plan-devex-review` | Developer experience gaps | 1 | issues_open | score: 5/10 → 6/10, TTHW: ~20 min est → < 2 min target; 7 decisions logged (D2-D7 approved + D4 correction), 4 unresolved (D8, D9, TODOs, measurement policy) |

**OUTSIDE COVERAGE:** outside voice unavailable (provider not available in this harness; both
recorded attempts: skipped 2026-10-20, unavailable 2026-10-21; today's preflight
reconfirms no external agent available on OpenCode). No completed external review exists —
no outside coverage. Per policy this does NOT block this DX plan review's own verdict; it is
recorded missing coverage, follow the caller's fallback only if reviews are enabled.

**VERDICT:** ENG + DX reviews carried issues_open — this spec is scoped-for-implementation
AFTER the original five-item plan, pending D8 + D9 answers before the affected W4/W5 shapes
and any W6 are finalized. DX: upgrade amendments approved (D5/D6/D7); eng review required for
any behavior-bearing executor that leaves the five-item fence (none currently proposed).

**UNRESOLVED DECISIONS:**
- D8 — retry mask freshness bundle (α two-fresh-loaders+contracts / β untracked snapshot read / γ spec-literal stale window) — blocks W4/W5 finalization
- D9 — CompleteItem endpoint summary drift fix (W6) vs ledger-only
- TODO A/B/C confirmations (see Candidate TODOs)
- T5 measurement trigger (run the 3-question contract audit now vs at next /devex-review)
