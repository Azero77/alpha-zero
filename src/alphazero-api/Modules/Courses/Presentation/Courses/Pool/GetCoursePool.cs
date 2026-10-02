using AlphaZero.Modules.Courses.Application.Courses.Queries;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Pool;

public record GetCoursePoolRequest
{
    public Guid CourseId { get; init; }
}

public class GetCoursePoolSummary : Summary<GetCoursePoolEndpoint>
{
    public GetCoursePoolSummary()
    {
        Summary = "Retrieves the course asset pool";
        Description = "Returns all assets in the course pool with Available state ready to be assigned to the curriculum.";
        Response<List<CourseAssetDto>>(200, "Assets retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:View permission)");
    }
}

public class GetCoursePoolEndpoint : Endpoint<GetCoursePoolRequest, List<CourseAssetDto>>
{
    private readonly CoursesModule _module;

    public GetCoursePoolEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/{CourseId}/pool");
        this.AccessControl("courses:View", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new GetCoursePoolSummary());
    }

    public override async Task HandleAsync(GetCoursePoolRequest req, CancellationToken ct)
    {
        var query = new GetCoursePoolQuery(req.CourseId);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
