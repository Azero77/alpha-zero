using AlphaZero.Modules.Assessments.Application.Submissions.Queries.GetSubmissions;
using AlphaZero.Modules.Assessments.Presentation;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Assessments.Presentation.Endpoints.Submissions.List;

public record ListSubmissionsRequest
{
    public Guid? AssessmentId { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListSubmissionsSummary : Summary<ListSubmissionsEndpoint>
{
    public ListSubmissionsSummary()
    {
        Summary = "Lists assessment submissions";
        Description = "Retrieves a paginated list of submissions optionally filtered by assessment or status.";
        Response<PagedResult<SubmissionSummaryDto>>(200, "Submissions retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing assessments:ViewSubmissions permission)");
    }
}

public class ListSubmissionsEndpoint(AssessmentsModule module) : Endpoint<ListSubmissionsRequest, PagedResult<SubmissionSummaryDto>>
{
    public override void Configure()
    {
        Get("/assessments/submissions");
        
        // If AssessmentId is provided, we check access to that specific assessment.
        // If not, we check for tenant-wide submission view permissions.
        this.AccessControl("assessments:ViewSubmissions", (req, tenantId) => 
            req.AssessmentId.HasValue 
                ? ResourceArn.ForAssessment(tenantId, req.AssessmentId.Value) 
                : ResourceArn.ForTenant(tenantId));

        Description(d => d.WithTags("Submissions"));
        Summary(new ListSubmissionsSummary());
    }

    public override async Task HandleAsync(ListSubmissionsRequest req, CancellationToken ct)
    {
        var query = new GetSubmissionsQuery(req.AssessmentId, req.Status, req.Page, req.PerPage);
        var result = await module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
