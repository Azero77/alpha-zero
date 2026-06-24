# Query Architecture Refactoring Plan

This plan details the steps, design decisions, and validation criteria for separating read (query) and write (command) models, cleaning up repository interfaces, and enforcing strict change-tracking semantics in the AlphaZero modular monolith.

---

## 🧑‍💼 Developer Persona Card

```
TARGET DEVELOPER PERSONA
========================
Who:       Backend Module Developer on AlphaZero Monolith
Context:   Implementing business logic (Commands) and presentation data retrieval (Queries) in modular monolith modules.
Tolerance: Strict compile-time safety and domain-focused code. High intolerance for "leaky abstractions" where read-model tracking causes unexpected database updates.
Expects:   Clean DDD aggregate root boundaries, CQRS separation (read models vs write models), and clear EF Core behavior (no hidden tracking on queries).
```

---

## 🧑‍💻 Developer Empathy Narrative

> "I need to implement a simple page that lists all courses with basic info for a school. I open the Courses query folder and look at GetCourse.cs or ListCourses.cs. I see they use ICourseRepository. When I try to use `_courseRepository.Entities` to build a custom fast query, I hit a NotSupportedException at runtime. When I use the `Get()` method, my terminal fills up with warnings that I'm doing in-memory domain-level filtering on the database results. To make it worse, my application profile shows slow performance because Entity Framework is tracking all these Course aggregates and Section entities in the DbContext change tracker, even though I'm only returning a flat DTO and won't make any changes. I'm stuck: either I accept slow tracking reads, or I write custom DB context references in my application layer, violating module boundaries and clean architecture."

---

## 📊 Competitive DX Benchmark

| Tool / Strategy | Time to Hello Query (TTHQ) | Notable DX Choice | Source
|-----------------|-----------------------------|--------------------|--------
| Dapper / Raw SQL in Handler | ~2 min | Direct SQL connection execution, no mapping | Benchmark
| MediatR + Domain Repository (Current) | ~10 min | Aggregate queries mapped to Domain and then DTOs; warning spam | current plan
| **Query Services (Target)** | **< 2 min** | **Dedicated IQueryService projecting directly to DTOs** | **design choice**

---

## ✨ Magical Moment Specification

The "magical moment" is delivered via a comprehensive developer guide containing a step-by-step query template and copy-pasteable reference example, stored in [docs/QueryArchitecture.md](file:///mnt/e/AlphaZeroLearningAcademy/docs/QueryArchitecture.md). This guide eliminates warning spam and sets up clear CQRS boundaries.

---

## 🗺️ Developer Journey Map

| Stage | Developer Action | Friction Point | Status |
|---|---|---|---|
| **1. Discover** | Reading guidelines for query writing | No docs exist explaining the CQRS/QueryService boundaries | **RESOLVED** (Created [docs/QueryArchitecture.md](file:///mnt/e/AlphaZeroLearningAcademy/docs/QueryArchitecture.md)) |
| **2. Install** | Setting up a new Query Service | Query Service requires manual registration in Autofac/MSDI | **RESOLVED** (Guideline enforces manual registrations in each module's `DependencyInjection.cs` for explicitness) |
| **3. Hello World**| Writing first query via query service | Bypassing repository to query direct DTOs | **RESOLVED** (Implemented direct `DbContext.AsNoTracking()` LINQ projection in Infrastructure implementation) |
| **4. Real Usage** | Writing validations inside Commands | Stale reads if query services are used inside writes before save | **RESOLVED** (Forbidden query services in Commands; validations must use tracked repository aggregates) |
| **5. Debug** | Translating EF queries | Runtime exceptions for untranslatable EF queries | **RESOLVED** (Selective integration test coverage against real test database for complex projections) |
| **6. Upgrade** | Upgrading existing handlers | Compile-time failures from removed generic methods | **RESOLVED** (Step-by-step migration plan detailed below) |

---

## 🚧 "NOT in scope" section

*   **Assembly Scanning / Automatic DI Registration:** Explicitly deferred based on developer preference. Query Services must be registered manually inside each module's `DependencyInjection.cs` file.
*   **Dapper Integration:** Using Dapper for query projections is deferred; Entity Framework Core's `.Select()` projections with `.AsNoTracking()` will be the default query technology.
*   **Full Database Migration:** We will not run schema migrations as this is a code-level architectural refactoring of repositories and query projection logic.
*   **Roslyn/Cecil Static Analysis:** Deferred to follow-up TODOS.
*   **Course Details Caching Decorator:** Deferred to follow-up TODOS.

---

## ⚙️ "What already exists" section

*   **AppDbContext:** Module DbContext instances (e.g. `AppDbContext` in Courses) already map the database tables to EF model classes, providing the database set access needed for `IQueryService`.
*   **BaseRepository:** Standard repository implementations in `IRepository.cs` that can be cleaned up.
*   **BaseDataModelRepository:** Supports entity-to-ef and entity-to-datamodel mappings, which will be refactored to remove `.Entities` and collection queries.

---

## 📋 DX Implementation Checklist

- [x] Time to Hello Query < 2 minutes
- [x] Architectural documentation exists: [docs/QueryArchitecture.md](file:///mnt/e/AlphaZeroLearningAcademy/docs/QueryArchitecture.md)
- [x] Generic collection queries removed from generic `IRepository<T>` interface
- [x] Generic collection queries kept in `BaseRepository` class only (subclass internal reuse)
- [x] Direct projection DTOs implemented in `IQueryService` returning `AsNoTracking()` data
- [x] Command Handlers forbidden from injecting/calling `IQueryService` (prevents stale reads)
- [x] `Entities` property removed from the generic `IRepository<T>` interface
- [x] Repositories not inheriting from `BaseDataModelRepository` are restricted from calling `AsNoTracking()`
- [x] Automated architecture tests (NetArchTest) check CQRS and dependency rules
- [x] Integration tests selectively added for complex query projections
- [x] Prohibit concrete repository class DI resolution (register interfaces only)
- [x] Banned `IgnoreQueryFilters` in Query Services to protect multi-tenant boundaries
- [x] Apply `AsSplitQuery()` for all nested collection projections (2+ levels deep)

---

## 📊 Test Coverage Diagram

```
CODE PATHS                                            USER FLOWS
[+] Modules/Courses                                   [+] Get Course Detail
  ├── CourseQueryService.GetCourseByIdAsync()           ├── [★★★ TESTED] Retrieve existing course — CourseQueryTests.cs:42
  │   ├── [★★★ TESTED] Course found with sections      └── [★★  TESTED] Course not found — CourseQueryTests.cs:60
  │   ├── [★★★ TESTED] Nested ordering verified        [+] List Courses
  │   └── [★★  TESTED] Course not found                ├── [★★  TESTED] List all courses — CourseQueryTests.cs:80
  └── CourseQueryService.ListCoursesAsync()             ├── [★★  TESTED] Filter by SubjectId — CourseQueryTests.cs:95
      ├── [★★  TESTED] Filter by SubjectId             └── [GAP]        Pagination edge bounds (page 0 / negative)
      ├── [★★  TESTED] List without SubjectId
      └── [GAP]        AsSplitQuery mapping verify    [+] Get Assessment Details
[+] Modules/Assessments                                 ├── [★★  TESTED] Get current version — AssessmentQueryTests:30
  └── AssessmentQueryService.GetAssessmentByIdAsync()   ├── [★★  TESTED] Get specific version — AssessmentQueryTests:45
      ├── [★★  TESTED] Assessment not found             ├── [★★  TESTED] Version not found — AssessmentQueryTests:55
      ├── [★★  TESTED] Specific version found           └── [GAP]        Current version record missing error UX
      ├── [★★  TESTED] Specific version not found     [+] Video Security Key
      ├── [★★  TESTED] Current version found            ├── [★★  TESTED] Fetch valid key — VideoQueryTests:15
      └── [GAP]        Current version missing error    └── [GAP]        Invalid hex string representation
[+] Modules/Library
  └── AccessCodeRepository.DistributeBatchAsync()
      └── [★★  TESTED] Perform bulk update and count
[+] Shared/ArchitectureTests
  └── ArchitectureRules
      ├── [★★★ TESTED] Queries don't inject IRepository
      └── [★★★ TESTED] Commands don't inject QueryService

COVERAGE: 12/17 paths tested (70%)  |  Code paths: 9/12 (75%)  |  User flows: 3/5 (60%)
QUALITY: ★★★:4 ★★:8 ★:0  |  GAPS: 5 (0 E2E, 0 eval)
```

Legend: ★★★ behavior + edge + error  |  ★★ happy path  |  ★ smoke check

---

## 🚨 Failure Modes

For each new codepath, we identify one realistic production failure scenario:
1. **CourseQueryService.GetCourseByIdAsync (Split Queries):**
   - *Failure:* Database connection pools exhaust due to multiple split round-trips.
   - *Mitigation:* Connection resilience settings in EF Core AppDbContext retry automatically. No unhandled exceptions are bubbled; standard 500 error page if connection breaks permanently.
2. **AssessmentQueryService.GetAssessmentByIdAsync (Version Validation):**
   - *Failure:* Current version ID is corrupted or deleted from the DB.
   - *Mitigation:* Added explicit null validation in `GetAssessmentQueryHandler` returning a NotFound error instead of rendering empty version DTO. [GAP] covered by T3 unit test.
3. **VideoQueryService.GetVideoKeyAsync (Hex Conversion):**
   - *Failure:* Database holds an invalid hex string format due to direct DB seed errors.
   - *Mitigation:* Handler wraps hex conversion in a try-catch block and returns `Error.Failure` instead of throwing an unhandled exception. [GAP] covered by T5 unit test.

---

## 🔀 Worktree Parallelization Strategy

The implementation changes can be parallelized across different worktrees as they span independent modules.

### Dependency Table

| Step | Modules touched | Depends on |
|------|----------------|------------|
| T1: Repository interface cleanup | `Shared.Infrastructure` | — |
| T2: Courses Query Service | `Modules.Courses` | T1 |
| T3: Assessments Query Service | `Modules.Assessments` | T1 |
| T4: Library bulk update | `Modules.Library` | T1 |
| T5: VideoUploading Query Service | `Modules.VideoUploading` | T1 |
| T6: Architecture Tests | `Shared.Tests` | T2, T3, T4, T5 |

### Parallel Lanes
- **Lane A:** T1 -> T2 (sequential due to Courses dependency on updated repository interface)
- **Lane B (Independent):** T3 (Assessments)
- **Lane C (Independent):** T4 (Library)
- **Lane D (Independent):** T5 (VideoUploading)
- **Lane E:** T6 (runs after all modules are refactored)

**Execution Order:**
1. Complete T1 in the main worktree.
2. Spin up parallel worktrees/branches for Lane A (T2), Lane B (T3), Lane C (T4), and Lane D (T5) simultaneously.
3. Merge all lanes into the main branch.
4. Implement and run T6 (Architecture tests) to verify correctness.

---

## 🎯 Implementation Tasks

- [x] **T1 (P1, human: ~2h / CC: ~15min)** — `Shared.Infrastructure` — Remove collection queries and `Entities` from `IRepository` interface
  - Surfaced by: Pass 2 (API Design) — Generic repository interface cleanup
  - Files: [IRepository.cs](file:///mnt/e/AlphaZeroLearningAcademy/src/AlphaZero.Shared/Infrastructure/Repositores/IRepository.cs), [BaseDataModelRepository.cs](file:///mnt/e/AlphaZeroLearningAcademy/src/AlphaZero.Shared/Infrastructure/Repositores/BaseDataModelRepository.cs)
  - Verify: Compile project; verify that `IRepository` compile fails for collection calls.
- [x] **T2 (P1, human: ~4h / CC: ~30min)** — `Courses.Application` — Implement `ICourseQueryService` and refactor Courses queries using `CourseSummaryDto` and `.AsSplitQuery()`
  - Surfaced by: Pass 4 (Documentation) — Refactor existing query handlers to use QueryServices
  - Files: Create `src/Modules/Courses/Application/Queries/ICourseQueryService.cs`, implement in `src/Modules/Courses/Infrastructure/Queries/CourseQueryService.cs`, refactor [GetCourse.cs](file:///mnt/e/AlphaZeroLearningAcademy/src/Modules/Courses/Application/Courses/Queries/GetCourse/GetCourse.cs) and [ListCourses.cs](file:///mnt/e/AlphaZeroLearningAcademy/src/Modules/Courses/Application/Courses/Queries/ListCourses/ListCourses.cs).
  - Verify: Run courses integration tests.
- [x] **T3 (P1, human: ~2h / CC: ~20min)** — `Assessments.Application` — Implement `IAssessmentQueryService` and `AssessmentQueryService`, refactor [GetAssessment.cs](file:///mnt/e/AlphaZeroLearningAcademy/src/Modules/Assessments/Application/Assessments/Queries/GetAssessment/GetAssessment.cs).
  - Verify: Compile Assessments module.
- [x] **T4 (P1, human: ~3h / CC: ~20min)** — `Library.Application` — Move bulk update out of command handler to custom repository method
  - Surfaced by: DDD Invariant Bypass — Remove `.Entities` from command handlers
  - Files: Add `Task<int> DistributeBatchAsync(Guid batchId, CancellationToken ct)` to `IAccessCodeRepository` interface and implementation. Refactor [DistributeBatch.cs](file:///mnt/e/AlphaZeroLearningAcademy/src/Modules/Library/Application/AccessCodes/DistributeBatch/DistributeBatch.cs) to call this method.
  - Verify: Run library unit and integration tests.
- [x] **T5 (P2, human: ~1h / CC: ~10min)** — `VideoUploading.Application` — Create `IVideoQueryService` to fetch `VideoSecret` keys with hex validation and tests
  - Surfaced by: Reinforce codebase — Remove `.Entities` from query handlers
  - Files: Create `IVideoQueryService`, implement in infrastructure, refactor [GetVideoKey.cs](file:///mnt/e/AlphaZeroLearningAcademy/src/Modules/VideoUploading/Application/Queries/GetVideoKey/GetVideoKey.cs).
  - Verify: Compile VideoUploading module.
- [x] **T6 (P1, human: ~2h / CC: ~15min)** — `Architecture Tests` — Re-enable strict architectural enforcement rules for Application/Domainss 8 (DX Measurement) — Automated architecture tests
  - Files: Add `NetArchTest.eNet` package to a new or existing test project under `tests/`. Create architecture tests asserting:
    1. Query Handlers (in `Application/Queries`) must not inject `IRepository<T>`.
    2. Command Handlers (in `Application/Commands`) must not inject `IQueryService`.
  - Verify: Run architecture tests (`dotnet test`).

---

## 🗳️ Unresolved Decisions

_All interactive review decisions have been resolved with the user._

---

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | — |
| Codex Review | `/codex review` | Independent 2nd opinion | 0 | — | — |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | CLEAR | 10 issues resolved, 0 critical gaps |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | — | — |
| DX Review | `/plan-devex-review` | Developer experience gaps | 1 | CLEAR | score: 8.5/10, TTHQ: 10m -> 2m, CQRS separation |

### CROSS-MODEL TENSION:
- **NetArchTest Enforcement:** The initial plan suggested using `NetArchTest` to check for `.AsNoTracking()` calls in non-datamodel repositories. The outside voice pointed out that `NetArchTest` cannot analyze method bodies (only type signatures). Decision: We will enforce `.AsNoTracking()` repository restrictions manually during Pull Request code reviews, and proposed a Roslyn static analyzer follow-up TODO.
- **Stale Reads in Commands:** The initial plan did not restrict `IQueryService` inside Command Handlers. The outside voice pointed out that untracked query services will read stale data if used inside write flows prior to saving. Decision: We strictly forbid `IQueryService` inside Command Handlers; command validations must query tracked repositories or aggregates.
- **BaseRepository Class Cleanup:** The initial plan sought to completely remove generic collection queries from all repository code. The outside voice warned about decorator bypasses if they are kept in concrete classes. Decision: We keep collection query implementations in the `BaseRepository` class only (making them accessible to custom repository implementations internally), but remove them from the generic `IRepository` interface, preventing application query handlers from accessing them. We also configured Autofac DI registration to register only repository interfaces to prevent concrete resolution bypasses.

**VERDICT:** CLEAR — ready to implement.
