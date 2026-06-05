using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Shared.Queries;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Presentation.Courses.List;

public record ListCoursesRequest
{
    public Guid? SubjectId { get; init; }
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListCoursesSummary : Summary<ListCoursesEndpoint>
{
    public ListCoursesSummary()
    {
        Summary = "Lists all courses with pagination";
        Description = "Returns a paged list of courses for the current tenant. Optionally filterable by subject.";
        Response<PagedResult<CourseDto>>(200, "Courses retrieved successfully");
    }
}

public class ListCoursesEndpoint : Endpoint<ListCoursesRequest, PagedResult<CourseDto>>
{
    private readonly CoursesModule _module;

    public ListCoursesEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses");
        this.AccessControl("courses:View", (req, tenantId) => ResourceArn.Create("courses", tenantId.ToString(), "*").Value);
        Description(d => d.WithTags("Courses"));
        Summary(new ListCoursesSummary());
    }

    public override async Task HandleAsync(ListCoursesRequest req, CancellationToken ct)
    {
        var query = new ListCoursesQuery(req.SubjectId, req.Page, req.PerPage);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
