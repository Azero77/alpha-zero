using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.ListSubjects;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using MediatR;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Subjects.List;

public record ListSubjectsRequest
{
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListSubjectsSummary : Summary<ListSubjectsEndpoint>
{
    public ListSubjectsSummary()
    {
        Summary = "Lists all subjects with pagination";
        Description = "Returns a paged list of educational subjects for the current tenant.";
        Response<PagedResult<SubjectDto>>(200, "Subjects retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing subjects:List permission)");
    }
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
        this.AccessControl("subjects:List", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Subjects"));
        Summary(new ListSubjectsSummary());
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
