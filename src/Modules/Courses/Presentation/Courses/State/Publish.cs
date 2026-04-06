using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record PublishCourseRequest
{
    public Guid CourseId { get; init; }
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
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
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
