using AlphaZero.Modules.Assessments.Application.Assessments.Queries.GetAssessment;
using AlphaZero.Modules.Assessments.Presentation;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Assessments.Presentation.Endpoints.Assessments.Get;

public record GetAssessmentRequest 
{ 
    public Guid Id { get; init; } 
    public int? Version { get; init; }
}

public class GetAssessmentSummary : Summary<GetAssessmentEndpoint>
{
    public GetAssessmentSummary()
    {
        Summary = "Retrieves a specific assessment by ID";
        Description = "Returns full details of an assessment including its current content snapshot or a specific version.";
        Response<AssessmentDetailsDto>(200, "Assessment details retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Assessment or version not found (Assessment.NotFound, Assessment.VersionNotFound)");
    }
}

public class GetAssessmentEndpoint : Endpoint<GetAssessmentRequest, AssessmentDetailsDto>
{
    private readonly AssessmentsModule _module;

    public GetAssessmentEndpoint(AssessmentsModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/assessments/{Id}");
        AllowAnonymous(); // For this demo
        Description(d => d.WithTags("Assessments"));
        Summary(new GetAssessmentSummary());
    }

    public override async Task HandleAsync(GetAssessmentRequest req, CancellationToken ct)
    {
        var query = new GetAssessmentQuery(req.Id, req.Version);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
