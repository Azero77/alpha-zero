using AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;
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
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
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
