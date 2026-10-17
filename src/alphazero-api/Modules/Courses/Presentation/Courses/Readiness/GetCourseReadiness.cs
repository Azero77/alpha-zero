using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseReadiness;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Readiness;

public record GetCourseReadinessRequest
{
    public Guid CourseId { get; init; }
}

public class GetCourseReadinessSummary : Summary<GetCourseReadinessEndpoint>
{
    public GetCourseReadinessSummary()
    {
        Summary = "Evaluates course publishing readiness";
        Description = "Checks course overview completeness, section/item existence, asset transcoding states, and active pricing plans.";
        Response<CourseReadiness>(200, "Readiness evaluation completed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:View permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
    }
}

public class GetCourseReadinessEndpoint : Endpoint<GetCourseReadinessRequest, CourseReadiness>
{
    private readonly CoursesModule _module;

    public GetCourseReadinessEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/{CourseId}/readiness");
        this.AccessControl("courses:View", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new GetCourseReadinessSummary());
    }

    public override async Task HandleAsync(GetCourseReadinessRequest req, CancellationToken ct)
    {
        var query = new GetCourseReadinessQuery(req.CourseId);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
