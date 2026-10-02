using AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Reorder.Items;

public record ReorderItemsRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public List<Guid> ItemIds { get; init; } = new();
}

public class ReorderItemsSummary : Summary<ReorderItemsEndpoint>
{
    public ReorderItemsSummary()
    {
        Summary = "Reorders curriculum items in a section";
        Description = "Updates the sequence order of lessons/quizzes within a specific course section.";
        Response(204, "Items reordered successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Not found (Course.NotFound, Course.Section)");
    }
}

public class ReorderItemsEndpoint : Endpoint<ReorderItemsRequest>
{
    private readonly CoursesModule _module;

    public ReorderItemsEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/{SectionId}/reorder");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new ReorderItemsSummary());
    }

    public override async Task HandleAsync(ReorderItemsRequest req, CancellationToken ct)
    {
        var command = new ReorderItemsCommand(req.CourseId, req.SectionId, req.ItemIds);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
