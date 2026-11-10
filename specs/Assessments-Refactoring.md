# Code Review Report — Assessments Module

> **Scope**
> - Module: `src/alphazero-api/Modules/Assessments` (full tree, excluding `obj/`/`bin/`)
> - Fixed point: `origin/main` (`7918d72`) — HEAD is identical and the tree was clean, so the review covered the **entire module tree** rather than a diff.
>
> **Spec sources**
> 1. `docs/PRD.md` §3 "Hybrid Assessment System" — Automated MCQ/TrueFalse/LaTeX, Handwritten teacher-grading workflows, Exam Types
> 2. `docs/api-audit/01-assessments.md` — audited endpoints, request/response models, FluentValidation, ErrorOr codes
> 3. `docs/Identity/reference/system-roles.md` — `assessments:Create / Edit / Submit / ViewSubmissions`
> 4. `docs/specs/course-builder-and-lifecycle.md`, `docs/specs/2026-10-16-course-asset-identity-cleanup.md` — course asset integration, Layer 1 resource identity
>
> **Standards sources**
> 1. `GEMINI.md` — Enterprise Clean Architecture, Modular Monolith isolation, MassTransit hybrid messaging (`IMediator` in-memory vs `IPublishEndpoint` SQS), FastEndpoints, tenant scoping via `ITenantProvider`
> 2. Clean Code / Fowler smell baseline (Refactoring ch. 3) — judgement-call heuristics; repo standards override

---

## Standards Axis

**1. [BUG/HARD — GEMINI.md §Domain Integrity] Version numbering corrupt.**
`Assessment.cs:73` `var nextVersionNumber = _versions.Count + 1;` — `UpdateAssessmentContent.cs:43` loads via `AssessmentRepository.GetByIdAsync` (`AssessmentRepository.cs:17`) with **no `Include(a => a.Versions)`**, so `_versions.Count` is always 0 → every content update inserts `VersionNumber = 1` (no PK collision — PK is `Guid Id` per `AssessmentConfiguration.cs:36` — but duplicates break `GetAssessmentAsync`'s `Where(v => v.VersionNumber == version)`, `AssessmentQueryService.cs:39`).

**2. [BUG] Hybrid assessments crash on content update.**
`IAssestmentValidator.cs:64` `public AssessmentType AssessmentType => throw new NotImplementedException();` — hit the moment `Assessment.cs:67` compares `validator.AssessmentType != this.Type` → 500 for every Hybrid `PUT /assessments/{id}/content`.

**3. [HARD VIOLATION — GEMINI.md §Domain Integrity: "zero dependencies on Infrastructure or API layers"] Service Locator in Domain.**
`IAssestmentValidator.cs:4` `using Microsoft.Extensions.DependencyInjection;`, `:23` `private readonly IServiceProvider _serviceProvider;` + concrete `AssestmentValidtorFactory`/validators defined in Domain; resolved via DI (`DependencyInjection.cs:54-57`). Factory should live in Application/Infrastructure.

**4. [SMELL: Mysterious Name / repo convention] Public-surface typos.**
Folder+namespace `Servies` (`IAssestmentValidator.cs:6`), `IAssestmentValidator`, `IAssestmentValidtorFactory`, `AssestmentValidtorFactory` (10+ refs across Application); `Persistance` namespace (`AppDbContext.cs:9`, `AssessmentsModule.cs:35,43`) — though `Persistance` is a cross-module convention (Tenants/Identity/Courses/VideoUploading too); `Tenats` in Shared (`Tenats.Using` in Domain using-lines). Renaming breaks compile — sweep as one change.

**5. [SMELL: misconfiguration] Consumer registered by accident.**
`AssessmentsModule.cs:43` scans only the Presentation assembly: `AddModuleConsumers<...>(typeof(AssessmentsModule).Assembly)` — `CreateAssessmentRequestHandler` lives in Infrastructure (`Consumers/CreateAssessmentRequestHandler.cs:11`). It's discovered at runtime only via `Program.cs:248` global `x.AddConsumers(..., assemblies)` megascan (evidence: `run_output.txt:31`; logs also committed to repo). Correct pattern: `DocumentsModule.cs:53` / `CoursesModule.cs:61` explicitly add the Infrastructure assembly.

**6. [HARD VIOLATION — GEMINI.md §Implementation Standards 4] Zero tests.**
`tests/Modules` has no Assessments project (verified via csproj glob); grading service, version numbering, validators, resolvers, consumer all untested.

**7. [SMELL: Speculative Generality] Dead code.**
No callers found (whole-src grep): `Assessment.Publish()` (`:82`) → `AssessmentPublishedDomainEvent` + its MediatR handler chain, `Archive()` (`:95`), `UpdateInformation()` (`:49`) → `AssessmentMetadataUpdatedDomainEvent`, `GetByIdWithCurrentVersionAsync` (`IAssessmentRepository.cs:9`), `CreateAssessmentRequest.TenantId` field (`CreateAssessmentMessages.cs:11`, ignored by consumer).

**8. [SMELL: Primitive Obsession]**
`AssessmentContentModels.cs:18` `object RenderData`, `SubmissionModels.cs:7` `Dictionary<string, object> Answers`, `:12` `object? Value` — no schema validation, serialized raw to jsonb (`AssessmentConfiguration.cs:39-40`, `AssessmentSubmissionConfiguration.cs:17-18`); graders rely on `answer?.ToString()` (`McqQuestionResolver.cs:16`).

**9. [SMELL] Swallowed validation.**
`CreateAssessment.cs:77` discards `UpdateContent`'s `ErrorOr` → invalid `InitialContent` silently yields a contentless Draft.

**10. [SMELL] Anonymous read endpoints under a tenant-scoped model.**
`GetAssessmentEndpoint.cs:103` `AllowAnonymous(); // For this demo`, `ListAssessmentsEndpoint.cs:159` (`// WARNING`).

### Verified red herrings (not leaks)
- Tenant scoping is enforced via global query filters (`ModelBuilderExtensions.cs:55-77` via `AppDbContext.cs:30`) plus TenantId indexes on all 3 tables (`AddTenantIdIndexes.cs:14-29`).
- MassTransit consumer naming / outbox setup matches the program-wide convention.

---

## Spec Axis

### (a) Missing / partial requirements

1. **No "start attempt" flow.** PRD §3: "*Students upload photos of handwritten work; Teachers use a specialized grading interface*". Grep of whole `src/`: `AssessmentSubmission.Create` (`AssessmentSubmission.cs:34`) is invoked by **zero** endpoints/commands — no one can create a submission (`Status=InProgress`, the only submittable state). The submit endpoint is unusable in practice.
2. **No teacher grading workflow.** PRD §3: "*specialized grading interface for manual correction and feedback*". No endpoint views submission answers/images (`ListSubmissionsEndpoint` returns `SubmissionSummaryDto(Id, ... TotalScore)` — no `Responses`/`MediaUrl`, `GetSubmissions.cs:8`); no grade/finalize command exists; `FinalizeGrading` is only auto-invoked (`SubmitAssessment.cs:59`). `UnderReview` submissions are terminal dead-ends; students also have no "view my results" query.
3. **Automated grading partial.** PRD §3: "*Supports MCQ, True/False, and LaTeX-based Math expressions*". `QuestionType` has only `MCQ, Handwritten, Voice, Video` (`AssessmentEnums.cs:16-22`); only `McqQuestionResolver` registered (`DependencyInjection.cs:61`). No TrueFalse/LaTeX resolvers.
4. **RBAC unenforceable.** `system-roles.md:33` lists `assessments:Submit` under StudentAccess, but `ManagedPolicies.json:8-13` grants only `courses:View`, `video:Stream`, `subjects:*`; TeacherAccess:34-38 grants `exams:Create/Grade/View` instead of `assessments:*`. Also `ResourceArn.ForAssessmentSubmission` emits service segment `az:submission` (`ResourceArn.cs:114`), absent from the registry (`assessments`, `system-roles.md:56`), so submission-ARN policies can never match.
5. **Assessment publish/archive unreachable.** `Assessment.Publish()/Archive()/UpdateInformation` have no command/endpoint; `AssessmentPublishedDomainEvent` handler `AssessmentDomainEventHandlers.cs:60` is dead code.
6. **Answer-key leak.** `GetAssessmentEndpoint.cs:38`: `AllowAnonymous(); // For this demo` returns full `AssessmentContent` including `CorrectChoiceId` (`AssessmentContentModels.cs:30`), `Rubric`/`AiHint` (:34-35) to anonymous callers; `ListAssessments` (`:38 AnonymousAllow(); // WARNING`) lists all Draft/Archived assessments (`AssessmentQueryService.cs:87`, no status filter) — contradicts PRD §3/Publishing ("invisible to students").

### (b) Scope creep

- `Voice`/`Video` question types (`AssessmentEnums.cs:20-21`) and `AiGradingInfo` with AI transcription/suggested score (`SubmissionModels.cs:19-25`) — in no spec.
- `AssessmentMetadataChangedIntegrationEvent.cs:10` comment drift: `// "Practice", "Final", etc.` vs actual enum `MCQ/Handwritten/Hybrid`.

### (c) Implemented-but-wrong

- `HybridAssessmentValidator.AssessmentType => throw new NotImplementedException()` (`IAssestmentValidator.cs:64`): creating a Hybrid assessment with `InitialContent` throws an unhandled exception → 500; its `Validate` unconditionally returns Success (no validation).
- Submit endpoint invents status: `SubmitAssessment.cs:64` returns `"UnderReview"` whenever score is null, regardless of actual state.
- `GetByIdWithCurrentVersionAsync` (`IAssessmentRepository.cs:9`) — declared, never called (dead repo method).

---

## Summary

- **Standards axis: 10 findings** — 3 hard `GEMINI.md` violations (versioning bug #1, Domain DI/Service-Locator #3, zero tests #6) plus 7 bugs/smells — worst: **version numbering corrupts every content update** (`_versions.Count + 1` on a never-loaded collection, `Assessment.cs:73`).
- **Spec axis: 11 findings** — 6 missing/partial, 2 scope creep, 3 implemented-but-wrong — worst: **students can never start an assessment** (`AssessmentSubmission.Create` unreachable, `AssessmentSubmission.cs:34`), making the entire handwritten → teacher-grading loop unbuildable.

The two axes are deliberately separate (per the code-review skill): a change can pass one and fail the other, so no cross-axis ranking is applied. Instrumented evidence for scrutiny directives that turned out to be red herrings is recorded under each axis so future audits don't re-litigate them.
