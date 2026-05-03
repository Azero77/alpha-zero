using AlphaZero.Modules.Assessments.Application.Assessments.Queries.ListAssessments;
using AlphaZero.Modules.Assessments.Presentation;
using AlphaZero.Shared.Queries;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Assessments.Presentation.Endpoints.Assessments.List;

public record ListAssessmentsRequest
{
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListAssessmentsSummary : Summary<ListAssessmentsEndpoint>
{
    public ListAssessmentsSummary()
    {
        Summary = "Lists all assessments with pagination";
        Description = "Returns a paged list of assessments for the current tenant.";
        Response<PagedResult<AssessmentDto>>(200, "Assessments retrieved successfully");
    }
}

public class ListAssessmentsEndpoint : Endpoint<ListAssessmentsRequest, PagedResult<AssessmentDto>>
{
    private readonly AssessmentsModule _module;

    public ListAssessmentsEndpoint(AssessmentsModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/assessments");
        AllowAnonymous(); // Following the user's preference for this demo
        Description(d => d.WithTags("Assessments"));
        Summary(new ListAssessmentsSummary());
    }

    public override async Task HandleAsync(ListAssessmentsRequest req, CancellationToken ct)
    {
        var query = new ListAssessmentsQuery(req.Page, req.PerPage);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
