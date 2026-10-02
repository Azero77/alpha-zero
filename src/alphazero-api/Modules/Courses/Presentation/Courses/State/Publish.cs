using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record PublishCourseRequest
{
    public Guid CourseId { get; init; }
}

public class PublishCourseSummary : Summary<PublishCourseEndpoint>
{
    public PublishCourseSummary()
    {
        Summary = "Publishes an approved course";
        Description = "Transitions course status from Approved to Published, making it discoverable and enrollable.";
        Response(204, "Course published successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Course.NoPlans - course must have at least one plan before publishing)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Publish permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Course.Status - only approved courses can be published)");
    }
}

public class PublishCourseEndpoint : Endpoint<PublishCourseRequest>
{
    private readonly CoursesModule _module;

    public PublishCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }
    public override void Configure()
    {
        Patch("/courses/{CourseId}/publish");
        this.AccessControl("courses:Publish", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d
            .WithTags("Courses")
            .Accepts<PublishCourseRequest>("application/json"));
        Summary(new PublishCourseSummary());
    }

    public override async Task HandleAsync(PublishCourseRequest req, CancellationToken ct)
    {
        var command = new PublishCourseCommand(req.CourseId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
