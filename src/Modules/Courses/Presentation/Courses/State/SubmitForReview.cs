using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record SubmitForReviewRequest
{
    public Guid CourseId { get; init; }
}

public class SubmitForReviewEndpoint : Endpoint<SubmitForReviewRequest>
{
    private readonly CoursesModule _module;

    public SubmitForReviewEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Patch("/courses/{CourseId}/review");
        this.AccessControl("courses:Submit", req => ResourceArn.ForCourse(Guid.Empty, req.CourseId));
        Description(d => d.WithTags("Courses"));
    }

    public override async Task HandleAsync(SubmitForReviewRequest req, CancellationToken ct)
    {
        var command = new SubmitCourseForReviewCommand(req.CourseId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
