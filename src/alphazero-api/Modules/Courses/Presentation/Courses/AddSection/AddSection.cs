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

public class AddSectionSummary : Summary<AddSectionEndpoint>
{
    public AddSectionSummary()
    {
        Summary = "Adds a curriculum section to a course";
        Description = "Appends a new numbered section to the specified course.";
        Response(204, "Section added successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId empty, Title empty/too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
    }
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
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new AddSectionSummary());
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
