using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.ListSubjects;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Subjects.List;

public record ListSubjectsRequest
{
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListSubjectsEndpoint : Endpoint<ListSubjectsRequest, PagedResult<SubjectDto>>
{
    private readonly CoursesModule _module;

    public ListSubjectsEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/subjects");
        AllowAnonymous();
        Description(d => d.WithTags("Subjects"));
    }

    public override async Task HandleAsync(ListSubjectsRequest req, CancellationToken ct)
    {
        var query = new ListSubjectsQuery(req.Page, req.PerPage);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
