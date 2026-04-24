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
        this.AccessControl("assessments:Edit", req => ResourceArn.ForAssessment(Guid.Empty, req.AssessmentId));
        Description(d => d.WithTags("Assessments"));
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
