# 🪐 Query Architecture & CQRS Separation Guidelines

This document outlines the standard architecture for reading and writing data in the AlphaZero modular monolith modules, enforcing strict command-query separation (CQRS) and preventing performance issues from change-tracked read models.

---

## 🏛️ Core Architectural Mandates

1. **Write Path (Commands):**
   * Uses domain repositories (inheriting from `BaseRepository<TContext, TEntity>` or `BaseDataModelRepository<TContext, TEntity, TDataModel>`).
   * Entities are **always tracked** by Entity Framework's Change Tracker.
   * `AsNoTracking()` must **never** be used in repositories that do not inherit from `BaseDataModelRepository`.
   * Generic collection queries (`Get(filter)`, `GetAll()`, paginated `Get`) are **forbidden** on the generic `IRepository<T>` interface.
   * If a write command needs to load a collection of aggregate roots, it must define a custom, intention-revealing method on its specific repository interface (e.g., `ICourseRepository.GetCoursesBySubjectIdAsync`).

2. **Read Path (Queries):**
   * Uses **Query Services** (interfaces prefixed with module name, e.g., `ICourseQueryService`).
   * Query services bypass the domain model entirely and project data directly from the DbContext (`TDataModel` or `TEntity`) into read-only DTOs.
   * All database queries in query services must run with `.AsNoTracking()` or `.AsNoTrackingAndIdentityResolution()`.
   * Query services live in `Application/Queries` and are implemented in `Infrastructure/Queries`.

---

## 🛠️ Code Structure & Directory Conventions

```
src/Modules/{ModuleName}/
├── Application/
│   └── Queries/
│       ├── ICourseQueryService.cs        <-- Consolidated module query service interface
│       └── GetCourse/
│           ├── GetCourse.cs              <-- MediatR Query & Handler (injects ICourseQueryService)
│           └── CourseDto.cs              <-- Read-only DTO
└── Infrastructure/
    ├── Queries/
    │   └── CourseQueryService.cs         <-- Direct projection with AsNoTracking()
    └── DependencyInjection.cs            <-- Manual registration of Query Services
```

---

## 📝 Query Service Implementation Pattern

### 1. Define the Interface (Application Layer)
Define the Query Service interface in the module's `Application/Queries` directory.

```csharp
// src/Modules/Courses/Application/Queries/ICourseQueryService.cs
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Shared.Queries;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Application.Queries;

public interface ICourseQueryService
{
    Task<ErrorOr<CourseDto>> GetCourseDetailsAsync(Guid courseId, CancellationToken ct = default);
    Task<PagedResult<CourseDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken ct = default);
}
```

### 2. Implement the Query Service (Infrastructure Layer)
Implement the service in `Infrastructure/Queries`. Inject the module's `DbContext` and use `.AsNoTracking()` with direct `.Select()` projections to map database rows straight to DTOs.

```csharp
// src/Modules/Courses/Infrastructure/Queries/CourseQueryService.cs
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

public class CourseQueryService : ICourseQueryService
{
    private readonly AppDbContext _context;

    public CourseQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ErrorOr<CourseDto>> GetCourseDetailsAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new CourseDto(
                c.Id,
                c.Title,
                c.Description,
                c.SubjectId,
                c.Status.ToString(),
                c.Sections.OrderBy(s => s.Order).Select(s => new SectionDto(
                    s.Id,
                    s.Title,
                    s.Order,
                    s.Items.OrderBy(i => i.Order).Select(i => new ItemDto(
                        i.Id,
                        i.Title,
                        i.MainType,
                        i.Order,
                        i.BitIndex,
                        new List<ResourceDto>() // Project resources here if needed
                    )).ToList()
                )).ToList()
            ))
            .FirstOrDefaultAsync(ct);

        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        return course;
    }

    public async Task<PagedResult<CourseDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken ct = default)
    {
        var query = _context.Courses.AsNoTracking();

        if (subjectId.HasValue)
            query = query.Where(c => c.SubjectId == subjectId.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Title)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(c => new CourseDto(
                c.Id,
                c.Title,
                c.Description,
                c.SubjectId,
                c.Status.ToString(),
                new List<SectionDto>()
            ))
            .ToListAsync(ct);

        return new PagedResult<CourseDto>(items, total, page, perPage);
    }
}
```

### 3. Register in Dependency Injection
Register the service manually in your module's `DependencyInjection.cs` file.

```csharp
moduleServices.AddScoped<ICourseQueryService, CourseQueryService>();
```

### 4. Consume in MediatR Query Handler
Inject the query service into the MediatR query handler.

```csharp
// src/Modules/Courses/Application/Courses/Queries/ListCourses/ListCoursesQueryHandler.cs
public sealed class ListCoursesQueryHandler : IRequestHandler<ListCoursesQuery, ErrorOr<PagedResult<CourseDto>>>
{
    private readonly ICourseQueryService _queryService;

    public ListCoursesQueryHandler(ICourseQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<ErrorOr<PagedResult<CourseDto>>> Handle(ListCoursesQuery request, CancellationToken cancellationToken)
    {
        return await _queryService.ListCoursesAsync(request.SubjectId, request.Page, request.PerPage, cancellationToken);
    }
}
```

---

## ⚠️ Key Constraints & Validation Rules

### 1. Stale Reads in Commands
*   **Rule:** **Never** inject or use `IQueryService` inside Command Handlers.
*   **Reason:** Because Query Services run with `AsNoTracking()`, they do not see modifications made in-memory inside the current Unit of Work (DbContext scope) prior to calling `SaveChangesAsync()`.
*   **Enforcement:** Command validation and invariant checks must load aggregates/entities via tracked repository methods (e.g., `GetById`, `GetFirst`).

### 2. Generic Query Deletion from `IRepository<T>`
*   The generic `IRepository<T>` has been cleaned up. Methods like `GetAll` and `Get(filter)` are deleted from the interface to prevent developers from querying collection lists through write repositories.
*   `BaseRepository` class retains these implementations internally so that custom repository implementations can call them internally if needed, but they are not exposed to application layers.

### 3. Automated NetArchTest Checks
A dedicated architecture test suite verifies that:
*   Classes in `Application/Queries` must **never** depend on `IRepository<T>`.
*   Classes in `Application/Commands` must **never** depend on `IQueryService` types (preventing stale reads during validation).
*   Any repository that does not inherit from `BaseDataModelRepository` must **not** call `.AsNoTracking()`. (Note: enforced during manual PR review since NetArchTest checks type references, not internal method calls).
