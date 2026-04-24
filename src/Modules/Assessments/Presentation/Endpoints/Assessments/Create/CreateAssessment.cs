using AlphaZero.Modules.Assessments.Application.Assessments.Commands.Create;
using AlphaZero.Modules.Assessments.Domain.Enums;
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
    public AssessmentType Type { get; init; }
    public decimal PassingScore { get; init; }
    public AssessmentContent? InitialContent { get; init; }
}

public record CreateAssessmentResponse(Guid Id);

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
        this.AccessControl("assessments:Create", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Assessments"));
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
