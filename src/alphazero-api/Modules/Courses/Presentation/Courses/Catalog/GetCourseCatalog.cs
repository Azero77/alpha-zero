using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Catalog;

public record GetCourseCatalogRequest
{
    public string? Search { get; init; }
    public Guid? SubjectId { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class GetCourseCatalogSummary : Summary<GetCourseCatalogEndpoint>
{
    public GetCourseCatalogSummary()
    {
        Summary = "Student catalog: search, filter and page published courses";
        Description = "Permission-light browse surface for students and anonymous prospects (#32). Keyword search over title and description, subject filter, sort (newest | title), paging. Only Published courses are ever returned.";
        Response<PagedResult<CourseCatalogItemDto>>(200, "Catalog retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (page/per-page bounds, sort key)");
    }
}

public class GetCourseCatalogEndpoint : Endpoint<GetCourseCatalogRequest, PagedResult<CourseCatalogItemDto>>
{
    private readonly CoursesModule _module;

    public GetCourseCatalogEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/catalog");
        // Permission-light by design (#32): published-only is enforced inside the query, so
        // anonymous browse is content-safe; unpublished content is structurally unreachable.
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
        Summary(new GetCourseCatalogSummary());
    }

    public override async Task HandleAsync(GetCourseCatalogRequest req, CancellationToken ct)
    {
        var result = await _module.Send(
            new ListCourseCatalogQuery(req.Search, req.SubjectId, req.Sort, req.Page, req.PerPage),
            ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
