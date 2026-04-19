using AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Reorder.Sections;

public record ReorderSectionsRequest
{
    public Guid CourseId { get; init; }
    public List<Guid> SectionIds { get; init; } = new();
}

public class ReorderSectionsEndpoint : Endpoint<ReorderSectionsRequest>
{
    private readonly CoursesModule _module;

    public ReorderSectionsEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/reorder");
        this.AccessControl("courses:Edit", req => ResourceArn.ForCourse(Guid.Empty, req.CourseId));
        Description(d => d.WithTags("Courses"));
    }

    public override async Task HandleAsync(ReorderSectionsRequest req, CancellationToken ct)
    {
        var command = new ReorderSectionsCommand(req.CourseId, req.SectionIds);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
