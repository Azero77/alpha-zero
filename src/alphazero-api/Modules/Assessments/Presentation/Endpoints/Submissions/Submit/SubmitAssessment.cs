using AlphaZero.Modules.Assessments.Application.Submissions.Commands.Submit;
using AlphaZero.Modules.Assessments.Domain.Models.Submissions;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Assessments.Presentation.Endpoints.Submissions.Submit;

public record SubmitAssessmentRequest
{
    public Guid SubmissionId { get; init; }
    public AssessmentSubmissionResponses Responses { get; init; } = default!;
}

public record SubmitAssessmentResponse(decimal? Score, string Status);

public class SubmitAssessmentSummary : Summary<SubmitAssessmentEndpoint>
{
    public SubmitAssessmentSummary()
    {
        Summary = "Submits responses for an assessment";
        Description = "Submits a student's responses, triggers automated grading, and marks for manual review if subjective questions are present.";
        Response<SubmitAssessmentResponse>(200, "Assessment submitted successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Submission.Empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing assessments:Submit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Not found (Submission.NotFound, Assessment.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Submission.Status - only in-progress submissions can be submitted)");
    }
}

public class SubmitAssessmentEndpoint : Endpoint<SubmitAssessmentRequest, SubmitAssessmentResponse>
{
    private readonly AssessmentsModule _module;

    public SubmitAssessmentEndpoint(AssessmentsModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/assessments/submissions/{SubmissionId}/submit");
        this.AccessControl("assessments:Submit", (req, tenantId) => ResourceArn.ForAssessmentSubmission(tenantId, req.SubmissionId));
        Description(d => d.WithTags("Assessments"));
        Summary(new SubmitAssessmentSummary());
    }

    public override async Task HandleAsync(SubmitAssessmentRequest req, CancellationToken ct)
    {
        var command = new SubmitAssessmentCommand(req.SubmissionId, req.Responses);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        // We could fetch the submission again to get the status, 
        // or just return the score if it was finalized immediately.
        await Send.OkAsync(new SubmitAssessmentResponse(result.Value, result.Value.HasValue ? "Graded" : "UnderReview"), ct);
    }
}
