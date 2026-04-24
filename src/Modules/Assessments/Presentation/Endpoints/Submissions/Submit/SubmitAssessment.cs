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
        this.AccessControl("assessments:Submit", req => ResourceArn.ForAssessmentSubmission(Guid.Empty, req.SubmissionId));
        Description(d => d.WithTags("Assessments"));
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
