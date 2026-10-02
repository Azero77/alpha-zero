using AlphaZero.Modules.Assessments.Application.Assessments.Commands.Create;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Assessments.Presentation.Endpoints.Assessments.Create;

public record CreateAssessmentRequest
{
    public string Title { get; init; } = default!;
    public string? Description { get; init; }
    public string Type { get; init; } = default!;
    public decimal PassingScore { get; init; }
    public AssessmentContent? InitialContent { get; init; }
}

public record CreateAssessmentResponse(Guid Id);

public class CreateAssessmentSummary : Summary<CreateAssessmentEndpoint>
{
    public CreateAssessmentSummary()
    {
        Summary = "Creates a new assessment";
        Description = "Initializes an assessment in Draft status with metadata and optional initial content.";
        Response<CreateAssessmentResponse>(201, "Assessment successfully created");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Assessment.Title, Assessment.PassingScore, or invalid Type)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound or unauthenticated)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing assessments:Create permission)");
    }
}

public class CreateAssessmentEndpoint : Endpoint<CreateAssessmentRequest, CreateAssessmentResponse>
{
    private readonly AssessmentsModule _module;

    public CreateAssessmentEndpoint(AssessmentsModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/assessments");
        this.AccessControl("assessments:Create", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Assessments"));
        Summary(new CreateAssessmentSummary());
    }

    public override async Task HandleAsync(CreateAssessmentRequest req, CancellationToken ct)
    {
        var command = new CreateAssessmentCommand(req.Title, req.Description, req.Type, req.PassingScore, req.InitialContent);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/assessments/{result.Value}", responseBody: new CreateAssessmentResponse(result.Value), cancellation: ct);
    }
}
