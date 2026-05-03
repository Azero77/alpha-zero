using AlphaZero.Modules.Assessments.IntegrationEvents;
using AlphaZero.Modules.Courses.Application.Courses.Commands.AddAssessment;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.AddItem;

public record AddAssessmentRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
    public Guid AssessmentId { get; init; }
    public string Type { get;init;  }
    public decimal PassingScore { get; set; }
    public string? Description { get; set; }
}
public class AddAssessmentEndpoint : Endpoint<AddAssessmentRequest>
{
    private readonly CoursesModule _module;
    private readonly ITenantProvider _tenantProvider;

    public AddAssessmentEndpoint(CoursesModule module, ITenantProvider tenantProvider)
    {
        _module = module;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/{SectionId}/assessments");
        this.AccessControl("courses:Edit", req => ResourceArn.ForCourse(Guid.Empty, req.CourseId));
        Description(d => d.WithTags("Courses"));
    }

    public override async Task HandleAsync(AddAssessmentRequest req, CancellationToken ct)
    {
        var tenant = _tenantProvider.GetTenant();
        
        if (tenant == null) { await this.SendErrorResponseAsync([Error.Validation("No TenantID is provided")]); return; }
        var command = new AddAssessmentCommand(req.CourseId, req.SectionId, req.Title, new CreateAssessmentRequest(req.Title,req.Type,req.PassingScore,req.Description,(Guid) tenant));
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
