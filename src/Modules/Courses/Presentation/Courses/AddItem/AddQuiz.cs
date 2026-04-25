using AlphaZero.Modules.Courses.Application.Courses.Commands.AddAssessment;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.AddItem;

public record AddAssessmentRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
    public Guid AssessmentId { get; init; }
}
public class AddAssessmentEndpoint : Endpoint<AddAssessmentRequest>
{
    private readonly CoursesModule _module;

    public AddAssessmentEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/{SectionId}/assessments");
        this.AccessControl("courses:Edit", req => ResourceArn.ForCourse(Guid.Empty, req.CourseId));
        Description(d => d.WithTags("Courses"));
    }

    public override async Task HandleAsync(AddAssessmentRequest req, CancellationToken ct)
    {
        var command = new AddAssessmentCommand(req.CourseId, req.SectionId, req.Title, req.AssessmentId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
