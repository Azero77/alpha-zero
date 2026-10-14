# Implementation Plan: Courses Application QueryService & AsNoTracking Refactor

## Goal Description
Refactor all queries under `Courses/Application` that currently depend on tracked repositories (`IRepository<T>`, `ICourseRepository`, `ICourseOverviewRepository`, `IEnrollementRepository`, `ISubjectRepository`) to use read-only, untracked query services (`IQueryService`). In addition, add an `AsNoTracking()` option to `ICourseQueryBuilder` and `CourseQueryBuilder` so that query builder operations can execute without entity change tracking.

---

## User Review Required
> [!NOTE]
> All query handlers will solely depend on `IQueryService` interfaces (`ICourseQueryService`, `IEnrollmentQueryService`, `ISubjectQueryService`).
> The repositories will retain their command-side mutation responsibilities.
> No breaking changes to HTTP API contracts or FastEndpoints routes.

---

## Proposed Changes

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                Courses Application                                    │
│                                                                                        │
│   GetCourseOverviewQueryHandler         GetCoursePoolQueryHandler                     │
│                │                                     │                                 │
│                ▼                                     ▼                                 │
│     [ICourseQueryService]                 [ICourseQueryService]                       │
│                ▲                                     ▲                                 │
│   GetCourseOverviewPreRequestHandler                 │                                 │
│                                                                                        │
│   GetEnrollementQueryHandler            GetSubjectQueryHandler                        │
│                │                                     │                                 │
│                ▼                                     ▼                                 │
│    [IEnrollmentQueryService]              [ISubjectQueryService]                      │
└──────────────────────────────────────┬─────────────────────────────────────────────────┘
                                       │
                                       ▼ (Infrastructure - AsNoTracking Queries)
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              Courses Infrastructure                                    │
│                                                                                        │
│    CourseQueryService                   EnrollmentQueryService   SubjectQueryService  │
│    CourseQueryBuilder (with AsNoTracking() support)                                   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

### 1. `ICourseQueryBuilder` & `CourseQueryBuilder` (`AsNoTracking` Option)

#### [MODIFY] [`ICourseQueryBuilder.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Repositories/ICourseQueryBuilder.cs)
- Add `ICourseQueryBuilder AsNoTracking();` method to the interface.
```csharp
namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseQueryBuilder
{
    ICourseQueryBuilder ForCourse(Guid courseId);
    ICourseQueryBuilder WithSections();
    ICourseQueryBuilder WithSection(Guid sectionId);
    ICourseQueryBuilder WithItems(bool include = true);
    ICourseQueryBuilder WithResources(bool include = true);
    ICourseQueryBuilder WithPlans(bool include = true);
    ICourseQueryBuilder AsSplitQuery();
    ICourseQueryBuilder IgnoreQueryFilters();
    ICourseQueryBuilder AsNoTracking(); // <-- NEW

    Task<Course?> ExecuteAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}
```

#### [MODIFY] [`CourseQueryBuilder.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Repositories/CourseQueryBuilder.cs)
- Add `private bool _asNoTracking;` field.
- Implement `public ICourseQueryBuilder AsNoTracking() { _asNoTracking = true; return this; }`.
- In `BuildQuery()`, apply `if (_asNoTracking) query = query.AsNoTracking();`.
```csharp
    private bool _asNoTracking;

    public ICourseQueryBuilder AsNoTracking()
    {
        _asNoTracking = true;
        return this;
    }

    private IQueryable<Course> BuildQuery()
    {
        var query = _context.Courses.AsQueryable();

        if (_asNoTracking)
            query = query.AsNoTracking();

        if (_isSplitQuery)
            query = query.AsSplitQuery();

        if (_ignoreQueryFilters)
            query = query.IgnoreQueryFilters();
        ...
```

---

### 2. `GetCourseOverview` & `GetCourseOverviewPreRequestHandler` Refactor

#### [MODIFY] [`ICourseQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Queries/ICourseQueryService.cs)
- Add:
```csharp
Task<CourseOverviewDto?> GetCourseOverviewAsync(Guid courseId, CancellationToken cancellationToken = default);
```

#### [MODIFY] [`CourseQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Queries/CourseQueryService.cs)
- Implement `GetCourseOverviewAsync` using `AsNoTracking()` and projection to `CourseOverviewDto`:
```csharp
public async Task<CourseOverviewDto?> GetCourseOverviewAsync(Guid courseId, CancellationToken cancellationToken = default)
{
    var query = from course in _context.Courses.AsNoTracking()
                join courseOverview in _context.CourseOverviews.AsNoTracking()
                    on course.Id equals courseOverview.CourseId
                where course.Id == courseId
                select new CourseOverviewDto(
                    courseOverview.Id,
                    course.Id,
                    course.Status.ToString(),
                    courseOverview.DescriptionContent.Value,
                    courseOverview.TargetAudienceContent == null ? null : courseOverview.TargetAudienceContent.Value,
                    courseOverview.LearningObjectivesContent == null ? null : courseOverview.LearningObjectivesContent.Value,
                    courseOverview.CoverImageDocumentId,
                    courseOverview.ETag);

    return await query.FirstOrDefaultAsync(cancellationToken);
}
```

#### [MODIFY] [`GetCourseOverview.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Courses/Queries/GetCourseOverview/GetCourseOverview.cs)
- Replace `ICourseOverviewRepository _courseOverviewRepository` dependency with `ICourseQueryService _courseQueryService`.
```csharp
public class GetCourseOverviewQueryHandler : IRequestHandler<GetCourseOverviewQuery, ErrorOr<CourseOverviewDto>>
{
    private readonly ICourseQueryService _courseQueryService;

    public GetCourseOverviewQueryHandler(ICourseQueryService courseQueryService)
    {
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<CourseOverviewDto>> Handle(GetCourseOverviewQuery request, CancellationToken cancellationToken)
    {
        var overview = await _courseQueryService.GetCourseOverviewAsync(request.CourseId, cancellationToken);
        if (overview is null)
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");

        return overview;
    }
}
```

#### [MODIFY] [`GetCourseOverviewPreRequestHandler.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Courses/Queries/GetCourseOverview/GetCourseOverviewPreRequestHandler.cs)
- Replace `ICourseOverviewRepository _courseOverviewRepository` dependency with `ICourseQueryService _courseQueryService`.
```csharp
public class GetCourseOverviewPreRequestHandler : IPreRequestHandler<GetCourseOverviewQuery>
{
    private readonly ICourseQueryService _courseQueryService;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;
    private readonly ITenantProvider _tenantProvider;

    public GetCourseOverviewPreRequestHandler(
        ICourseQueryService courseQueryService,
        IPolicyEvaluatorService policyEvaluatorService,
        ITenantProvider tenantProvider)
    {
        _courseQueryService = courseQueryService;
        _policyEvaluatorService = policyEvaluatorService;
        _tenantProvider = tenantProvider;
    }

    public async Task<ErrorOr<Success>> PreHandle(
        GetCourseOverviewQuery request,
        AuthorizationContext? context,
        CancellationToken cancellationToken)
    {
        var overview = await _courseQueryService.GetCourseOverviewAsync(request.CourseId, cancellationToken);
        if (overview is null)
        {
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");
        }
        ...
```

---

### 3. `GetCoursePool` Refactor

#### [MODIFY] [`ICourseQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Queries/ICourseQueryService.cs)
- Add:
```csharp
Task<List<CourseAssetDto>> GetCoursePoolAsync(Guid courseId, bool includeAssigned = false, CancellationToken cancellationToken = default);
```

#### [MODIFY] [`CourseQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Queries/CourseQueryService.cs)
- Implement `GetCoursePoolAsync` using `AsNoTracking()` in a single optimized query without repository tracking:
```csharp
public async Task<List<CourseAssetDto>> GetCoursePoolAsync(
    Guid courseId, 
    bool includeAssigned = false, 
    CancellationToken cancellationToken = default)
{
    var query = _context.Set<CourseAsset>()
        .AsNoTracking()
        .Where(a => a.CourseId == courseId && a.State == CourseAssetState.Available);

    if (!includeAssigned)
    {
        var assignedAssetIds = _context.Set<CurriculumResource>()
            .AsNoTracking()
            .Where(r => r.CourseAssetId != null)
            .Select(r => r.CourseAssetId!.Value);

        query = query.Where(a => !assignedAssetIds.Contains(a.Id));
    }

    var assets = await query
        .OrderBy(a => a.UploadedUtcAt)
        .ToListAsync(cancellationToken);

    return assets.Select(MapCourseAssetToDto).ToList();
}

private static CourseAssetDto MapCourseAssetToDto(CourseAsset asset)
{
    var dto = new CourseAssetDto(
        asset.Id,
        asset.Title,
        asset.CourseAssetType.ToString(),
        asset.State.ToString(),
        asset.ResourceArn.Value,
        asset.UploadedUtcAt);

    return asset switch
    {
        VideoCourseAsset video => dto with
        {
            Duration = video.Duration,
            ThumbnailUrl = video.ThumbnailUrl
        },
        DocumentCourseAsset doc => dto with
        {
            FileName = doc.FileName
        },
        AssessmentCourseAsset assessment => dto with
        {
            QuestionsNumber = assessment.QuestionsCount
        },
        _ => dto
    };
}
```

#### [MODIFY] [`GetCoursePool.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Courses/Queries/GetCoursePool.cs)
- Remove `IRepository<CourseAsset>` and `ICourseRepository`.
- Inject `ICourseQueryService _courseQueryService`.
```csharp
public sealed class GetCoursePoolQueryHandler 
    : IRequestHandler<GetCoursePoolQuery, ErrorOr<List<CourseAssetDto>>>
{
    private readonly ICourseQueryService _courseQueryService;

    public GetCoursePoolQueryHandler(ICourseQueryService courseQueryService)
    {
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<List<CourseAssetDto>>> Handle(
        GetCoursePoolQuery request, CancellationToken ct)
    {
        var poolAssets = await _courseQueryService.GetCoursePoolAsync(
            request.CourseId, 
            request.IncludeAssigned, 
            ct);

        return poolAssets;
    }
}
```

---

### 4. `GetEnrollement` Refactor

#### [MODIFY] [`IEnrollmentQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Queries/IEnrollmentQueryService.cs)
- Add:
```csharp
Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
```

#### [MODIFY] [`EnrollmentQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Queries/EnrollmentQueryService.cs)
- Implement `GetEnrollmentByIdAsync`:
```csharp
public async Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
{
    return await _context.Set<Enrollement>()
        .AsNoTracking()
        .Where(e => e.Id == enrollmentId)
        .Select(e => new EnrollmentDto(
            e.Id,
            e.StudentId,
            e.CourseId,
            e.Progress.CompletedBitsMask,
            e.Progress.IsAllComplete,
            e.TenantId))
        .FirstOrDefaultAsync(cancellationToken);
}
```

#### [MODIFY] [`GetEnrollement.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Enrollements/Queries/GetEnrollement/GetEnrollement.cs)
- Replace `IEnrollementRepository _enrollementRepository` with `IEnrollmentQueryService _enrollmentQueryService`.
```csharp
public sealed class GetEnrollementQueryHandler : IRequestHandler<GetEnrollementQuery, ErrorOr<EnrollmentDto>>
{
    private readonly IEnrollmentQueryService _enrollmentQueryService;

    public GetEnrollementQueryHandler(IEnrollmentQueryService enrollmentQueryService)
    {
        _enrollmentQueryService = enrollmentQueryService;
    }

    public async Task<ErrorOr<EnrollmentDto>> Handle(GetEnrollementQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollmentQueryService.GetEnrollmentByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null)
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        return enrollment;
    }
}
```

---

### 5. `GetSubject` Refactor

#### [MODIFY] [`ISubjectQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Queries/ISubjectQueryService.cs)
- Add:
```csharp
Task<SubjectDto?> GetSubjectByIdAsync(Guid subjectId, CancellationToken cancellationToken = default);
```

#### [MODIFY] [`SubjectQueryService.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Queries/SubjectQueryService.cs)
- Implement `GetSubjectByIdAsync`:
```csharp
public async Task<SubjectDto?> GetSubjectByIdAsync(Guid subjectId, CancellationToken cancellationToken = default)
{
    return await _context.Set<Subject>()
        .AsNoTracking()
        .Where(s => s.Id == subjectId)
        .Select(s => new SubjectDto(s.Id, s.Name, s.Description))
        .FirstOrDefaultAsync(cancellationToken);
}
```

#### [MODIFY] [`GetSubject.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Subjects/Queries/GetSubject/GetSubject.cs)
- Replace `ISubjectRepository _subjectRepository` with `ISubjectQueryService _subjectQueryService`.
```csharp
public sealed class GetSubjectQueryHandler : IRequestHandler<GetSubjectQuery, ErrorOr<SubjectDto>>
{
    private readonly ISubjectQueryService _subjectQueryService;

    public GetSubjectQueryHandler(ISubjectQueryService subjectQueryService)
    {
        _subjectQueryService = subjectQueryService;
    }

    public async Task<ErrorOr<SubjectDto>> Handle(GetSubjectQuery request, CancellationToken cancellationToken)
    {
        var subject = await _subjectQueryService.GetSubjectByIdAsync(request.SubjectId, cancellationToken);
        if (subject is null)
            return Error.NotFound("Subject.NotFound", "Subject not found.");

        return subject;
    }
}
```

---

### 6. Tests Updates

#### [MODIFY] [`GetCourseOverviewPreRequestHandlerTests.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/tests/Modules/Courses/Courses.UnitTests/Application/Courses/GetCourseOverviewPreRequestHandlerTests.cs)
- Update mock from `ICourseOverviewRepository` to `ICourseQueryService`:
  - `_courseQueryService.GetCourseOverviewAsync(_courseId, ...)` instead of `_overviewRepository.GetByCourseIdAsync(_courseId, ...)`.

#### [NEW] Unit Tests for New Query Handlers & Services
- Add unit tests for `GetCourseOverviewQueryHandler`, `GetCoursePoolQueryHandler`, `GetEnrollementQueryHandler`, and `GetSubjectQueryHandler` asserting they delegate to their respective `IQueryService` and return expected DTOs or Not Found errors.
- Add unit test for `CourseQueryBuilder.AsNoTracking()` verifying the flag is set and applied.

---

## Verification Plan

### Automated Tests
1. **Architecture Rule Enforcement**:
   ```bash
   dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
   ```
   Ensures query handlers do not have repository dependencies.
2. **Courses Unit Tests**:
   ```bash
   dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj
   ```
3. **Courses Integration Tests**:
   ```bash
   dotnet test tests/Modules/Courses/Courses.Tests.Integration/Courses.Tests.Integration.csproj
   ```
   Ensures all 32 integration tests (including course overview, pool, enrollment, and subject) continue to pass seamlessly with the untracked query services.
4. **Full Solution Test Suite**:
   ```bash
   dotnet test
   ```
   Ensures 100% of tests pass across all projects in the solution.
