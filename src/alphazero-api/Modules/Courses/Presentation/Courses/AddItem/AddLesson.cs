using AlphaZero.Modules.Courses.Application.Courses.Commands.AddLesson;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Authorization;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Presentation.Courses.AddItem;

public record AddLessonRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
    public Guid VideoId { get; init; }
}

public class AddLessonSummary : Summary<AddLessonEndpoint>
{
    public AddLessonSummary()
    {
        Summary = "Adds a video lesson to a course section";
        Description = "Appends a lesson curriculum item linked to a video.";
        Response(204, "Lesson added successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId, SectionId, Title, VideoId)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Not found (Course.NotFound, Course.Section)");
    }
}

public class AddLessonEndpoint : Endpoint<AddLessonRequest>
{
    private readonly CoursesModule _module;

    public AddLessonEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/{SectionId}/lessons");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new AddLessonSummary());
    }

    public override async Task HandleAsync(AddLessonRequest req, CancellationToken ct)
    {
        var command = new AddLessonCommand(req.CourseId, req.SectionId, req.Title, req.VideoId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
