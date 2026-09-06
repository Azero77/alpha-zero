using AlphaZero.Modules.Assessments.Application.Assessments.Commands.UpdateContent;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Assessments.Presentation.Endpoints.Assessments.UpdateContent;

public record UpdateAssessmentContentRequest
{
    public Guid AssessmentId { get; init; }
    public AssessmentContent Content { get; init; } = default!;
}

public class UpdateAssessmentContentSummary : Summary<UpdateAssessmentContentEndpoint>
{
    public UpdateAssessmentContentSummary()
    {
        Summary = "Updates the content of an existing assessment";
        Description = "Creates a new content version snapshot for the assessment.";
        Response(204, "Assessment content updated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (AssessmentId empty, Content null, or invalid questions)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing assessments:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Assessment not found (Assessment.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Assessment.Status - archived assessment cannot be modified)");
    }
}

public class UpdateAssessmentContentEndpoint : Endpoint<UpdateAssessmentContentRequest>
{
    private readonly AssessmentsModule _module;

    public UpdateAssessmentContentEndpoint(AssessmentsModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Put("/assessments/{AssessmentId}/content");
        this.AccessControl("assessments:Edit", (req, tenantId) => ResourceArn.ForAssessment(tenantId, req.AssessmentId));
        Description(d => d.WithTags("Assessments"));
        Summary(new UpdateAssessmentContentSummary());
    }

    public override async Task HandleAsync(UpdateAssessmentContentRequest req, CancellationToken ct)
    {
        var command = new UpdateAssessmentContentCommand(req.AssessmentId, req.Content);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
