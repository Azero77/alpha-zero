using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record RejectCourseRequest
{
    public Guid CourseId { get; init; }
    public string Reason { get; init; } = default!;
}

public class RejectCourseEndpoint : Endpoint<RejectCourseRequest>
{
    private readonly CoursesModule _module;

    public RejectCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Patch("/courses/{CourseId}/reject");
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
    }

    public override async Task HandleAsync(RejectCourseRequest req, CancellationToken ct)
    {
        var command = new RejectCourseCommand(req.CourseId, req.Reason);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
