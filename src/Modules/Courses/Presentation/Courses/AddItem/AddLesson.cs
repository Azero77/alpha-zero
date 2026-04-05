using AlphaZero.Modules.Courses.Application.Courses.Commands.AddLesson;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.AddItem;

public record AddLessonRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
    public Guid VideoId { get; init; }
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
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
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
