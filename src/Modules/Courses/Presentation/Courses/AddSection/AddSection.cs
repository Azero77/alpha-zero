using AlphaZero.Modules.Courses.Application.Courses.Commands.AddSection;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.AddSection;

public record AddSectionRequest
{
    public Guid CourseId { get; init; }
    public string Title { get; init; } = default!;
}

public class AddSectionEndpoint : Endpoint<AddSectionRequest>
{
    private readonly CoursesModule _module;

    public AddSectionEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections");
        this.AccessControl("courses:Edit", req => ResourceArn.ForCourse(Guid.Empty, req.CourseId));
        Description(d => d.WithTags("Courses"));
    }

    public override async Task HandleAsync(AddSectionRequest req, CancellationToken ct)
    {
        var command = new AddSectionCommand(req.CourseId, req.Title);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
